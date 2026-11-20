using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.GestionDeCitas;
using Xunit;

namespace SmileTrack_MVC.Tests.Integration;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerIntegrationFactAttribute : FactAttribute
{
    public SqlServerIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("SMILETRACK_TEST_SQLSERVER_CONNECTION")) ||
            Environment.GetEnvironmentVariable("SMILETRACK_ALLOW_SQLSERVER_INTEGRATION_TESTS") != "1")
        {
            Skip = "Requires explicit opt-in and a disposable local SQL Server database.";
        }
    }
}

public sealed class SqlServerCitaServiceIntegrationTests
{
    [SqlServerIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task CambiarEstadoAsync_RollsBackAuditAndHistoryOnForeignKeyFailure_ThenPersistsThem()
    {
        string connectionString =
            Environment.GetEnvironmentVariable("SMILETRACK_TEST_SQLSERVER_CONNECTION")
            ?? throw new InvalidOperationException("The SQL Server integration connection is missing.");
        EnsureLocalConnection(connectionString);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        string suffix = Guid.NewGuid().ToString("N");
        string auditMarker = $"SqlIT-{suffix}";
        var paciente = new Paciente
        {
            TipoDocumento = "CC",
            Documento = $"T{suffix[..19]}",
            Nombres = $"SQLIT-{suffix[..12]}",
            Apellidos = suffix[..8],
            FechaNacimiento = new DateTime(1990, 1, 1),
            FechaRegistro = ColombiaClock.Today,
            Estado = "activo"
        };
        var profesional = new Profesional
        {
            Nombres = $"SQLIT-{suffix[..12]}",
            Apellidos = suffix[..8],
            RegistroMedico = $"SQLIT-{suffix}",
            Estado = "activo"
        };
        var servicio = new Servicio
        {
            Nombre = $"SQLIT-{suffix[..12]}",
            Precio = 1m,
            Estado = "activo",
            Categoria = "general",
            DuracionMinutos = 30
        };
        var consultorio = new Consultorio
        {
            Nombre = $"SQLIT-{suffix[..12]}",
            Estado = "disponible"
        };
        Cita? cita = null;
        int initialHistoryCount = 0;
        int initialAuditCount = 0;

        try
        {
            int idEstadoConfirmada;
            await using (var db = new AppDbContext(options))
            {
                var estadoConfirmada = await db.EstadosCita
                    .AsNoTracking()
                    .SingleOrDefaultAsync(e => e.NombreEstado == "Confirmada");
                Assert.NotNull(estadoConfirmada);
                idEstadoConfirmada = estadoConfirmada.IdEstado;

                db.AddRange(paciente, profesional, servicio, consultorio);
                await db.SaveChangesAsync();

                cita = new Cita
                {
                    IdPaciente = paciente.IdPaciente,
                    IdProfesional = profesional.IdProfesional,
                    IdServicio = servicio.IdServicio,
                    IdConsultorio = consultorio.IdConsultorio,
                    FechaHora = ColombiaClock.Now.AddDays(10),
                    Estado = "Programada",
                    DuracionMinutos = 30,
                    FechaCreacion = ColombiaClock.Now
                };
                db.Citas.Add(cita);
                await db.SaveChangesAsync();
            }

            await using (var verification = new AppDbContext(options))
            {
                initialHistoryCount = await verification.CitasHistorialEstado
                    .CountAsync(h => h.IdCita == cita.IdCita);
                initialAuditCount = await verification.Auditorias
                    .CountAsync(a => a.TablaAfectada == "Cita" &&
                                     a.IdRegistro == cita.IdCita &&
                                     a.IpOrigen == auditMarker);
            }

            await using (var db = new AppDbContext(options))
            {
                var service = new CitaService(db, NullLogger<CitaService>.Instance);
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    service.CambiarEstadoAsync(
                        cita.IdCita,
                        "Confirmada",
                        idUsuarioActor: int.MaxValue,
                        ipOrigen: auditMarker));
            }

            await using (var verification = new AppDbContext(options))
            {
                var unchanged = await verification.Citas
                    .AsNoTracking()
                    .SingleAsync(c => c.IdCita == cita.IdCita);
                Assert.Equal("Programada", unchanged.Estado);
                var historyAfterFailure = await verification.CitasHistorialEstado
                    .Where(h => h.IdCita == cita.IdCita)
                    .ToListAsync();
                Assert.Equal(initialHistoryCount, historyAfterFailure.Count);
                Assert.DoesNotContain(historyAfterFailure, h => h.EstadoTexto == "Confirmada");
                Assert.Equal(initialAuditCount, await verification.Auditorias
                    .Where(a => a.TablaAfectada == "Cita" &&
                                a.IdRegistro == cita.IdCita &&
                                a.IpOrigen == auditMarker)
                    .CountAsync());
            }

            await using (var db = new AppDbContext(options))
            {
                var service = new CitaService(db, NullLogger<CitaService>.Instance);
                var result = await service.CambiarEstadoAsync(
                    cita.IdCita,
                    "Confirmada",
                    ipOrigen: auditMarker);
                Assert.NotNull(result);
            }

            await using (var verification = new AppDbContext(options))
            {
                var updated = await verification.Citas
                    .AsNoTracking()
                    .SingleAsync(c => c.IdCita == cita.IdCita);
                Assert.Equal("Confirmada", updated.Estado);
                Assert.Equal(idEstadoConfirmada, updated.IdEstado);
                Assert.Contains(
                    await verification.CitasHistorialEstado
                        .Where(h => h.IdCita == cita.IdCita)
                        .ToListAsync(),
                    h => h.EstadoTexto == "Confirmada");
                Assert.Contains(
                    await verification.Auditorias
                        .Where(a => a.TablaAfectada == "Cita" &&
                                    a.IdRegistro == cita.IdCita &&
                                    a.IpOrigen == auditMarker)
                        .ToListAsync(),
                    a => a.Accion == "UPDATE");
                Assert.Contains(
                    await verification.Notificaciones
                        .Where(n => n.IdCita == cita.IdCita)
                        .ToListAsync(),
                    n => n.Tipo == "cambio_estado" && n.Estado == "pendiente");
            }
        }
        finally
        {
            await CleanupAsync(
                options,
                cita?.IdCita,
                paciente.IdPaciente,
                profesional.IdProfesional,
                servicio.IdServicio,
                consultorio.IdConsultorio);
        }
    }

