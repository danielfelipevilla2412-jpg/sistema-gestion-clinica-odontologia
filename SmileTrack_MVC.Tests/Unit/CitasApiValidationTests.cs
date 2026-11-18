using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Api.Controllers;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Perfiles;
using SmileTrack_MVC.Services.Email;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class CitasApiValidationTests
{
    private static AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CrearCitaDesdeAgenda_RespuestaExitosa_PersisteLaCita()
    {
        var db = CreateInMemoryDb($"citas_api_agenda_persist_{Guid.NewGuid()}");
        var paciente = new SmileTrack_MVC.Models.Entities.Paciente
        {
            IdPaciente = 1,
            Nombres = "Paciente",
            Apellidos = "Prueba",
            Estado = "activo"
        };
        var profesional = new SmileTrack_MVC.Models.Entities.Profesional
        {
            IdProfesional = 7,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "RM-AGENDA-TEST",
            Estado = "activo"
        };
        var especialidad = new SmileTrack_MVC.Models.Entities.Especialidad
        {
            IdEspecialidad = 1,
            Nombre = "Odontología General"
        };
        var servicio = new SmileTrack_MVC.Models.Entities.Servicio
        {
            IdServicio = 1,
            Nombre = "Consulta",
            Estado = "activo",
            DuracionMinutos = 30,
            IdEspecialidad = 1
        };
        var consultorio = new SmileTrack_MVC.Models.Entities.Consultorio
        {
            IdConsultorio = 1,
            Nombre = "Consultorio 1",
            Estado = "disponible"
        };
        var estado = new SmileTrack_MVC.Models.Entities.EstadoCita
        {
            IdEstado = 1,
            NombreEstado = "Programada"
        };
        db.AddRange(paciente, profesional, especialidad, servicio, consultorio, estado);
        db.ProfesionalEspecialidades.Add(new SmileTrack_MVC.Models.Entities.Profesional_Especialidad
        {
            IdProfesional = profesional.IdProfesional,
            IdEspecialidad = especialidad.IdEspecialidad,
            Principal = true
        });
        db.ProfesionalServicios.Add(new SmileTrack_MVC.Models.Entities.ProfesionalServicio
        {
            IdProfesional = profesional.IdProfesional,
            IdServicio = servicio.IdServicio,
            Activo = true
        });

        DateTime fecha = ColombiaClock.Today.AddDays(1);
        while (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            fecha = fecha.AddDays(1);
        string dia = fecha.DayOfWeek switch
        {
            DayOfWeek.Monday => "Lunes",
            DayOfWeek.Tuesday => "Martes",
            DayOfWeek.Wednesday => "Miercoles",
            DayOfWeek.Thursday => "Jueves",
            DayOfWeek.Friday => "Viernes",
            DayOfWeek.Saturday => "Sabado",
            _ => "Domingo"
        };
        db.HorariosProfesional.Add(new SmileTrack_MVC.Models.Entities.HorarioProfesional
        {
            IdProfesional = profesional.IdProfesional,
            DiaSemana = dia,
            HoraInicio = new TimeOnly(8, 0),
            HoraFin = new TimeOnly(17, 0),
            Activo = true
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, "Administrador");
        var result = await controller.CrearCitaDesdeAgenda(new SmileTrack_MVC.Models.ViewModels.CitaAgendaDto
        {
            IdPaciente = paciente.IdPaciente,
            IdProfesional = profesional.IdProfesional,
            IdServicio = servicio.IdServicio,
            IdConsultorio = consultorio.IdConsultorio,
            Fecha = fecha,
            HoraInicio = new TimeSpan(10, 0, 0),
            HoraFin = new TimeSpan(10, 30, 0),
            Estado = "Programada"
        });

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)response.Value!.GetType().GetProperty("success")!.GetValue(response.Value)!);
        var savedAppointment = await db.Citas.SingleAsync();
        Assert.Equal(paciente.IdPaciente, savedAppointment.IdPaciente);
        Assert.Equal(fecha.AddHours(10), savedAppointment.FechaHora);
    }

    [Fact]
    public async Task SolicitarCita_RechazaPacienteConPerfilIncompleto()
    {
        var db = CreateInMemoryDb($"citas_api_incomplete_profile_{Guid.NewGuid()}");
        db.Usuarios.Add(new SmileTrack_MVC.Models.Entities.Usuario
        {
            IdUsuario = 12,
            Nombre = "Ana",
            Apellidos = "López",
            Correo = "ana@example.test",
            Contrasena = "hash",
            IdRol = 3,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var service = new CitaService(db, NullLogger<CitaService>.Instance);
        var controller = new CitasApiController(
            db,
            NullLogger<CitasApiController>.Instance,
            new NoOpEmailService(),
            service,
            new MemoryCache(new MemoryCacheOptions()),
            new PerfilPacienteService(db));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "12"),
                    new Claim(ClaimTypes.Role, "Paciente")
                ], "Test"))
            }
        };

        var result = await controller.SolicitarCita(new CitaSolicitudPacienteDto
        {
            Fecha = DateTime.Today.AddDays(2)
        });

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var code = conflict.Value?.GetType().GetProperty("code")?.GetValue(conflict.Value)?.ToString();
        Assert.Equal("PROFILE_INCOMPLETE", code);
    }

    [Fact]
    public async Task Actualizar_RejectsModelStateErrorsBeforeBusinessValidation()
    {
        var db = CreateInMemoryDb($"citas_api_validation_{Guid.NewGuid()}");
        var service = new CitaService(db, NullLogger<CitaService>.Instance);
        var controller = new CitasApiController(
            db,
            NullLogger<CitasApiController>.Instance,
            new NoOpEmailService(),
            service,
            new MemoryCache(new MemoryCacheOptions()),
            new PerfilPacienteService(db));

        controller.ModelState.AddModelError("IdPaciente", "El paciente es obligatorio.");

        var dto = new CitaApiUpdateDto
        {
            IdCita = 1,
            IdPaciente = 5,
            FechaHora = DateTime.Today.AddDays(1).AddHours(9)
        };

        var result = await controller.Actualizar(1, dto);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);

        var message = badRequest.Value.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Equal("Datos de cita inválidos.", message);
    }

    [Fact]
    public void GestionCitas_NormalizaEstadosFinalizadosComoAtendidos()
    {
        var method = typeof(GestionCitasController).GetMethod("NormalizarEstado", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal("atendida", method.Invoke(null, ["Finalizada"]));
        Assert.Equal("atendida", method.Invoke(null, ["finalizada"]));
        Assert.Equal("atendida", method.Invoke(null, ["realizada"]));
    }

    [Fact]
    public void CitasApiController_NoDuplicaLaRutaDeReagendar()
    {
        var rutas = typeof(CitasApiController)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .SelectMany(m => m.GetCustomAttributes(typeof(RouteAttribute), inherit: true).Cast<RouteAttribute>())
            .Select(r => r.Template ?? string.Empty)
            .ToList();

        int repeticiones = rutas.Count(t => string.Equals(t, "api/citas/{id:int}/reagendar", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, repeticiones);
    }

    [Fact]
    public async Task Reagendar_RechazaFechaEnElPasado()
    {
        var db = CreateInMemoryDb($"citas_api_reagendar_{Guid.NewGuid()}");
        db.Pacientes.Add(new SmileTrack_MVC.Models.Entities.Paciente
        {
            IdPaciente = 1,
            Nombres = "Ana",
            Apellidos = "Prueba",
            Estado = "activo"
        });
        db.Citas.Add(new SmileTrack_MVC.Models.Entities.Cita
        {
            IdCita = 1,
            IdPaciente = 1,
            Estado = "Programada",
            FechaHora = DateTime.Now.AddDays(1)
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, "Administrador");

        var result = await controller.Reagendar(1, new CitasApiController.CitaReagendarDto
        {
            FechaHora = DateTime.Now.AddDays(-2)
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var message = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Contains("pasado", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ObtenerAsistenciaProcedimiento_ProfesionalAjeno_RetornaForbid()
    {
        var db = CreateInMemoryDb($"citas_api_asistencia_{Guid.NewGuid()}");
        db.Citas.Add(new SmileTrack_MVC.Models.Entities.Cita
        {
            IdCita = 3,
            IdPaciente = 1,
            IdProfesional = 99,
            Estado = "Programada",
            FechaHora = DateTime.Now
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, "Profesional", idProfesional: "7");

        var result = await controller.ObtenerAsistenciaProcedimiento(3);

        Assert.IsType<ForbidResult>(result);
    }

    private static CitasApiController BuildController(AppDbContext db, string rol, string? idProfesional = null)
    {
        var service = new CitaService(db, NullLogger<CitaService>.Instance);
        var controller = new CitasApiController(
            db,
            NullLogger<CitasApiController>.Instance,
            new NoOpEmailService(),
            service,
            new MemoryCache(new MemoryCacheOptions()),
            new PerfilPacienteService(db));

        var claims = new System.Collections.Generic.List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "5"),
            new(ClaimTypes.Role, rol)
        };
        if (idProfesional is not null)
            claims.Add(new Claim("IdProfesional", idProfesional));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };

        return controller;
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendCitaNotificacionAsync(string recipientEmail, string nombrePaciente, DateTime fechaCita, string profesional, string servicio, string nuevoEstado, CancellationToken cancellationToken = default, string? motivoCancelacion = null, string? mensajePersonalizado = null) => Task.CompletedTask;

        public Task SendFacturaRecordatorioAsync(string recipientEmail, string nombrePaciente, IReadOnlyList<FacturaRecordatorioEmail> facturas, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(string correoDestino, CancellationToken cancellationToken = default) => Task.FromResult((true, "OK"));
    }
}
