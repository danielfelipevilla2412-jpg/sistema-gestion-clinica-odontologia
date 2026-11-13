using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ProfesionalServiceTests
{
    [Fact]
    public async Task CrearAusenciaAsync_RechazaRangosSolapadosDelMismoProfesional()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-AUSENCIA-OVERLAP"
        };
        db.Profesionales.Add(profesional);
        db.AusenciasProfesional.Add(new AusenciaProfesional
        {
            IdProfesional = profesional.IdProfesional,
            Tipo = "vacaciones",
            FechaInicio = new DateOnly(2026, 12, 1),
            FechaFin = new DateOnly(2026, 12, 15)
        });
        await db.SaveChangesAsync();

        var result = await new ProfesionalService(db, NullLogger<ProfesionalService>.Instance)
            .CrearAusenciaAsync(profesional.IdProfesional, new AusenciaProfesionalApiRequest
            {
                Tipo = "permiso",
                FechaInicio = new DateOnly(2026, 12, 10),
                FechaFin = new DateOnly(2026, 12, 20)
            }, operadorId: 5);

        Assert.False(result.Success);
        Assert.Equal(409, result.ErrorStatusCode);
        Assert.Equal(1, await db.AusenciasProfesional.CountAsync());
    }

    [Fact]
    public async Task ActualizarAusenciaAsync_NoConsideraSolapamientoConsigoMisma()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-AUSENCIA-UPDATE"
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();
        var ausencia = new AusenciaProfesional
        {
            IdProfesional = profesional.IdProfesional,
            Tipo = "vacaciones",
            FechaInicio = new DateOnly(2026, 12, 1),
            FechaFin = new DateOnly(2026, 12, 15)
        };
        db.AusenciasProfesional.Add(ausencia);
        await db.SaveChangesAsync();

        var result = await new ProfesionalService(db, NullLogger<ProfesionalService>.Instance)
            .ActualizarAusenciaAsync(profesional.IdProfesional, ausencia.IdAusencia,
                new AusenciaProfesionalApiRequest
                {
                    Tipo = "vacaciones",
                    FechaInicio = ausencia.FechaInicio,
                    FechaFin = ausencia.FechaFin
                }, operadorId: 5);

        Assert.True(result.Success, result.Message);
        Assert.Single(await db.AusenciasProfesional.ToListAsync());
    }

    [Fact]
    public async Task ActualizarAusenciaAsync_AusenciaInexistenteRetorna404AntesDeValidarSolapamiento()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-AUSENCIA-MISSING"
        };
        db.Profesionales.Add(profesional);
        db.AusenciasProfesional.Add(new AusenciaProfesional
        {
            IdProfesional = profesional.IdProfesional,
            Tipo = "vacaciones",
            FechaInicio = new DateOnly(2026, 12, 1),
            FechaFin = new DateOnly(2026, 12, 15)
        });
        await db.SaveChangesAsync();

        var result = await new ProfesionalService(db, NullLogger<ProfesionalService>.Instance)
            .ActualizarAusenciaAsync(profesional.IdProfesional, 999,
                new AusenciaProfesionalApiRequest
                {
                    Tipo = "permiso",
                    FechaInicio = new DateOnly(2026, 12, 10),
                    FechaFin = new DateOnly(2026, 12, 20)
                }, operadorId: 5);

        Assert.False(result.Success);
        Assert.Equal(404, result.ErrorStatusCode);
        Assert.Single(await db.AusenciasProfesional.ToListAsync());
    }

    [Fact]
    public async Task RegistrarAusenciaConReasignacionAsync_RechazaRangoSolapadoAntesDePersistir()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-AUSENCIA-REASSIGN"
        };
        db.Profesionales.Add(profesional);
        db.AusenciasProfesional.Add(new AusenciaProfesional
        {
            IdProfesional = profesional.IdProfesional,
            Tipo = "vacaciones",
            FechaInicio = new DateOnly(2026, 12, 1),
            FechaFin = new DateOnly(2026, 12, 15)
        });
        await db.SaveChangesAsync();
        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegistrarAusenciaConReasignacionAsync(
                profesional.IdProfesional,
                new DateTime(2026, 12, 10),
                new DateTime(2026, 12, 20),
                "permiso"));

        Assert.Contains("solapa", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await db.AusenciasProfesional.ToListAsync());
    }

    [Fact]
    public async Task ActualizarHorariosAsync_NormalizaDiasConAcentoParaLaBaseDeDatos()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-ACCENT",
            IdUsuario = 42
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var result = await service.ActualizarHorariosAsync(
            profesional.IdProfesional,
            new[]
            {
                new HorarioSemanalApiRequest
                {
                    DayFull = "Miércoles",
                    Active = true,
                    Start = "08:00",
                    End = "12:00"
                },
                new HorarioSemanalApiRequest
                {
                    DayFull = "Sábado",
                    Active = true,
                    Start = "09:00",
                    End = "13:00"
                }
            },
            usuarioActualId: 42,
            esAdministrador: false);

        Assert.True(result.Success);
        Assert.Equal(
            new[] { "Miercoles", "Sabado" },
            await db.HorariosProfesional
                .OrderBy(h => h.DiaSemana)
                .Select(h => h.DiaSemana)
                .ToArrayAsync());
    }

    [Fact]
    public async Task ActualizarHorariosAsync_RechazaBloquesSolapadosEnElMismoDia()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-OVERLAP"
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var result = await service.ActualizarHorariosAsync(
            profesional.IdProfesional,
            new[]
            {
                new HorarioSemanalApiRequest { DiaSemana = "Lunes", Active = true, Start = "08:00", End = "12:00" },
                    new HorarioSemanalApiRequest { DiaSemana = "lunes", Active = true, Start = "11:00", End = "15:00" }
            },
            usuarioActualId: null,
            esAdministrador: true);

        Assert.False(result.Success);
        Assert.Equal(422, result.ErrorStatusCode);
        Assert.Contains("solapan", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.HorariosProfesional.ToListAsync());
    }

    [Fact]
    public async Task ActualizarHorariosAsync_RechazaCambiosQueDejanCitasFuturasFueraDeHorario()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-SCHEDULE-WITH-APPOINTMENT"
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();

        DateTime lunes = ColombiaClock.Today.AddDays(1);
        while (lunes.DayOfWeek != DayOfWeek.Monday)
            lunes = lunes.AddDays(1);

        db.HorariosProfesional.Add(new HorarioProfesional
        {
            IdProfesional = profesional.IdProfesional,
            DiaSemana = "Lunes",
            HoraInicio = new TimeOnly(8, 0),
            HoraFin = new TimeOnly(18, 0),
            Activo = true
        });
        db.Citas.Add(new Cita
        {
            IdPaciente = 1,
            IdProfesional = profesional.IdProfesional,
            FechaHora = lunes.AddHours(16),
            DuracionMinutos = 60,
            Estado = "Programada"
        });
        await db.SaveChangesAsync();

        var result = await new ProfesionalService(db, NullLogger<ProfesionalService>.Instance)
            .ActualizarHorariosAsync(profesional.IdProfesional,
                [new HorarioSemanalApiRequest
                {
                    DiaSemana = "Lunes",
                    Active = true,
                    Start = "08:00",
                    End = "12:00"
                }],
                usuarioActualId: null,
                esAdministrador: true);

        Assert.False(result.Success);
        Assert.Equal(409, result.ErrorStatusCode);
        var persistedSchedule = await db.HorariosProfesional.SingleAsync();
        Assert.Equal(new TimeOnly(18, 0), persistedSchedule.HoraFin);
    }

    [Fact]
    public async Task ActualizarHorariosAsync_AceptaBloquesConsecutivos()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-CONSECUTIVE"
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var result = await service.ActualizarHorariosAsync(
            profesional.IdProfesional,
            new[]
            {
                new HorarioSemanalApiRequest { DiaSemana = "Lunes", Active = true, Start = "08:00", End = "12:00" },
                new HorarioSemanalApiRequest { DiaSemana = "Lunes", Active = true, Start = "12:00", End = "16:00" }
            },
            usuarioActualId: null,
            esAdministrador: true);

        Assert.True(result.Success);
        Assert.Equal(2, await db.HorariosProfesional.CountAsync());
    }

    [Fact]
    public async Task ActualizarHorariosAsync_PersisteTodosLosBloquesDeUnDia()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-MULTI-BLOCKS",
            IdUsuario = 42
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var updated = await service.ActualizarHorariosAsync(
            profesional.IdProfesional,
            new[]
            {
                new HorarioSemanalApiRequest { DayFull = "Lunes", Active = true, Start = "08:00", End = "12:00" },
                new HorarioSemanalApiRequest { DayFull = "Lunes", Active = true, Start = "13:00", End = "17:00" },
                new HorarioSemanalApiRequest { DayFull = "Martes", Active = true, Start = "09:00", End = "13:00" }
            },
            usuarioActualId: 42,
            esAdministrador: false);

        Assert.True(updated.Success, updated.Message);
        var loaded = await service.ObtenerHorariosAsync(
            profesional.IdProfesional,
            usuarioActualId: 42,
            esAdministrador: false);

        Assert.True(loaded.Success);
        Assert.Equal(
            new[] { ("Lunes", "08:00", "12:00"), ("Lunes", "13:00", "17:00"), ("Martes", "09:00", "13:00") },
            loaded.Data.Select(block => (block.DiaSemana, block.HoraInicio, block.HoraFin)));
    }

    [Fact]
    public async Task ObtenerHorariosAsync_RechazaHorarioDeOtroProfesional()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-OWNER",
            IdUsuario = 42
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var result = await service.ObtenerHorariosAsync(
            profesional.IdProfesional,
            usuarioActualId: 99,
            esAdministrador: false);

        Assert.False(result.Success);
        Assert.Equal(403, result.ErrorStatusCode);
    }

    [Fact]
    public async Task ObtenerCitasAfectadasPorAusenciaAsync_IncluyeCitasQueCruzanMedianocheYExcluyeCanceladas()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "activo",
            RegistroMedico = "RM-AUSENCIA",
            IdUsuario = 42
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();

        var fechaInicio = new DateOnly(2025, 1, 2);
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            Documento = "DOC-AUSENCIA",
            Nombres = "Paciente",
            Apellidos = "Prueba",
            FechaNacimiento = new DateTime(1990, 1, 1)
        });
        db.Citas.AddRange(
            new Cita
            {
                IdPaciente = 1,
                IdProfesional = profesional.IdProfesional,
                FechaHora = new DateTime(2025, 1, 1, 23, 30, 0),
                DuracionMinutos = 60,
                Estado = "programada"
            },
            new Cita
            {
                IdPaciente = 1,
                IdProfesional = profesional.IdProfesional,
                FechaHora = new DateTime(2025, 1, 1, 23, 0, 0),
                DuracionMinutos = 60,
                Estado = "programada"
            },
            new Cita
            {
                IdPaciente = 1,
                IdProfesional = profesional.IdProfesional,
                FechaHora = new DateTime(2025, 1, 2, 9, 0, 0),
                DuracionMinutos = 30,
                Estado = "cancelada"
            },
            new Cita
            {
                IdPaciente = 1,
                IdProfesional = profesional.IdProfesional,
                FechaHora = new DateTime(2025, 1, 2, 9, 0, 0),
                DuracionMinutos = 30,
                Estado = "Rechazada"
            });
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var result = await service.ObtenerCitasAfectadasPorAusenciaAsync(
            profesional.IdProfesional, fechaInicio, fechaInicio);

        Assert.True(result.Success);
        var cita = Assert.Single(result.Data);
        Assert.Equal(new DateTime(2025, 1, 1, 23, 30, 0), cita.FechaHora);
    }

    [Fact]
    public async Task ObtenerServiciosAsync_AplicaEspecialidadesYExcepcionesProfesionales()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesionalOrtodoncia = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Ortodoncia",
            Estado = "activo",
            RegistroMedico = "RM-ORTO"
        };
        var profesionalEndodoncia = new Profesional
        {
            Nombres = "Luis",
            Apellidos = "Endodoncia",
            Estado = "activo",
            RegistroMedico = "RM-ENDO"
        };
        var ortodoncia = new Especialidad { Nombre = "Ortodoncia" };
        var endodoncia = new Especialidad { Nombre = "Endodoncia" };
        db.Profesionales.AddRange(profesionalOrtodoncia, profesionalEndodoncia);
        db.Especialidades.AddRange(ortodoncia, endodoncia);
        await db.SaveChangesAsync();

        var servicioGeneral = new Servicio { Nombre = "Valoración", Estado = "activo", IdEspecialidad = null };
        var servicioOrtodoncia = new Servicio
        {
            Nombre = "Ajuste de brackets",
            Estado = "activo",
            IdEspecialidad = ortodoncia.IdEspecialidad
        };
        var servicioEndodoncia = new Servicio
        {
            Nombre = "Endodoncia",
            Estado = "activo",
            IdEspecialidad = endodoncia.IdEspecialidad
        };
        db.Servicios.AddRange(servicioGeneral, servicioOrtodoncia, servicioEndodoncia);
        await db.SaveChangesAsync();

        db.ProfesionalEspecialidades.AddRange(
            new Profesional_Especialidad
            {
                IdProfesional = profesionalOrtodoncia.IdProfesional,
                IdEspecialidad = ortodoncia.IdEspecialidad,
                Principal = true
            },
            new Profesional_Especialidad
            {
                IdProfesional = profesionalEndodoncia.IdProfesional,
                IdEspecialidad = endodoncia.IdEspecialidad,
                Principal = true
            });
        db.ProfesionalServicios.Add(new ProfesionalServicio
        {
            IdProfesional = profesionalOrtodoncia.IdProfesional,
            IdServicio = servicioGeneral.IdServicio,
            Activo = false
        });
        db.ProfesionalServicios.Add(new ProfesionalServicio
        {
            IdProfesional = profesionalOrtodoncia.IdProfesional,
            IdServicio = servicioOrtodoncia.IdServicio,
            PrecioPersonalizado = 125000m,
            Activo = true
        });
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var serviciosOrtodoncia = await service.ObtenerServiciosAsync(profesionalOrtodoncia.IdProfesional);
        var serviciosEndodoncia = await service.ObtenerServiciosAsync(profesionalEndodoncia.IdProfesional);

        Assert.True(serviciosOrtodoncia.Success);
        Assert.Equal("Ajuste de brackets", Assert.Single(serviciosOrtodoncia.Data).NombreServicio);
        Assert.Equal(125000m, serviciosOrtodoncia.Data[0].PrecioEfectivo);
        Assert.True(serviciosEndodoncia.Success);
        Assert.Equal(
            new[] { "Endodoncia", "Valoración" },
            serviciosEndodoncia.Data.Select(s => s.NombreServicio).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task ObtenerPorIdAsync_UsaNombresDeLaCuentaVinculada()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.Add(new Rol { IdRol = 1, NombreRol = "Profesional" });
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 42,
            Nombre = "Nombre correcto",
            Apellidos = "Apellido correcto",
            Correo = "profesional@test.local",
            Contrasena = "hash",
            IdRol = 1,
            Estado = "activo"
        });
        var profesional = new Profesional
        {
            IdUsuario = 42,
            Nombres = "Nombre desactualizado",
            Apellidos = "Apellido desactualizado",
            Estado = "activo",
            RegistroMedico = "RM-NAME"
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var result = await service.ObtenerPorIdAsync(profesional.IdProfesional);

        Assert.NotNull(result);
        Assert.Equal("Nombre correcto", result.Nombres);
        Assert.Equal("Apellido correcto", result.Apellidos);
    }

    [Fact]
    public async Task CrearAsync_GeneraContrasenaAutomaticaCuandoNoSeEnvioUna()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Roles.Add(new Rol { NombreRol = "Profesional" });
        db.Especialidades.Add(new Especialidad { Nombre = "Odontología General" });
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var result = await service.CrearAsync(
            new ProfesionalApiRequest
            {
                Nombres = "María",
                Apellidos = "García",
                RegistroMedico = "RM-GEN-001",
                CorreoAcceso = "maria.garcia@smiletrack.test",
                ContrasenaAcceso = null
            },
            operadorId: 1,
            ipOrigen: "127.0.0.1");

        Assert.True(result.Success, result.Message);
        Assert.Contains("generada", result.Message, StringComparison.OrdinalIgnoreCase);
        var usuario = await db.Usuarios.SingleAsync();
        Assert.False(string.IsNullOrWhiteSpace(usuario.Contrasena));
        Assert.NotEqual(string.Empty, usuario.Contrasena);
        Assert.Equal("activo", usuario.Estado);
        var profesionalCreado = await db.Profesionales.SingleAsync();
        Assert.Equal("inactivo", profesionalCreado.Estado);
        Assert.False(profesionalCreado.CredencialesAprobadas);
    }

    [Fact]
    public async Task CrearAsync_RechazaCategoriaFueraDelCatalogo()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);

        var result = await service.CrearAsync(
            new ProfesionalApiRequest
            {
                Nombres = "María",
                Apellidos = "García",
                RegistroMedico = "RM-CAT-001",
                Categoria = "Categoría inventada",
                CorreoAcceso = "maria.categoria@smiletrack.test"
            },
            operadorId: 1,
            ipOrigen: "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(422, result.ErrorStatusCode);
        Assert.Empty(await db.Profesionales.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_AsignaVariasEspecialidadesSeleccionadas()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.Add(new Rol { NombreRol = "Profesional" });
        db.Especialidades.AddRange(
            new Especialidad { IdEspecialidad = 1, Nombre = "Ortodoncia" },
            new Especialidad { IdEspecialidad = 2, Nombre = "Endodoncia" });
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var result = await service.CrearAsync(
            new ProfesionalApiRequest
            {
                Nombres = "María",
                Apellidos = "García",
                RegistroMedico = "RM-MULTI-001",
                CorreoAcceso = "maria.multi@smiletrack.test",
                IdsEspecialidades = new List<int> { 1, 2 }
            },
            operadorId: 1,
            ipOrigen: "127.0.0.1");

        Assert.True(result.Success, result.Message);
        var asignaciones = await db.ProfesionalEspecialidades
            .OrderBy(especialidad => especialidad.IdEspecialidad)
            .ToListAsync();
        Assert.Equal(new[] { 1, 2 }, asignaciones.Select(especialidad => especialidad.IdEspecialidad));
        Assert.Single(asignaciones, especialidad => especialidad.Principal);
        Assert.Equal(1, asignaciones.Single(especialidad => especialidad.Principal).IdEspecialidad);
    }

    [Fact]
    public async Task RegistrarAusenciaConReasignacionAsync_CubreCrucesDeFechaEnCitasYAusencias()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var profesionalAusente = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Ausente",
            RegistroMedico = "RM-AUSENTE",
            Estado = "activo"
        };
        var candidatoConAusencia = new Profesional
        {
            Nombres = "Beatriz",
            Apellidos = "Ocupada",
            RegistroMedico = "RM-AUSENCIA",
            Estado = "activo"
        };
        var candidatoDisponible = new Profesional
        {
            Nombres = "Carla",
            Apellidos = "Disponible",
            RegistroMedico = "RM-DISPONIBLE",
            Estado = "activo"
        };
        db.Profesionales.AddRange(profesionalAusente, candidatoConAusencia, candidatoDisponible);
        var especialidad = new Especialidad { Nombre = "Odontología General" };
        var servicio = new Servicio { Nombre = "Consulta general", Estado = "activo", DuracionMinutos = 60 };
        db.Especialidades.Add(especialidad);
        db.Servicios.Add(servicio);
        await db.SaveChangesAsync();

        servicio.IdEspecialidad = especialidad.IdEspecialidad;
        db.ProfesionalEspecialidades.Add(new Profesional_Especialidad
        {
            IdProfesional = candidatoDisponible.IdProfesional,
            IdEspecialidad = especialidad.IdEspecialidad,
            Principal = true
        });
        db.ProfesionalServicios.Add(new ProfesionalServicio
        {
            IdProfesional = candidatoDisponible.IdProfesional,
            IdServicio = servicio.IdServicio,
            Activo = true
        });

        var fechaCita = DateTime.Today.AddDays(10).AddHours(23).AddMinutes(30);
        var fechaInicioAusencia = fechaCita.Date.AddDays(1);
        db.Citas.Add(new Cita
        {
            IdPaciente = 1,
            IdProfesional = profesionalAusente.IdProfesional,
            IdServicio = servicio.IdServicio,
            FechaHora = fechaCita,
            DuracionMinutos = 60,
            Estado = "Programada"
        });
        db.AusenciasProfesional.Add(new AusenciaProfesional
        {
            IdProfesional = candidatoConAusencia.IdProfesional,
            FechaInicio = DateOnly.FromDateTime(fechaInicioAusencia),
            FechaFin = DateOnly.FromDateTime(fechaInicioAusencia)
        });
        string diaCita = fechaCita.DayOfWeek switch
        {
            DayOfWeek.Monday => "Lunes",
            DayOfWeek.Tuesday => "Martes",
            DayOfWeek.Wednesday => "Miercoles",
            DayOfWeek.Thursday => "Jueves",
            DayOfWeek.Friday => "Viernes",
            DayOfWeek.Saturday => "Sabado",
            _ => "Domingo"
        };
        string diaSiguiente = fechaCita.AddDays(1).DayOfWeek switch
        {
            DayOfWeek.Monday => "Lunes",
            DayOfWeek.Tuesday => "Martes",
            DayOfWeek.Wednesday => "Miercoles",
            DayOfWeek.Thursday => "Jueves",
            DayOfWeek.Friday => "Viernes",
            DayOfWeek.Saturday => "Sabado",
            _ => "Domingo"
        };
        db.HorariosProfesional.AddRange(
            new HorarioProfesional
            {
                IdProfesional = candidatoDisponible.IdProfesional,
                DiaSemana = diaCita,
                HoraInicio = new TimeOnly(22, 0),
                HoraFin = new TimeOnly(23, 59),
                Activo = true
            },
            new HorarioProfesional
            {
                IdProfesional = candidatoDisponible.IdProfesional,
                DiaSemana = diaSiguiente,
                HoraInicio = TimeOnly.MinValue,
                HoraFin = new TimeOnly(2, 0),
                Activo = true
            });
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var resultado = await service.RegistrarAusenciaConReasignacionAsync(
            profesionalAusente.IdProfesional,
            fechaInicioAusencia,
            fechaInicioAusencia.AddHours(23).AddMinutes(59),
            "incapacidad");

        var citaReasignada = await db.Citas.SingleAsync();
        Assert.Equal(1, resultado.TotalReasignadas);
        Assert.Equal(candidatoDisponible.IdProfesional, citaReasignada.IdProfesional);
    }

    [Fact]
    public async Task CambiarEstadoAsync_NoActivaProfesionalSinCredencialesAprobadas()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "inactivo",
            CredencialesAprobadas = false,
            RegistroMedico = "RM-PENDING"
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var result = await service.CambiarEstadoAsync(
            profesional.IdProfesional,
            "activo",
            operadorId: 1,
            ipOrigen: "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(409, result.ErrorStatusCode);
        Assert.Equal("inactivo", (await db.Profesionales.SingleAsync()).Estado);
    }

    [Fact]
    public async Task CambiarEstadoAsync_RecalculaAprobacionDesdeCredencialesAprobadas()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "inactivo",
            CredencialesAprobadas = false,
            RegistroMedico = "RM-APPROVED-CREDENTIAL"
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();
        db.CredencialesProfesionales.Add(new CredencialProfesional
        {
            IdProfesional = profesional.IdProfesional,
            Tipo = "Registro profesional",
            NombreOriginal = "registro.pdf",
            RutaPrivada = "private_uploads/credential.pdf",
            ContentType = "application/pdf",
            TamanoBytes = 100,
            EstadoRevision = "aprobado"
        });
        await db.SaveChangesAsync();

        var result = await new ProfesionalService(db, NullLogger<ProfesionalService>.Instance)
            .CambiarEstadoAsync(profesional.IdProfesional, "activo", operadorId: 1, ipOrigen: "127.0.0.1");

        Assert.True(result.Success, result.Message);
        var savedProfessional = await db.Profesionales.SingleAsync();
        Assert.True(savedProfessional.CredencialesAprobadas);
        Assert.Equal("activo", savedProfessional.Estado);
    }

    [Fact]
    public async Task CambiarEstadoAsync_RechazaReactivacionMientrasQuedenCredencialesPendientes()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var profesional = new Profesional
        {
            Nombres = "Ana",
            Apellidos = "Zuluaga",
            Estado = "inactivo",
            CredencialesAprobadas = false,
            RegistroMedico = "RM-PENDING-CREDENTIAL"
        };
        db.Profesionales.Add(profesional);
        await db.SaveChangesAsync();
        db.CredencialesProfesionales.AddRange(
            CrearCredencial(profesional.IdProfesional, "Registro aprobado.pdf", "aprobado"),
            CrearCredencial(profesional.IdProfesional, "Certificado pendiente.pdf", "pendiente"));
        await db.SaveChangesAsync();

        var result = await new ProfesionalService(db, NullLogger<ProfesionalService>.Instance)
            .CambiarEstadoAsync(profesional.IdProfesional, "activo", operadorId: 1, ipOrigen: "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(409, result.ErrorStatusCode);
        Assert.Contains("pendientes", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("inactivo", (await db.Profesionales.SingleAsync()).Estado);
    }

    private static CredencialProfesional CrearCredencial(int idProfesional, string nombre, string estado) => new()
    {
        IdProfesional = idProfesional,
        Tipo = "Título de especialidad",
        NombreOriginal = nombre,
        RutaPrivada = $"private_uploads/{Guid.NewGuid():N}.pdf",
        ContentType = "application/pdf",
        TamanoBytes = 100,
        EstadoRevision = estado
    };

    [Fact]
    public async Task ObtenerVistaMVCAsync_AplicaFiltroYPaginacion()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Profesionales.AddRange(
            new Profesional { Nombres = "Ana", Apellidos = "Zuluaga", Estado = "activo", RegistroMedico = "RM-1" },
            new Profesional { Nombres = "Luis", Apellidos = "Alvarez", Estado = "inactivo", RegistroMedico = "RM-2" });
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var result = await service.ObtenerVistaMVCAsync(new PaginationQuery
        {
            Page = 1,
            PageSize = 1,
            Search = "Ana"
        });

        Assert.Single(result.Items);
        Assert.Equal("Ana", result.Items[0].Nombres);
        Assert.Equal(1, result.Paginacion.TotalCount);
    }
}