    private static void EnsureLocalConnection(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        string server = builder.DataSource
            .Replace("tcp:", string.Empty, StringComparison.OrdinalIgnoreCase);
        int comma = server.IndexOf(',');
        if (comma >= 0)
            server = server[..comma];
        int instanceSeparator = server.IndexOf('\\');
        if (instanceSeparator >= 0)
            server = server[..instanceSeparator];

        bool isLocal = server is "." or "(local)" or "localhost" or "127.0.0.1" ||
                       string.Equals(server, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        if (!isLocal)
            throw new InvalidOperationException("SQL Server integration tests only accept a local database.");
    }

    private static async Task CleanupAsync(
        DbContextOptions<AppDbContext> options,
        int? idCita,
        int idPaciente,
        int idProfesional,
        int idServicio,
        int idConsultorio)
    {
        await using var db = new AppDbContext(options);
        if (idCita is > 0)
        {
            int citaId = idCita.Value;
            db.Notificaciones.RemoveRange(
                await db.Notificaciones.Where(n => n.IdCita == citaId).ToListAsync());
            db.CitasHistorialEstado.RemoveRange(
                await db.CitasHistorialEstado.Where(h => h.IdCita == citaId).ToListAsync());
            db.Auditorias.RemoveRange(
                await db.Auditorias
                    .Where(a => a.TablaAfectada == "Cita" && a.IdRegistro == citaId)
                    .ToListAsync());

            var cita = await db.Citas.FindAsync(citaId);
            if (cita is not null)
                db.Citas.Remove(cita);
        }

        if (idConsultorio > 0)
        {
            var consultorio = await db.Consultorios.FindAsync(idConsultorio);
            if (consultorio is not null)
                db.Consultorios.Remove(consultorio);
        }

        if (idServicio > 0)
        {
            var servicio = await db.Servicios.FindAsync(idServicio);
            if (servicio is not null)
                db.Servicios.Remove(servicio);
        }

        if (idProfesional > 0)
        {
            var profesional = await db.Profesionales.FindAsync(idProfesional);
            if (profesional is not null)
                db.Profesionales.Remove(profesional);
        }

        if (idPaciente > 0)
        {
            var paciente = await db.Pacientes.FindAsync(idPaciente);
            if (paciente is not null)
                db.Pacientes.Remove(paciente);
        }

        await db.SaveChangesAsync();
    }
}
