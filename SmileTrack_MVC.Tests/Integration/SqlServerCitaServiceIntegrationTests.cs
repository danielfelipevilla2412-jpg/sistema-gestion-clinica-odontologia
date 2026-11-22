using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Exceptions;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.DTOs;
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
    public async Task ConfirmarYAsignarCitaAsync_NoConfirmaDosCitasSimultaneasConElMismoProfesional()
    {
        string connectionString =
            Environment.GetEnvironmentVariable("SMILETRACK_TEST_SQLSERVER_CONNECTION")
            ?? throw new InvalidOperationException("The SQL Server integration connection is missing.");
        EnsureLocalConnection(connectionString);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(3),
                errorNumbersToAdd: null))
            .Options;
        string suffix = Guid.NewGuid().ToString("N");
        var pacientes = new[]
        {
            new Paciente
            {
                TipoDocumento = "CC",
                Documento = $"A{suffix[..19]}",
                Nombres = $"SQLIT-A-{suffix[..8]}",
                Apellidos = "Concurrency",
                FechaNacimiento = new DateTime(1990, 1, 1),
                FechaRegistro = ColombiaClock.Today,
                Estado = "activo"
            },
            new Paciente
            {
                TipoDocumento = "CC",
                Documento = $"B{suffix[..19]}",
                Nombres = $"SQLIT-B-{suffix[..8]}",
                Apellidos = "Concurrency",
                FechaNacimiento = new DateTime(1990, 1, 1),
                FechaRegistro = ColombiaClock.Today,
                Estado = "activo"
            }
        };
        var profesional = new Profesional
        {
            Nombres = $"SQLIT-{suffix[..8]}",
            Apellidos = "Concurrency",
            RegistroMedico = $"SQLIT-{suffix}",
            Estado = "activo"
        };
        var especialidad = new Especialidad { Nombre = $"Odontología General {suffix[..8]}" };
        var servicio = new Servicio
        {
            Nombre = $"SQLIT-{suffix[..8]}",
            Precio = 1m,
            Estado = "activo",
            Categoria = "general",
            DuracionMinutos = 30
        };
        var consultorio = new Consultorio
        {
            Nombre = $"SQLIT-{suffix[..8]}",
            Estado = "disponible"
        };
        Cita[] citas = [];

        try
        {
            DateTime fechaHora = ProximoLunes().AddDays(7).AddHours(9);
            string diaSemana = fechaHora.DayOfWeek switch
            {
                DayOfWeek.Monday => "Lunes",
                DayOfWeek.Tuesday => "Martes",
                DayOfWeek.Wednesday => "Miercoles",
                DayOfWeek.Thursday => "Jueves",
                DayOfWeek.Friday => "Viernes",
                DayOfWeek.Saturday => "Sabado",
                _ => "Domingo"
            };

            await using (var db = new AppDbContext(options))
            {
                var estadoSolicitada = await db.EstadosCita
                    .AsNoTracking()
                    .SingleAsync(e => e.NombreEstado == "Solicitada");
                db.AddRange(pacientes);
                db.AddRange(profesional, especialidad);
                await db.SaveChangesAsync();

                servicio.IdEspecialidad = especialidad.IdEspecialidad;
                db.AddRange(servicio, consultorio);
                await db.SaveChangesAsync();

                db.ProfesionalServicios.Add(new ProfesionalServicio
                {
                    IdProfesional = profesional.IdProfesional,
                    IdServicio = servicio.IdServicio,
                    Activo = true
                });
                db.HorariosProfesional.Add(new HorarioProfesional
                {
                    IdProfesional = profesional.IdProfesional,
                    DiaSemana = diaSemana,
                    HoraInicio = new TimeOnly(8, 0),
                    HoraFin = new TimeOnly(18, 0),
                    Activo = true
                });
                citas = pacientes.Select(p => new Cita
                {
                    IdPaciente = p.IdPaciente,
                    IdServicio = servicio.IdServicio,
                    IdEstado = estadoSolicitada.IdEstado,
                    Estado = estadoSolicitada.NombreEstado,
                    FechaHora = fechaHora.AddDays(1),
                    FechaCreacion = ColombiaClock.Now
                }).ToArray();
                db.Citas.AddRange(citas);
                await db.SaveChangesAsync();
            }

            var errores = new System.Collections.Concurrent.ConcurrentBag<string>();
            async Task<bool> ConfirmarAsync(Cita cita)
            {
                await using var db = new AppDbContext(options);
                var service = new CitaService(db, NullLogger<CitaService>.Instance);
                try
                {
                    var result = await service.ConfirmarYAsignarCitaAsync(
                        cita.IdCita,
                        new CitaConfirmacionAsignacionDto
                        {
                            IdCita = cita.IdCita,
                            IdProfesional = profesional.IdProfesional,
                            IdConsultorio = consultorio.IdConsultorio,
                            Fecha = fechaHora.Date,
                            HoraInicio = fechaHora.TimeOfDay
                        });
                    return result is not null;
                }
                catch (CitaConflictException ex)
                {
                    errores.Add($"Cita {cita.IdCita}: {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"La confirmación concurrente de la cita {cita.IdCita} falló: {ex.Message}",
                        ex);
                }
            }

            bool[] results = await Task.WhenAll(citas.Select(ConfirmarAsync));
            Assert.True(results.Any(confirmed => confirmed),
                $"Ninguna confirmación fue aceptada. Errores: {string.Join(" | ", errores)}");

            await using (var verification = new AppDbContext(options))
            {
                Assert.Equal(1, await verification.Citas.CountAsync(c =>
                    citas.Select(x => x.IdCita).Contains(c.IdCita) && c.Estado == "Confirmada"));
                Assert.Equal(1, await verification.Citas.CountAsync(c =>
                    citas.Select(x => x.IdCita).Contains(c.IdCita) && c.Estado == "Solicitada"));
            }
        }
        finally
        {
            await CleanupConcurrencyAsync(
                options,
                citas.Select(c => (int?)c.IdCita).ToArray(),
                pacientes.Select(p => p.IdPaciente).ToArray(),
                profesional.IdProfesional,
                especialidad.IdEspecialidad,
                servicio.IdServicio,
                consultorio.IdConsultorio);
        }
    }

    [SqlServerIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task CambiarEstadoAsync_RollsBackAuditAndHistoryOnForeignKeyFailure_ThenPersistsThem()
    {
        string connectionString =
            Environment.GetEnvironmentVariable("SMILETRACK_TEST_SQLSERVER_CONNECTION")
            ?? throw new InvalidOperationException("The SQL Server integration connection is missing.");
        EnsureLocalConnection(connectionString);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(3),
                errorNumbersToAdd: null))
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
                    n => n.Tipo == "cambio_estado" && n.Estado == "enviada");
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

    private static DateTime ProximoLunes()
    {
        DateTime fecha = ColombiaClock.Today.AddDays(1);
        while (fecha.DayOfWeek != DayOfWeek.Monday)
            fecha = fecha.AddDays(1);
        return fecha;
    }

    private static async Task CleanupConcurrencyAsync(
        DbContextOptions<AppDbContext> options,
        int?[] citaIds,
        int[] pacienteIds,
        int profesionalId,
        int especialidadId,
        int servicioId,
        int consultorioId)
    {
        await using var db = new AppDbContext(options);
        var existingCitaIds = citaIds.Where(id => id.HasValue).Select(id => id!.Value).ToArray();
        if (existingCitaIds.Length > 0)
        {
            db.Notificaciones.RemoveRange(await db.Notificaciones.Where(n =>
                n.IdCita.HasValue && existingCitaIds.Contains(n.IdCita.Value)).ToListAsync());
            db.CitasHistorialEstado.RemoveRange(await db.CitasHistorialEstado.Where(h =>
                existingCitaIds.Contains(h.IdCita)).ToListAsync());
            db.Auditorias.RemoveRange(await db.Auditorias.Where(a =>
                a.TablaAfectada == "Cita" && a.IdRegistro.HasValue &&
                existingCitaIds.Contains(a.IdRegistro.Value)).ToListAsync());
            db.Citas.RemoveRange(await db.Citas.Where(c => existingCitaIds.Contains(c.IdCita)).ToListAsync());
        }

        db.HorariosProfesional.RemoveRange(await db.HorariosProfesional
            .Where(h => h.IdProfesional == profesionalId).ToListAsync());
        db.ProfesionalServicios.RemoveRange(await db.ProfesionalServicios
            .Where(ps => ps.IdProfesional == profesionalId).ToListAsync());
        db.ProfesionalEspecialidades.RemoveRange(await db.ProfesionalEspecialidades
            .Where(pe => pe.IdProfesional == profesionalId).ToListAsync());
        var profesional = await db.Profesionales.FindAsync(profesionalId);
        if (profesional is not null) db.Profesionales.Remove(profesional);
        var servicio = await db.Servicios.FindAsync(servicioId);
        if (servicio is not null) db.Servicios.Remove(servicio);
        var especialidad = await db.Especialidades.FindAsync(especialidadId);
        if (especialidad is not null) db.Especialidades.Remove(especialidad);
        var consultorio = await db.Consultorios.FindAsync(consultorioId);
        if (consultorio is not null) db.Consultorios.Remove(consultorio);
        db.Pacientes.RemoveRange(await db.Pacientes.Where(p => pacienteIds.Contains(p.IdPaciente)).ToListAsync());
        await db.SaveChangesAsync();
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
