using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.Email;
using SmileTrack_MVC.Services.GestionDeCitas;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class RecordatoriosCitasBackgroundServiceTests
{
    [Fact]
    public async Task ProcesarAsync_EnviaRecordatoriosProgramadosDentroDe24Horas()
    {
        string dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var db = new AppDbContext(options))
        {
            db.Pacientes.Add(new Paciente
            {
                IdPaciente = 1,
                Nombres = "Paciente",
                Apellidos = "Prueba",
                Correo = "paciente@example.test",
                Estado = "activo"
            });
            db.Profesionales.Add(new Profesional
            {
                IdProfesional = 1,
                Nombres = "Profesional",
                Apellidos = "Prueba",
                Estado = "activo",
                RegistroMedico = "RM-RECORDATORIOS"
            });
            db.Servicios.Add(new Servicio
            {
                IdServicio = 1,
                Nombre = "Consulta",
                Estado = "activo",
                DuracionMinutos = 60
            });
            db.Citas.AddRange(
                Enumerable.Range(1, 100).Select(index => new Cita
                {
                    IdCita = index,
                    IdPaciente = 1,
                    IdProfesional = 1,
                    IdServicio = 1,
                    FechaHora = DateTime.Now.AddHours(1 + (index % 20)),
                    DuracionMinutos = 60,
                    Estado = "Programada"
                }));
            await db.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(builder => builder.UseInMemoryDatabase(dbName));
        services.AddSingleton<IEmailService, CapturingEmailService>();
        services.AddScoped<IRecordatorioCitaService, RecordatorioCitaService>();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        var servicio = new RecordatoriosCitasBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RecordatoriosCitasBackgroundService>.Instance);

        var metodo = typeof(RecordatoriosCitasBackgroundService).GetMethod(
            "ProcesarAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(metodo);
        await (Task)metodo!.Invoke(servicio, new object[] { CancellationToken.None })!;

        await using var verificacion = new AppDbContext(options);
        Assert.Equal(100, await verificacion.RecordatoriosCita.CountAsync());
        Assert.Equal(100, await verificacion.RecordatoriosCita.CountAsync(r => r.Estado == "enviado"));
    }

    [Fact]
    public async Task ProcesarAsync_NoDuplicaRecordatorioYaEnviado()
    {
        string dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var db = new AppDbContext(options))
        {
            db.Pacientes.Add(new Paciente
            {
                IdPaciente = 1,
                Nombres = "Paciente",
                Estado = "activo",
                Correo = "paciente@example.test"
            });
            db.Profesionales.Add(new Profesional
            {
                IdProfesional = 1,
                Nombres = "Profesional",
                Estado = "activo",
                RegistroMedico = "RM-UNICO"
            });
            db.Citas.Add(new Cita
            {
                IdCita = 1,
                IdPaciente = 1,
                IdProfesional = 1,
                FechaHora = DateTime.Now.AddHours(12),
                Estado = "Programada"
            });
            await db.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(builder => builder.UseInMemoryDatabase(dbName));
        services.AddSingleton<IEmailService, CapturingEmailService>();
        services.AddScoped<IRecordatorioCitaService, RecordatorioCitaService>();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        var servicio = new RecordatoriosCitasBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RecordatoriosCitasBackgroundService>.Instance);
        var metodo = typeof(RecordatoriosCitasBackgroundService).GetMethod(
            "ProcesarAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(metodo);

        await (Task)metodo!.Invoke(servicio, new object[] { CancellationToken.None })!;
        await (Task)metodo.Invoke(servicio, new object[] { CancellationToken.None })!;

        await using var verificacion = new AppDbContext(options);
        Assert.Single(await verificacion.RecordatoriosCita.ToListAsync());
        Assert.Equal("enviado", (await verificacion.RecordatoriosCita.SingleAsync()).Estado);
    }

    private sealed class CapturingEmailService : IEmailService
    {
        public Task SendRecoveryCodeAsync(
            string recipientEmail,
            string code,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendCitaNotificacionAsync(
            string recipientEmail,
            string nombrePaciente,
            DateTime fechaCita,
            string profesional,
            string servicio,
            string nuevoEstado,
            CancellationToken cancellationToken = default,
            string? motivoCancelacion = null,
            string? mensajePersonalizado = null) =>
            Task.CompletedTask;

        public Task SendFacturaRecordatorioAsync(
            string recipientEmail,
            string nombrePaciente,
            IReadOnlyList<FacturaRecordatorioEmail> facturas,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(
            string correoDestino,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((true, "OK"));
    }
}
