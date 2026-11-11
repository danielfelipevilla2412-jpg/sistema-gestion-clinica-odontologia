using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Api.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class CitaServiceTests
{
    [Fact]
    public async Task ObtenerKpisGestion_CalculaTendenciaYPorcentajes()
    {
        await using var db = CrearDb();
        var fecha = new DateTime(2026, 9, 10, 10, 0, 0);
        db.Citas.AddRange(
            new Cita { FechaHora = fecha, Estado = "Atendida", IdPaciente = 1 },
            new Cita { FechaHora = fecha.AddDays(1), Estado = "Confirmada", IdPaciente = 2 },
            new Cita { FechaHora = fecha.AddDays(-7), Estado = "Programada", IdPaciente = 3 },
            new Cita { FechaHora = fecha.AddDays(-8), Estado = "Cancelada", IdPaciente = 4 });
        await db.SaveChangesAsync();

        var kpi = await CrearServicio(db).ObtenerKpisGestionAsync(fecha);

        Assert.Equal(4, kpi.Total);
        Assert.Equal(2, kpi.Programadas);
        Assert.Equal(1, kpi.Atendidas);
        Assert.Equal(1, kpi.Canceladas);
        Assert.Equal(0, kpi.DiferenciaSemana);
    }

    [Fact]
    public async Task ObtenerProfesionalesDisponibles_ConsideraLaDuracionRealDeCitasExistentes()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var inicioCandidato = ProximaFecha(DayOfWeek.Monday).AddHours(10).AddMinutes(30);
        db.Citas.Add(new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            FechaHora = inicioCandidato.AddMinutes(-60),
            DuracionMinutos = 90,
            Estado = "Programada"
        });
        await db.SaveChangesAsync();

        var disponibles = await CrearServicio(db).ObtenerProfesionalesDisponiblesAsync(
            inicioCandidato.Date,
            inicioCandidato.TimeOfDay,
            duracionMinutos: 60);

        Assert.Empty(disponibles);
    }

    [Fact]
    public async Task ObtenerProfesionalesDisponibles_UsaLaDuracionDelServicioSeleccionado()
    {
        await using var db = CrearDb();
        var profesional = new Profesional
        {
            Nombres = "Dra.",
            Apellidos = "Prueba",
            Estado = "activo"
        };
        var servicio = new Servicio
        {
            Nombre = "Procedimiento largo",
            Estado = "activo",
            DuracionMinutos = 120
        };
        db.AddRange(profesional, servicio);
        await db.SaveChangesAsync();

        var fecha = ProximaFecha(DayOfWeek.Monday);
        db.HorariosProfesional.Add(new HorarioProfesional
        {
            IdProfesional = profesional.IdProfesional,
            DiaSemana = "Lunes",
            HoraInicio = new TimeOnly(9, 0),
            HoraFin = new TimeOnly(10, 0),
            Activo = true
        });
        await db.SaveChangesAsync();

        var citaService = CrearServicio(db);
        var controller = new CitasApiController(
            db,
            NullLogger<CitasApiController>.Instance,
            null!,
            citaService,
            new MemoryCache(new MemoryCacheOptions()));

        var result = await controller.ObtenerProfesionalesDisponibles(
            fecha,
            "09:00",
            duracionMinutos: null,
            idServicio: servicio.IdServicio);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("\"data\":[]", JsonSerializer.Serialize(response.Value));
    }

    [Fact]
    public void CitaAgendaDto_AceptaDuracionNoHardcodeada()
    {
        var dto = new CitaAgendaDto
        {
            IdPaciente = 1,
            IdProfesional = 1,
            IdServicio = 1,
            IdConsultorio = 1,
            Fecha = DateTime.Today.AddDays(1),
            HoraInicio = new TimeSpan(9, 0, 0),
            Estado = "Programada"
        };
        var resultados = new List<ValidationResult>();

        bool valido = Validator.TryValidateObject(
            dto,
            new ValidationContext(dto),
            resultados,
            validateAllProperties: true);

        Assert.True(valido);
    }

    [Fact]
    public void CitaAgendaDto_RechazaHoraFinAnteriorAlInicio()
    {
        var dto = new CitaAgendaDto
        {
            IdPaciente = 1,
            IdProfesional = 1,
            IdServicio = 1,
            IdConsultorio = 1,
            Fecha = DateTime.Today.AddDays(1),
            HoraInicio = new TimeSpan(10, 0, 0),
            HoraFin = new TimeSpan(9, 30, 0),
            Estado = "Programada"
        };
        var resultados = new List<ValidationResult>();

        bool valido = Validator.TryValidateObject(
            dto,
            new ValidationContext(dto),
            resultados,
            validateAllProperties: true);

        Assert.False(valido);
        Assert.Contains(resultados, resultado =>
            resultado.MemberNames.Contains(nameof(CitaAgendaDto.HoraFin)));
    }

    [Fact]
    public async Task ObtenerSolicitudesPendientesAsync_DevuelveSoloCitasEnEstadoSolicitada()
    {
        await using var db = CrearDb();
        var paciente = new Paciente { Nombres = "Ana", Apellidos = "García", Estado = "activo", Correo = "ana@test.com" };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();

        db.Citas.AddRange(
            new Cita { IdPaciente = paciente.IdPaciente, FechaHora = DateTime.Today.AddDays(3).AddHours(9), Estado = "Solicitada", IdServicio = 1, Notas = "Necesito revisión" },
            new Cita { IdPaciente = paciente.IdPaciente, FechaHora = DateTime.Today.AddDays(4).AddHours(10), Estado = "Confirmada", IdServicio = 1, Notas = "Confirmada" },
            new Cita { IdPaciente = paciente.IdPaciente, FechaHora = DateTime.Today.AddDays(5).AddHours(11), Estado = "Programada", IdServicio = 1, Notas = "Programada" }
        );
        await db.SaveChangesAsync();

        var solicitudes = await CrearServicio(db).ObtenerSolicitudesPendientesAsync();

        Assert.Single(solicitudes);
        Assert.Equal("Solicitada", solicitudes[0].Estado);
        Assert.Contains("Necesito revisión", solicitudes[0].Notas);
    }

    [Fact]
    public async Task RechazarSolicitudAsync_GuardaMotivoHistorialYNotificacionParaPaciente()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var estadoRechazada = new EstadoCita { NombreEstado = "Rechazada" };
        db.EstadosCita.Add(estadoRechazada);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdServicio = datos.Servicio.IdServicio,
            FechaHora = ColombiaClock.Today.AddDays(2).AddHours(9),
            Estado = "Solicitada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db).RechazarSolicitudAsync(
            cita.IdCita,
            "No hay disponibilidad",
            idUsuarioActor: 27);

        Assert.Equal("Rechazada", resultado?.Estado);
        Assert.Equal(estadoRechazada.IdEstado, resultado?.IdEstado);
        var historial = await db.CitasHistorialEstado.SingleAsync();
        Assert.Equal(27, historial.IdUsuario);
        Assert.Contains("No hay disponibilidad", historial.Motivo);
        var notificacion = await db.Notificaciones.SingleAsync();
        Assert.Equal(cita.IdPaciente, notificacion.IdPaciente);
        Assert.Equal("cita_rechazada", notificacion.Tipo);
        Assert.Contains("No hay disponibilidad", notificacion.Contenido);
    }

    [Fact]
    public async Task RechazarSolicitudAsync_NoPermiteRechazarUnaCitaYaConfirmada()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdServicio = datos.Servicio.IdServicio,
            FechaHora = ColombiaClock.Today.AddDays(2).AddHours(9),
            Estado = "Confirmada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).RechazarSolicitudAsync(cita.IdCita, "No hay disponibilidad"));

        Assert.Contains("pendientes", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Confirmada", (await db.Citas.FindAsync(cita.IdCita))?.Estado);
        Assert.Empty(await db.Notificaciones.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_AceptaAliasProgramadaCuandoElCatalogoUsaAgendada()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        DateTime fecha = DateTime.Today.AddDays(2).AddHours(9);

        db.EstadosCita.RemoveRange(db.EstadosCita);
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Agendada" });
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Confirmada" });
        db.HorariosProfesional.Add(new HorarioProfesional
        {
            IdProfesional = datos.Profesional.IdProfesional,
            DiaSemana = fecha.DayOfWeek switch
            {
                DayOfWeek.Monday => "Lunes",
                DayOfWeek.Tuesday => "Martes",
                DayOfWeek.Wednesday => "Miercoles",
                DayOfWeek.Thursday => "Jueves",
                DayOfWeek.Friday => "Viernes",
                DayOfWeek.Saturday => "Sabado",
                _ => "Domingo"
            },
            HoraInicio = new TimeOnly(8, 0),
            HoraFin = new TimeOnly(12, 0),
            Activo = true
        });
        await db.SaveChangesAsync();

        var cita = await CrearServicio(db).CrearAsync(new CitaApiRequest
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = fecha,
            Estado = "Programada"
        });

        Assert.NotNull(cita);
        Assert.Equal("Agendada", cita.Estado);
    }

    [Fact]
    public async Task CrearAsync_AceptaCitaDentroDelHorarioProfesional()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        DateTime fecha = ProximaFecha(DayOfWeek.Monday).AddHours(9);

        db.HorariosProfesional.Add(new HorarioProfesional
        {
            IdProfesional = datos.Profesional.IdProfesional,
            DiaSemana = "Lunes",
            HoraInicio = new TimeOnly(8, 0),
            HoraFin = new TimeOnly(12, 0),
            Activo = true
        });
        await db.SaveChangesAsync();

        var cita = await CrearServicio(db).CrearAsync(new CitaApiRequest
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = fecha,
            Estado = "Programada"
        });

        Assert.Equal(fecha, cita.FechaHora);
    }

    [Fact]
    public async Task CrearAsync_RechazaCitaDuranteBloqueoProfesional()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        DateTime fecha = DateTime.Today.AddDays(2).AddHours(10);

        db.BloqueosProfesional.Add(new BloqueoProfesional
        {
            IdProfesional = datos.Profesional.IdProfesional,
            FechaInicio = fecha.AddMinutes(-15),
            FechaFin = fecha.AddHours(1)
        });
        await db.SaveChangesAsync();

        var accion = () => CrearServicio(db).CrearAsync(new CitaApiRequest
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = fecha,
            Estado = "Programada"
        });

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(accion);
        Assert.Contains("bloqueo", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearAsync_RechazaServicioDeOtraEspecialidad()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var especialidadProfesional = new Especialidad { Nombre = "Ortodoncia" };
        var especialidadServicio = new Especialidad { Nombre = "Endodoncia" };
        db.Especialidades.AddRange(especialidadProfesional, especialidadServicio);
        await db.SaveChangesAsync();
        datos.Servicio.IdEspecialidad = especialidadServicio.IdEspecialidad;
        db.ProfesionalEspecialidades.Add(new Profesional_Especialidad
        {
            IdProfesional = datos.Profesional.IdProfesional,
            IdEspecialidad = especialidadProfesional.IdEspecialidad,
            Principal = true
        });
        await db.SaveChangesAsync();

        var accion = () => CrearServicio(db).CrearAsync(new CitaApiRequest
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = DateTime.Today.AddDays(2).AddHours(10),
            Estado = "Programada"
        });

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(accion);
        Assert.Contains("especialidades", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearAsync_RechazaCitaDuranteAusenciaProfesional()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        DateTime fecha = DateTime.Today.AddDays(2).AddHours(10);

        db.AusenciasProfesional.Add(new AusenciaProfesional
        {
            IdProfesional = datos.Profesional.IdProfesional,
            Tipo = "permiso",
            FechaInicio = DateOnly.FromDateTime(fecha),
            FechaFin = DateOnly.FromDateTime(fecha)
        });
        await db.SaveChangesAsync();

        var accion = () => CrearServicio(db).CrearAsync(new CitaApiRequest
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = fecha,
            Estado = "Programada"
        });

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(accion);
        Assert.Contains("ausencia", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidarHorarioClinica_AceptaBloqueCompletoDentroDelHorario()
    {
        await using var db = CrearDb();
        db.ConfiguracionesGenerales.AddRange(
            new ConfiguracionGeneral { Clave = "horario_apertura", Valor = "07:00" },
            new ConfiguracionGeneral { Clave = "horario_cierre", Valor = "18:00" },
            new ConfiguracionGeneral { Clave = "dias_atencion", Valor = "1,2,3,4,5,6,7" });
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db).ValidarHorarioClinicaAsync(
            DateTime.Today.AddDays(1).Date.AddHours(17), 60);

        Assert.True(resultado.EsValido);
    }

    [Fact]
    public async Task ValidarHorarioClinica_RechazaCuandoElFinExcedeElCierre()
    {
        await using var db = CrearDb();
        db.ConfiguracionesGenerales.AddRange(
            new ConfiguracionGeneral { Clave = "horario_apertura", Valor = "07:00" },
            new ConfiguracionGeneral { Clave = "horario_cierre", Valor = "18:00" },
            new ConfiguracionGeneral { Clave = "dias_atencion", Valor = "1,2,3,4,5,6,7" });
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db).ValidarHorarioClinicaAsync(
            DateTime.Today.AddDays(1).Date.AddHours(17).AddMinutes(1), 60);

        Assert.False(resultado.EsValido);
        Assert.Contains("finalizar", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ObtenerDuracionCitaMinutos_UsaConfiguracionExistente()
    {
        await using var db = CrearDb();
        db.ConfiguracionesGenerales.Add(new ConfiguracionGeneral
        {
            Clave = "cita_duracion_minutos",
            Valor = "90"
        });
        await db.SaveChangesAsync();

        var duracion = await CrearServicio(db).ObtenerDuracionCitaMinutosAsync();

        Assert.Equal(90, duracion);
    }

    [Fact]
    public async Task CambiarEstadoAsync_RechazaConfirmarCitaSinProfesional()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Confirmada" });
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdServicio = datos.Servicio.IdServicio,
            IdProfesional = null,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = DateTime.Today.AddDays(2).AddHours(9),
            Estado = "Agendada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).CambiarEstadoAsync(cita.IdCita, "Confirmada"));

        Assert.Contains("profesional", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CambiarEstadoAsync_RechazaReactivacionDeEstadoTerminal()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        db.EstadosCita.AddRange(
            new EstadoCita { NombreEstado = "Atendida" },
            new EstadoCita { NombreEstado = "Confirmada" });
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = DateTime.Today.AddDays(1).AddHours(9),
            Estado = "Atendida",
            IdEstado = 2
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var accion = () => CrearServicio(db).CambiarEstadoAsync(cita.IdCita, "Confirmada");

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(accion);
        Assert.Contains("finalizada", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CambiarEstadoAsync_IniciaCitaAlcanzadaYRegistraInicioDeProcedimiento()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "En consulta" });
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = ColombiaClock.Now.AddMinutes(-1),
            Estado = "Confirmada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var actualizada = await CrearServicio(db).CambiarEstadoAsync(cita.IdCita, "En consulta");

        Assert.Equal("En consulta", actualizada?.Estado);
        var asistencia = await db.AsistenciasProcedimiento.SingleAsync(a => a.IdCita == cita.IdCita);
        Assert.InRange(asistencia.Inicio, ColombiaClock.Now.AddMinutes(-1), ColombiaClock.Now.AddSeconds(1));
    }

    [Theory]
    [InlineData("En consulta")]
    [InlineData("Atendida")]
    [InlineData("No asistió")]
    public async Task CambiarEstadoAsync_RechazaEstadosClinicosAntesDeLaFechaProgramada(string estado)
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = DateTime.Today.AddDays(1).AddHours(9),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).CambiarEstadoAsync(cita.IdCita, estado));

        Assert.Contains("día programado", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Programada", (await db.Citas.FindAsync(cita.IdCita))?.Estado);
    }

    [Fact]
    public async Task ActualizarAsync_RechazaReactivacionDeEstadoTerminal()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        db.EstadosCita.AddRange(
            new EstadoCita { NombreEstado = "Atendida" },
            new EstadoCita { NombreEstado = "Confirmada" });
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = DateTime.Today.AddDays(1).AddHours(9),
            Estado = "Atendida",
            IdEstado = 2
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var accion = () => CrearServicio(db).ActualizarAsync(cita.IdCita, new CitaApiUpdateDto
        {
            IdCita = cita.IdCita,
            IdPaciente = datos.Paciente.IdPaciente,
            Estado = "Confirmada",
            FechaHora = cita.FechaHora
        });

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(accion);
        Assert.Contains("finalizada", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActualizarAsync_RegistraCambioDeConsultorioConUsuarioYFecha()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var consultorioNuevo = new Consultorio { Nombre = "Consultorio 2", Estado = "activo" };
        db.Consultorios.Add(consultorioNuevo);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = ProximaFecha(DayOfWeek.Monday).AddHours(9),
            Estado = "Programada",
            IdEstado = 1
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        DateTime antesDeActualizar = ColombiaClock.Now;
        await CrearServicio(db).ActualizarAsync(cita.IdCita, new CitaApiUpdateDto
        {
            IdCita = cita.IdCita,
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = consultorioNuevo.IdConsultorio,
            FechaHora = cita.FechaHora,
            Estado = "Programada",
            IdEstado = 1,
            IdUsuarioModificacion = 17
        });
        DateTime despuesDeActualizar = ColombiaClock.Now;

        var historial = await db.CitasConsultoriosHistorial.SingleAsync();
        Assert.Equal(cita.IdCita, historial.IdCita);
        Assert.Equal(datos.Consultorio.IdConsultorio, historial.IdConsultorioAnterior);
        Assert.Equal(consultorioNuevo.IdConsultorio, historial.IdConsultorioNuevo);
        Assert.Equal(17, historial.IdUsuario);
        Assert.InRange(historial.FechaCambio, antesDeActualizar, despuesDeActualizar);
    }

    private static CitaService CrearServicio(AppDbContext db) =>
        new(db, NullLogger<CitaService>.Instance);

    private static AppDbContext CrearDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (Paciente Paciente, Profesional Profesional, Servicio Servicio, Consultorio Consultorio) SeedBase(AppDbContext db)
    {
        var paciente = new Paciente { Nombres = "Ana", Apellidos = "Prueba", Estado = "activo" };
        var profesional = new Profesional { Nombres = "Dr.", Apellidos = "Prueba", Estado = "activo" };
        var especialidad = new Especialidad { Nombre = "Odontología General" };
        var servicio = new Servicio { Nombre = "Consulta", Estado = "activo", IdEspecialidad = 1 };
        var consultorio = new Consultorio { Nombre = "Consultorio 1", Estado = "activo" };
        db.Pacientes.Add(paciente);
        db.Profesionales.Add(profesional);
        db.Especialidades.Add(especialidad);
        db.Servicios.Add(servicio);
        db.Consultorios.Add(consultorio);
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Programada" });
        db.SaveChanges();

        servicio.IdEspecialidad = especialidad.IdEspecialidad;
        db.ProfesionalEspecialidades.Add(new Profesional_Especialidad
        {
            IdProfesional = profesional.IdProfesional,
            IdEspecialidad = especialidad.IdEspecialidad,
            Principal = true
        });
        db.ProfesionalServicios.Add(new ProfesionalServicio
        {
            IdProfesional = profesional.IdProfesional,
            IdServicio = servicio.IdServicio,
            Activo = true
        });
        db.SaveChanges();

        string[] dias = ["Lunes", "Martes", "Miercoles", "Jueves", "Viernes", "Sabado", "Domingo"];
        foreach (var dia in dias)
        {
            db.HorariosProfesional.Add(new HorarioProfesional
            {
                IdProfesional = profesional.IdProfesional,
                DiaSemana = dia,
                HoraInicio = new TimeOnly(0, 0),
                HoraFin = new TimeOnly(23, 59),
                Activo = true
            });
        }
        db.SaveChanges();

        return (paciente, profesional, servicio, consultorio);
    }

    private static DateTime ProximaFecha(DayOfWeek dia)
    {
        DateTime fecha = DateTime.Today.AddDays(1);
        while (fecha.DayOfWeek != dia)
            fecha = fecha.AddDays(1);
        return fecha;
    }

    // =========================================================================
    // NUEVAS PRUEBAS: SolicitarCitaPacienteAsync
    // =========================================================================

    [Fact]
    public async Task ObtenerProfesionalesDisponiblesAsync_AceptaServicioConAsignacionActivaYEspecialidadCoincidente()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var fecha = ProximaFecha(DayOfWeek.Monday).Date.AddHours(9);

        db.HorariosProfesional.Add(new HorarioProfesional
        {
            IdProfesional = datos.Profesional.IdProfesional,
            DiaSemana = "Lunes",
            HoraInicio = new TimeOnly(8, 0),
            HoraFin = new TimeOnly(12, 0),
            Activo = true
        });


        var asignacion = await db.ProfesionalServicios.SingleAsync();
        asignacion.PrecioPersonalizado = 125000m;
        await db.SaveChangesAsync();

        var disponibles = await CrearServicio(db).ObtenerProfesionalesDisponiblesAsync(
            fecha.Date,
            TimeSpan.FromHours(9),
            duracionMinutos: 60,
            idServicio: datos.Servicio.IdServicio);

        Assert.Single(disponibles);
    }

    [Fact]
    public async Task CrearAsync_RechazaAsignacionProfesionalServicioInactiva()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var asignacion = await db.ProfesionalServicios.SingleAsync();
        asignacion.Activo = false;
        await db.SaveChangesAsync();

        var fecha = ProximaFecha(DayOfWeek.Monday).AddHours(9);
        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).CrearAsync(new CitaApiRequest
            {
                IdPaciente = datos.Paciente.IdPaciente,
                IdProfesional = datos.Profesional.IdProfesional,
                IdServicio = datos.Servicio.IdServicio,
                IdConsultorio = datos.Consultorio.IdConsultorio,
                FechaHora = fecha,
                Estado = "Agendada"
            }));

        Assert.Contains("asignación activa", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearAsync_RechazaServicioSinEspecialidadConfigurada()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        datos.Servicio.IdEspecialidad = null;
        await db.SaveChangesAsync();

        var fecha = ProximaFecha(DayOfWeek.Monday).AddHours(9);
        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).CrearAsync(new CitaApiRequest
            {
                IdPaciente = datos.Paciente.IdPaciente,
                IdProfesional = datos.Profesional.IdProfesional,
                IdServicio = datos.Servicio.IdServicio,
                IdConsultorio = datos.Consultorio.IdConsultorio,
                FechaHora = fecha,
                Estado = "Agendada"
            }));

        Assert.Contains("especialidad configurada", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SolicitarCitaPacienteAsync_RechazaFechaPasada()
    {
        // Arrange
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var dto = new CitaSolicitudPacienteDto
        {
            Fecha = DateTime.Today.AddDays(-1),  // fecha pasada
            IdServicio = datos.Servicio.IdServicio,
            Notas = "Prueba"
        };

        // Act & Assert
        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).SolicitarCitaPacienteAsync(datos.Paciente.IdPaciente, dto));

        Assert.Contains("pasadas", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SolicitarCitaPacienteAsync_CreaCitaEnEstadoSolicitada()
    {
        // Arrange
        await using var db = CrearDb();
        var datos = SeedBase(db);
        // Agregar estado "Solicitada" para el flujo ideal
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Solicitada" });
        await db.SaveChangesAsync();

        var dto = new CitaSolicitudPacienteDto
        {
            Fecha = DateTime.Today.AddDays(2),
            IdServicio = datos.Servicio.IdServicio,
            Notas = "Muela del juicio"
        };

        // Act
        var cita = await CrearServicio(db).SolicitarCitaPacienteAsync(datos.Paciente.IdPaciente, dto);

        // Assert: la cita debe existir y tener estado apropiado para solicitudes
        Assert.NotNull(cita);
        Assert.Equal(datos.Paciente.IdPaciente, cita.IdPaciente);
        Assert.Null(cita.IdProfesional);     // sin profesional asignado aún
        Assert.Null(cita.IdConsultorio);     // sin consultorio asignado aún
        Assert.Equal("Solicitada", cita.Estado);
    }

    [Fact]
    public async Task SolicitarCitaPacienteAsync_SinServicio_RechazaSolicitud()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Solicitada" });
        await db.SaveChangesAsync();

        var dto = new CitaSolicitudPacienteDto
        {
            Fecha = DateTime.Today.AddDays(3),
            IdServicio = null,
            Notas = "No sé qué servicio necesito"
        };

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).SolicitarCitaPacienteAsync(datos.Paciente.IdPaciente, dto));

        Assert.Contains("servicio", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.Citas.ToListAsync());
    }

    // =========================================================================
    // NUEVAS PRUEBAS: CancelarAsync (Recepcionista)
    // =========================================================================

    [Fact]
    public async Task CancelarAsync_CancelaCorrectamenteCitaExistente()
    {
        // Arrange
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = DateTime.Today.AddDays(5).AddHours(10),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        // Act
        bool resultado = await CrearServicio(db).CancelarAsync(cita.IdCita, "Cancelación administrativa");

        // Assert
        Assert.True(resultado);
        var citaActualizada = await db.Citas.FindAsync(cita.IdCita);
        Assert.True(
            string.Equals(citaActualizada?.Estado, "Cancelada", StringComparison.OrdinalIgnoreCase),
            $"Se esperaba 'Cancelada', se obtuvo: '{citaActualizada?.Estado}'");
        Assert.Equal("Cancelación administrativa", citaActualizada?.MotivoCancelacion);
    }

    [Fact]
    public async Task CancelarAsync_RechazaMotivoVacio()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = DateTime.Today.AddDays(5).AddHours(10),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).CancelarAsync(cita.IdCita, "   "));

        Assert.Contains("motivo de cancelación es obligatorio", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Programada", (await db.Citas.FindAsync(cita.IdCita))?.Estado);
    }

    [Fact]
    public async Task CancelarAsync_RechazaMotivoConMenosDeCincoCaracteres()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = DateTime.Today.AddDays(5).AddHours(10),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).CancelarAsync(cita.IdCita, "abc"));

        Assert.Contains("al menos 5 caracteres", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Programada", (await db.Citas.FindAsync(cita.IdCita))?.Estado);
    }

    [Fact]
    public void CitaCancelacionDto_RechazaMotivoMenorDeCincoCaracteres()
    {
        var dto = new CitaCancelacionDto { Motivo = "abc" };
        var results = new List<ValidationResult>();

        bool isValid = Validator.TryValidateObject(
            dto,
            new ValidationContext(dto),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.ErrorMessage!.Contains("5 caracteres"));
    }

    [Fact]
    public async Task VerificarConflictoCompletoAsync_RechazaSolapamientosPorProfesionalPacienteYConsultorio()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        db.Citas.Add(new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = new DateTime(2026, 12, 10, 9, 0, 0),
            DuracionMinutos = 60,
            Estado = "Programada"
        });
        await db.SaveChangesAsync();

        var service = CrearServicio(db);
        var inicioSolapado = new DateTime(2026, 12, 10, 9, 30, 0);

        var conflictoProfesional = await service.VerificarConflictoCompletoAsync(
            datos.Profesional.IdProfesional, 0, null, inicioSolapado);
        var conflictoPaciente = await service.VerificarConflictoCompletoAsync(
            null, datos.Paciente.IdPaciente, null, inicioSolapado);
        var conflictoConsultorio = await service.VerificarConflictoCompletoAsync(
            null, 0, datos.Consultorio.IdConsultorio, inicioSolapado);

        Assert.Equal(TipoConflictoCita.Profesional, conflictoProfesional.Tipo);
        Assert.Equal(TipoConflictoCita.Paciente, conflictoPaciente.Tipo);
        Assert.Equal(TipoConflictoCita.Consultorio, conflictoConsultorio.Tipo);
    }

    [Fact]
    public async Task CambiarEstadoAsync_RechazaCancelacionPorFlujoNormal()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = DateTime.Today.AddDays(2).AddHours(9),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).CambiarEstadoAsync(cita.IdCita, "Cancelada"));

        Assert.Contains("función de cancelación", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelarAsync_RechazaCitaVencidaAntesDeGuardar()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = DateTime.Now.AddMinutes(-5),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).CancelarAsync(cita.IdCita, "La cita ya venció"));

        Assert.Contains("fecha y hora anterior", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        var citaSinCambios = await db.Citas.FindAsync(cita.IdCita);
        Assert.Equal("Programada", citaSinCambios?.Estado);
    }

    [Fact]
    public async Task CancelarAsync_RechazaPacienteConMenosDeDosHoras()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            FechaHora = DateTime.Now.AddHours(1),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var accion = () => CrearServicio(db).CancelarAsync(
            cita.IdCita,
            "Solicitud del paciente",
            TimeSpan.FromHours(2));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(accion);

        Assert.Contains("2 horas", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        var citaSinCambios = await db.Citas.FindAsync(cita.IdCita);
        Assert.Equal("Programada", citaSinCambios?.Estado);
    }

    [Fact]
    public async Task CancelarAsync_PermitePacienteConDosHorasDeAnticipacion()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            FechaHora = DateTime.Now.AddHours(3),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db).CancelarAsync(
            cita.IdCita,
            "Solicitud del paciente",
            TimeSpan.FromHours(2));

        Assert.True(resultado);
        var citaCancelada = await db.Citas.FindAsync(cita.IdCita);
        Assert.Equal("Cancelada", citaCancelada?.Estado);
    }

    [Fact]
    public async Task CancelarAsync_RetornaFalseParaCitaInexistente()
    {
        // Arrange
        await using var db = CrearDb();
        _ = SeedBase(db);

        // Act
        bool resultado = await CrearServicio(db).CancelarAsync(99999, "Cita inexistente");

        // Assert
        Assert.False(resultado);
    }

    // =========================================================================
    // NUEVAS PRUEBAS: EnviarRecordatoriosAsync
    // =========================================================================

    [Fact]
    public async Task EnviarRecordatoriosAsync_RetornaCeroEnviadosSiPacienteSinCorreo()
    {
        // Arrange — paciente sin correo registrado
        await using var db = CrearDb();
        var datos = SeedBase(db);
        // El paciente de SeedBase no tiene correo (campo Correo = null por defecto)
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            FechaHora = DateTime.Today.AddDays(1).AddHours(9),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        // Act
        var (enviados, fallidos, idsEnviados) = await CrearServicio(db).EnviarRecordatoriosAsync(
            new List<int> { cita.IdCita });

        // Assert: sin correo no se puede enviar, pero tampoco debe lanzar excepción
        Assert.Equal(0, enviados);
        Assert.Equal(1, fallidos);
        Assert.Empty(idsEnviados);
        var registro = await db.RecordatoriosCita.SingleAsync();
        Assert.Equal("fallido", registro.Estado);
        Assert.Contains("correo", registro.UltimoError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnviarRecordatoriosAsync_SendsCustomMessageAndPersistsSuccessfulDelivery()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        datos.Paciente.Correo = "ana@example.test";
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            Paciente = datos.Paciente,
            IdProfesional = datos.Profesional.IdProfesional,
            Profesional = datos.Profesional,
            FechaHora = DateTime.Now.AddDays(1),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();
        var emailService = new CapturingEmailService();
        var service = new CitaService(db, NullLogger<CitaService>.Instance, emailService);

        var (enviados, fallidos, idsEnviados) = await service.EnviarRecordatoriosAsync(
            [cita.IdCita, cita.IdCita],
            "Confirma tu asistencia.");

        Assert.Equal(1, enviados);
        Assert.Equal(0, fallidos);
        Assert.Equal([cita.IdCita], idsEnviados);
        Assert.Equal("Confirma tu asistencia.", emailService.MensajePersonalizado);
        var registro = await db.RecordatoriosCita.SingleAsync();
        Assert.Equal("enviado", registro.Estado);
        Assert.NotNull(registro.EnviadoEn);
        Assert.Single(await db.Auditorias.ToListAsync());
    }

    private sealed class CapturingEmailService : IEmailService
    {
        public string? MensajePersonalizado { get; private set; }

        public Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendCitaNotificacionAsync(
            string recipientEmail,
            string nombrePaciente,
            DateTime fechaCita,
            string profesional,
            string servicio,
            string nuevoEstado,
            CancellationToken cancellationToken = default,
            string? motivoCancelacion = null,
            string? mensajePersonalizado = null)
        {
            MensajePersonalizado = mensajePersonalizado;
            return Task.CompletedTask;
        }

        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(
            string correoDestino,
            CancellationToken cancellationToken = default)
            => Task.FromResult((true, "OK"));
    }
}
