using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.Email;
using SmileTrack_MVC.Services.GestionDeCitas;
using Xunit;

namespace SmileTrack_MVC.Tests.Integration;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerDisposableIntegrationFactAttribute : FactAttribute
{
    public SqlServerDisposableIntegrationFactAttribute()
    {
        string? connection = Environment.GetEnvironmentVariable("SMILETRACK_TEST_SQLSERVER_CONNECTION");
        string optIn = Environment.GetEnvironmentVariable("SMILETRACK_ALLOW_SQLSERVER_INTEGRATION_TESTS") ?? "";
        bool disposableName = false;
        if (!string.IsNullOrWhiteSpace(connection))
        {
            var builder = new SqlConnectionStringBuilder(connection);
            disposableName = builder.InitialCatalog.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                             builder.InitialCatalog.Contains("disposable", StringComparison.OrdinalIgnoreCase);
        }

        if (optIn != "1" || !disposableName)
            Skip = "Requires explicit opt-in and a SQL Server database whose name contains Test or Disposable.";
    }
}

public sealed class SqlServerRecordatorioCitaIntegrationTests
{
    [SqlServerDisposableIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task ProcesarPendientesAsync_DosInstancias_SQLServerSoloReclamaUnaVez()
    {
        string connection = Environment.GetEnvironmentVariable("SMILETRACK_TEST_SQLSERVER_CONNECTION")!;
        var cs = new SqlConnectionStringBuilder(connection);
        string host = cs.DataSource.Replace("tcp:", "", StringComparison.OrdinalIgnoreCase);
        int comma = host.IndexOf(',');
        if (comma >= 0) host = host[..comma];
        int instanceSeparator = host.IndexOf('\\');
        if (instanceSeparator >= 0) host = host[..instanceSeparator];
        if (host is not ("localhost" or "127.0.0.1" or "." or "(local)") &&
            !string.Equals(host, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The opt-in SQL Server target must be local.");

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
        string suffix = Guid.NewGuid().ToString("N");
        var paciente = new Paciente
        {
            TipoDocumento = "CC", Documento = $"T{suffix[..19]}", Nombres = "Synthetic",
            Apellidos = "Reminder Test", FechaNacimiento = new DateTime(1990, 1, 1),
            FechaRegistro = ColombiaClock.Today, Correo = $"cit02-{suffix[..8]}@example.invalid", Estado = "activo"
        };
        Cita? cita = null;
        try
        {
            await using (var setup = new AppDbContext(options))
            {
                setup.Pacientes.Add(paciente);
                await setup.SaveChangesAsync();
                cita = new Cita
                {
                    IdPaciente = paciente.IdPaciente,
                    FechaHora = ColombiaClock.Now.AddHours(12),
                    Estado = "Programada",
                    DuracionMinutos = 30
                };
                setup.Citas.Add(cita);
                await setup.SaveChangesAsync();
                setup.RecordatoriosCita.Add(new RecordatorioCita
                {
                    IdCita = cita.IdCita,
                    ProgramacionId = cita.ProgramacionId,
                    Canal = "email",
                    Estado = "pendiente",
                    ProgramadoPara = ColombiaClock.Now.AddMinutes(-1),
                    ProximoIntentoEn = ColombiaClock.Now.AddMinutes(-1),
                    CreadoEn = ColombiaClock.Now
                });
                await setup.SaveChangesAsync();
            }

            var email = new EmailFalso();
            async Task ProcesarInstanciaAsync()
            {
                await using var db = new AppDbContext(options);
                await new RecordatorioCitaService(db, email,
                    NullLogger<RecordatorioCitaService>.Instance).ProcesarPendientesAsync();
            }

            await Task.WhenAll(ProcesarInstanciaAsync(), ProcesarInstanciaAsync());
            Assert.Equal(1, email.Llamadas);

            await using var verification = new AppDbContext(options);
            var stored = await verification.RecordatoriosCita.SingleAsync(r => r.IdCita == cita.IdCita);
            Assert.Equal("enviado", stored.Estado);
            Assert.Equal(1, stored.Intentos);
            Assert.Null(stored.ReservaId);
        }
        finally
        {
            if (paciente.IdPaciente > 0)
            {
                await using var cleanup = new AppDbContext(options);
                var ids = await cleanup.Citas.AsNoTracking().Where(c => c.IdPaciente == paciente.IdPaciente)
                    .Select(c => c.IdCita).ToArrayAsync();
                cleanup.RecordatoriosCita.RemoveRange(await cleanup.RecordatoriosCita.Where(r => ids.Contains(r.IdCita)).ToListAsync());
                cleanup.Auditorias.RemoveRange(await cleanup.Auditorias.Where(a => ids.Contains(a.IdRegistro ?? 0)).ToListAsync());
                cleanup.Citas.RemoveRange(await cleanup.Citas.Where(c => c.IdPaciente == paciente.IdPaciente).ToListAsync());
                cleanup.Pacientes.RemoveRange(await cleanup.Pacientes.Where(p => p.IdPaciente == paciente.IdPaciente).ToListAsync());
                await cleanup.SaveChangesAsync();
            }
        }
    }

    private sealed class EmailFalso : IEmailService
    {
        private int _llamadas;
        public int Llamadas => Volatile.Read(ref _llamadas);
        public Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendFacturaRecordatorioAsync(string recipientEmail, string nombrePaciente,
            IReadOnlyList<FacturaRecordatorioEmail> facturas, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(string correoDestino,
            CancellationToken cancellationToken = default) => Task.FromResult((true, "fake"));
        public Task SendCitaNotificacionAsync(string recipientEmail, string nombrePaciente, DateTime fechaCita,
            string profesional, string servicio, string nuevoEstado, CancellationToken cancellationToken = default,
            string? motivoCancelacion = null, string? mensajePersonalizado = null)
        {
            Interlocked.Increment(ref _llamadas);
            return Task.CompletedTask;
        }
    }
}
