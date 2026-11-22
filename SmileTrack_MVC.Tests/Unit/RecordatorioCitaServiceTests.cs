using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.Email;
using SmileTrack_MVC.Services.GestionDeCitas;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class RecordatorioCitaServiceTests
{
    [Fact]
    public void Reprogramar_CambiaIdentidadSoloCuandoCambiaFechaHora()
    {
        var cita = new Cita { FechaHora = ColombiaClock.Now.AddDays(2) };
        Guid original = cita.ProgramacionId;

        Assert.False(cita.Reprogramar(cita.FechaHora));
        Assert.Equal(original, cita.ProgramacionId);
        Assert.True(cita.Reprogramar(cita.FechaHora.AddHours(1)));
        Assert.NotEqual(original, cita.ProgramacionId);
    }

    [Fact]
    public async Task EnviarAsync_ReprogramacionInvalidaPendienteYConservaHistorialEnviado()
    {
        await using var db = CrearContexto(Guid.NewGuid().ToString());
        var (cita, _) = await SembrarCitaAsync(db);
        Guid versionAnterior = cita.ProgramacionId;
        db.RecordatoriosCita.AddRange(
            CrearRecordatorio(cita, versionAnterior, "pendiente"),
            CrearRecordatorio(cita, Guid.NewGuid(), "enviado", enviado: ColombiaClock.Now.AddDays(-1)));
        cita.Reprogramar(cita.FechaHora.AddHours(1));
        await db.SaveChangesAsync();

        var correo = new CorreoFalso();
        var servicio = CrearServicio(db, correo);
        var resultado = await servicio.EnviarAsync([cita.IdCita]);

        var recordatorios = await db.RecordatoriosCita.OrderBy(r => r.IdRecordatorio).ToListAsync();
        Assert.Equal(3, recordatorios.Count);
        Assert.Equal("cancelado", recordatorios[0].Estado);
        Assert.Equal("enviado", recordatorios[1].Estado);
        Assert.NotNull(recordatorios[1].EnviadoEn);
        Assert.Equal("enviado", recordatorios[2].Estado);
        Assert.Equal(cita.ProgramacionId, recordatorios[2].ProgramacionId);
        Assert.Equal(1, correo.Llamadas);
        Assert.Equal([cita.IdCita], resultado.IdsEnviados);
    }

    [Fact]
    public async Task ProcesarPendientesAsync_AplicaDosReintentosYAgotaEnElTercero()
    {
        await using var db = CrearContexto(Guid.NewGuid().ToString());
        var (cita, _) = await SembrarCitaAsync(db);
        var recordatorio = CrearRecordatorio(cita, cita.ProgramacionId, "pendiente");
        db.RecordatoriosCita.Add(recordatorio);
        await db.SaveChangesAsync();
        var correo = new CorreoFalso { Fallar = true };
        var servicio = CrearServicio(db, correo);

        await servicio.ProcesarPendientesAsync();
        db.ChangeTracker.Clear();
        recordatorio = await db.RecordatoriosCita.SingleAsync();
        Assert.Equal(1, recordatorio.Intentos);
        Assert.Equal("pendiente", recordatorio.Estado);
        Assert.InRange(recordatorio.ProximoIntentoEn!.Value,
            ColombiaClock.Now.AddMinutes(4), ColombiaClock.Now.AddMinutes(6));

        recordatorio.ProximoIntentoEn = ColombiaClock.Now.AddMinutes(-1);
        await db.SaveChangesAsync();
        await servicio.ProcesarPendientesAsync();
        db.ChangeTracker.Clear();
        recordatorio = await db.RecordatoriosCita.SingleAsync();
        Assert.Equal(2, recordatorio.Intentos);
        Assert.Equal("pendiente", recordatorio.Estado);
        Assert.InRange(recordatorio.ProximoIntentoEn!.Value,
            ColombiaClock.Now.AddMinutes(14), ColombiaClock.Now.AddMinutes(16));

        recordatorio.ProximoIntentoEn = ColombiaClock.Now.AddMinutes(-1);
        await db.SaveChangesAsync();
        await servicio.ProcesarPendientesAsync();
        db.ChangeTracker.Clear();
        recordatorio = await db.RecordatoriosCita.SingleAsync();
        Assert.Equal(3, recordatorio.Intentos);
        Assert.Equal("fallido", recordatorio.Estado);
        Assert.Null(recordatorio.ProximoIntentoEn);
        Assert.Equal(3, correo.Llamadas);
    }

    [Fact]
    public async Task ProcesarPendientesAsync_RecuperaReservaVencidaYNoEnviaCitaCancelada()
    {
        await using var db = CrearContexto(Guid.NewGuid().ToString());
        var (cita, _) = await SembrarCitaAsync(db);
        var recordatorio = CrearRecordatorio(cita, cita.ProgramacionId, "pendiente");
        recordatorio.Intentos = 1;
        recordatorio.ReservaId = Guid.NewGuid();
        recordatorio.ReservaHasta = ColombiaClock.Now.AddMinutes(-1);
        db.RecordatoriosCita.Add(recordatorio);
        var citaCancelada = new Cita
        {
            IdCita = 2, IdPaciente = cita.IdPaciente, FechaHora = ColombiaClock.Now.AddHours(2), Estado = "Cancelada"
        };
        db.Citas.Add(citaCancelada);
        db.RecordatoriosCita.Add(CrearRecordatorio(citaCancelada, citaCancelada.ProgramacionId, "pendiente"));
        await db.SaveChangesAsync();

        var correo = new CorreoFalso();
        await CrearServicio(db, correo).ProcesarPendientesAsync();

        db.ChangeTracker.Clear();
        recordatorio = await db.RecordatoriosCita.SingleAsync(r => r.IdCita == cita.IdCita);
        var cancelado = await db.RecordatoriosCita.SingleAsync(r => r.IdCita == 2);
        Assert.Equal("enviado", recordatorio.Estado);
        Assert.Equal(2, recordatorio.Intentos);
        Assert.Null(recordatorio.ReservaId);
        Assert.Equal("cancelado", cancelado.Estado);
        Assert.Equal(1, correo.Llamadas);
    }

    [Fact]
    public async Task ProcesarPendientesAsync_FalloAlGuardarExitoDejaReservaParaRecuperacion()
    {
        var correo = new CorreoFalso();
        var interceptor = new FallarGuardadoDespuesDelEnvio(() => correo.Enviado);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        await using var db = new AppDbContext(options);
        var (cita, _) = await SembrarCitaAsync(db);
        db.RecordatoriosCita.Add(CrearRecordatorio(cita, cita.ProgramacionId, "pendiente"));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearServicio(db, correo).ProcesarPendientesAsync());

        var estadoPersistido = await db.RecordatoriosCita.AsNoTracking().SingleAsync();
        Assert.Equal(1, correo.Llamadas);
        Assert.Equal("pendiente", estadoPersistido.Estado);
        Assert.Equal(1, estadoPersistido.Intentos);
        Assert.NotNull(estadoPersistido.ReservaId);
        Assert.True(estadoPersistido.ReservaHasta > ColombiaClock.Now);
    }

    [Fact]
    public async Task ProcesarPendientesAsync_TrabajadorConTokenAntiguoNoSobrescribeNuevoResultado()
    {
        string database = Guid.NewGuid().ToString();
        await using var inicial = CrearContexto(database);
        var (cita, _) = await SembrarCitaAsync(inicial);
        inicial.RecordatoriosCita.Add(CrearRecordatorio(cita, cita.ProgramacionId, "pendiente"));
        await inicial.SaveChangesAsync();

        var bloqueante = new CorreoFalso { Bloquear = true };
        await using var dbWorkerAnterior = CrearContexto(database);
        Task procesoAntiguo = CrearServicio(dbWorkerAnterior, bloqueante).ProcesarPendientesAsync();
        await bloqueante.Iniciado.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await using (var expirador = CrearContexto(database))
        {
            var actual = await expirador.RecordatoriosCita.SingleAsync();
            actual.ReservaHasta = ColombiaClock.Now.AddSeconds(-1);
            await expirador.SaveChangesAsync();
        }

        await using (var dbNuevoWorker = CrearContexto(database))
            await CrearServicio(dbNuevoWorker, new CorreoFalso()).ProcesarPendientesAsync();

        bloqueante.Liberar.TrySetResult();
        await procesoAntiguo;

        await using var verificacion = CrearContexto(database);
        var final = await verificacion.RecordatoriosCita.SingleAsync();
        Assert.Equal("enviado", final.Estado);
        Assert.Null(final.ReservaId);
        Assert.Equal(2, final.Intentos);
        Assert.Equal(1, bloqueante.Llamadas);
    }

    private static AppDbContext CrearContexto(string nombre) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombre).Options);

    private static RecordatorioCitaService CrearServicio(AppDbContext db, CorreoFalso correo) =>
        new(db, correo, NullLogger<RecordatorioCitaService>.Instance);

    private static async Task<(Cita Cita, Paciente Paciente)> SembrarCitaAsync(AppDbContext db)
    {
        var paciente = new Paciente
        {
            IdPaciente = 1, Nombres = "Paciente", Apellidos = "Sintético",
            Correo = "test@example.invalid", Estado = "activo"
        };
        var cita = new Cita
        {
            IdCita = 1, IdPaciente = 1, Paciente = paciente,
            FechaHora = ColombiaClock.Now.AddHours(12), Estado = "Programada"
        };
        db.Pacientes.Add(paciente);
        db.Citas.Add(cita);
        await db.SaveChangesAsync();
        return (cita, paciente);
    }

    private static RecordatorioCita CrearRecordatorio(Cita cita, Guid version, string estado,
        DateTime? enviado = null) => new()
    {
        IdCita = cita.IdCita,
        ProgramacionId = version,
        Canal = "email",
        Estado = estado,
        ProgramadoPara = ColombiaClock.Now,
        ProximoIntentoEn = estado == "pendiente" ? ColombiaClock.Now.AddMinutes(-1) : null,
        EnviadoEn = enviado,
        CreadoEn = ColombiaClock.Now
    };

    private sealed class CorreoFalso : IEmailService
    {
        public int Llamadas { get; private set; }
        public bool Fallar { get; init; }
        public bool Enviado { get; private set; }
        public bool Bloquear { get; init; }
        public TaskCompletionSource Iniciado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Liberar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendFacturaRecordatorioAsync(string recipientEmail, string nombrePaciente,
            IReadOnlyList<FacturaRecordatorioEmail> facturas, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(string correoDestino,
            CancellationToken cancellationToken = default) => Task.FromResult((true, "fake"));
        public async Task SendCitaNotificacionAsync(string recipientEmail, string nombrePaciente, DateTime fechaCita,
            string profesional, string servicio, string nuevoEstado, CancellationToken cancellationToken = default,
            string? motivoCancelacion = null, string? mensajePersonalizado = null)
        {
            Llamadas++;
            Iniciado.TrySetResult();
            if (Bloquear) await Liberar.Task.WaitAsync(cancellationToken);
            if (Fallar) throw new InvalidOperationException("Fallo SMTP simulado.");
            Enviado = true;
        }
    }

    private sealed class FallarGuardadoDespuesDelEnvio(Func<bool> envioRealizado) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (envioRealizado())
                throw new InvalidOperationException("Fallo de persistencia simulado.");
            return ValueTask.FromResult(result);
        }
    }
}
