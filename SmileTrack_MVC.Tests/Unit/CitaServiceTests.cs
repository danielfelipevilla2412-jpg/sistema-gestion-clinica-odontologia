using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Api.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Exceptions;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;
using SmileTrack_MVC.Services.GestionDeCitas;
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
    public void CitaService_SeResuelveConServiciosDeConsultaYDisponibilidad()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<ICitaQueryService, CitaQueryService>();
        services.AddScoped<ICitaDisponibilidadService, CitaDisponibilidadService>();
        services.AddScoped<ICitaService, CitaService>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<CitaService>(scope.ServiceProvider.GetRequiredService<ICitaService>());
    }

    [Fact]
    public async Task ObtenerAsync_MantieneAlcanceDePacienteYPaginacion()
    {
        await using var db = CrearDb();
        var paciente = new Paciente { Nombres = "Ana", Apellidos = "Prueba", Estado = "activo" };
        var otroPaciente = new Paciente { Nombres = "Luis", Apellidos = "Prueba", Estado = "activo" };
        db.Pacientes.AddRange(paciente, otroPaciente);
        await db.SaveChangesAsync();

        DateTime baseFecha = ColombiaClock.Now.AddDays(2);
        db.Citas.AddRange(
            new Cita { IdPaciente = paciente.IdPaciente, FechaHora = baseFecha, Estado = "Programada" },
            new Cita { IdPaciente = otroPaciente.IdPaciente, FechaHora = baseFecha.AddHours(1), Estado = "Programada" },
            new Cita { IdPaciente = paciente.IdPaciente, FechaHora = baseFecha.AddHours(2), Estado = "Programada" });
        await db.SaveChangesAsync();

        var (items, total) = await CrearServicio(db).ObtenerAsync(
            page: 2,
            pageSize: 1,
            role: "Paciente",
            idPaciente: paciente.IdPaciente);

        Assert.Equal(2, total);
        Assert.Single(items);
        Assert.Equal(baseFecha, items[0].FechaHora);

        var (sinIdentidad, totalSinIdentidad) = await CrearServicio(db).ObtenerAsync(
            page: 1,
            pageSize: 10,
            role: "Paciente");

        Assert.Empty(sinIdentidad);
        Assert.Equal(0, totalSinIdentidad);
    }

    [Fact]
    public async Task ObtenerListadoGestionAsync_AplicaFiltrosYPaginaSolicitada()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        DateTime fecha = ProximaFecha(DayOfWeek.Monday).Date;
        db.Citas.AddRange(
            new Cita
            {
                IdPaciente = datos.Paciente.IdPaciente,
                IdProfesional = datos.Profesional.IdProfesional,
                FechaHora = fecha.AddHours(9),
                Estado = "Programada"
            },
            new Cita
            {
                IdPaciente = datos.Paciente.IdPaciente,
                IdProfesional = datos.Profesional.IdProfesional,
                FechaHora = fecha.AddHours(10),
                Estado = "Programada"
            },
            new Cita
            {
                IdPaciente = datos.Paciente.IdPaciente,
                IdProfesional = datos.Profesional.IdProfesional,
                FechaHora = fecha.AddDays(1).AddHours(9),
                Estado = "Atendida"
            });
        await db.SaveChangesAsync();

        var result = await CrearServicio(db).ObtenerListadoGestionAsync(
            new PaginationQuery
            {
                Estado = "Programada",
                Profesional = datos.Profesional.IdProfesional.ToString(),
                Fecha = fecha.ToString("yyyy-MM-dd")
            },
            page: 2,
            pageSize: 1);

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(fecha.AddHours(9), result.Items[0].FechaHora);
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
    public async Task ObtenerProfesionalesDisponibles_IgnoraCitasRechazadas()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var estadoRechazada = new EstadoCita { NombreEstado = "Rechazada" };
        db.EstadosCita.Add(estadoRechazada);
        await db.SaveChangesAsync();
        var inicioCandidato = ProximaFecha(DayOfWeek.Monday).AddHours(10);
        db.Citas.Add(new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            IdEstado = estadoRechazada.IdEstado,
            EstadoCita = estadoRechazada,
            FechaHora = inicioCandidato,
            DuracionMinutos = 60,
            Estado = "Programada"
        });
        await db.SaveChangesAsync();

        var disponibles = await CrearServicio(db).ObtenerProfesionalesDisponiblesAsync(
            inicioCandidato.Date,
            inicioCandidato.TimeOfDay,
            duracionMinutos: 60,
            idServicio: datos.Servicio.IdServicio);

        Assert.Contains(disponibles, profesional => profesional.IdProfesional == datos.Profesional.IdProfesional);
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
            new MemoryCache(new MemoryCacheOptions()),
            new SmileTrack_MVC.Services.Perfiles.PerfilPacienteService(db));

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

        string motivo = new('x', 500);
        var resultado = await CrearServicio(db).RechazarSolicitudAsync(
            cita.IdCita,
            motivo,
            idUsuarioActor: 27,
            ipOrigen: "127.0.0.1");

        Assert.Equal("Rechazada", resultado?.Estado);
        Assert.Equal(estadoRechazada.IdEstado, resultado?.IdEstado);
        var historial = await db.CitasHistorialEstado.SingleAsync();
        Assert.Equal(27, historial.IdUsuario);
        Assert.Contains(motivo, historial.Motivo);
        var notificacion = await db.Notificaciones.SingleAsync();
        Assert.Equal(cita.IdPaciente, notificacion.IdPaciente);
        Assert.Equal("cita_rechazada", notificacion.Tipo);
        Assert.Contains(motivo, notificacion.Contenido);
        var auditoria = await db.Auditorias.SingleAsync();
        Assert.Equal(27, auditoria.IdUsuario);
        Assert.Equal("127.0.0.1", auditoria.IpOrigen);
        Assert.NotNull(auditoria.Descripcion);
        Assert.True(auditoria.Descripcion.Length <= 255);
        Assert.Contains("Solicitada", auditoria.DatosAnteriores);
        Assert.Contains("Rechazada", auditoria.DatosNuevos);
        Assert.Contains(motivo, auditoria.DatosNuevos);
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
    public async Task ConfirmarYAsignarCitaAsync_RechazaSolicitudSinServicioConErrorDeNegocio()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Solicitada" });
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = null,
            IdServicio = null,
            IdConsultorio = null,
            FechaHora = ColombiaClock.Today.AddDays(2).AddHours(8),
            Estado = "Solicitada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).ConfirmarYAsignarCitaAsync(cita.IdCita, new CitaConfirmacionAsignacionDto
            {
                IdCita = cita.IdCita,
                IdProfesional = datos.Profesional.IdProfesional,
                IdConsultorio = datos.Consultorio.IdConsultorio,
                Fecha = ColombiaClock.Today.AddDays(2),
                HoraInicio = TimeSpan.FromHours(9)
            }));

        Assert.Contains("servicio", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmarYAsignarCitaAsync_RegistraEstadoYActorEnElHistorial()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var estadoSolicitada = db.EstadosCita.Single(e => e.NombreEstado == "Solicitada");
        var estadoConfirmada = new EstadoCita { NombreEstado = "Confirmada" };
        db.EstadosCita.Add(estadoConfirmada);
        var fecha = ProximaFecha(DayOfWeek.Monday).Date.AddHours(9);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdServicio = datos.Servicio.IdServicio,
            IdEstado = estadoSolicitada.IdEstado,
            FechaHora = fecha,
            Estado = "Solicitada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var actualizada = await CrearServicio(db).ConfirmarYAsignarCitaAsync(
            cita.IdCita,
            new CitaConfirmacionAsignacionDto
            {
                IdCita = cita.IdCita,
                IdProfesional = datos.Profesional.IdProfesional,
                IdConsultorio = datos.Consultorio.IdConsultorio,
                Fecha = fecha.Date,
                HoraInicio = fecha.TimeOfDay
            },
            idUsuarioActor: 43,
            ipOrigen: "127.0.0.2");

        Assert.Equal("Confirmada", actualizada?.Estado);
        var historial = await db.CitasHistorialEstado.SingleAsync();
        Assert.Equal(cita.IdCita, historial.IdCita);
        Assert.Equal(estadoConfirmada.IdEstado, historial.IdEstado);
        Assert.Equal(43, historial.IdUsuario);
        Assert.Contains("confirmada", historial.Motivo, StringComparison.OrdinalIgnoreCase);
        var auditoria = await db.Auditorias.SingleAsync();
        Assert.Equal(43, auditoria.IdUsuario);
        Assert.Equal("127.0.0.2", auditoria.IpOrigen);
        Assert.Contains("Solicitada", auditoria.DatosAnteriores);
        Assert.Contains("Confirmada", auditoria.DatosNuevos);
    }

    [Fact]
    public async Task ConfirmarYAsignarCitaAsync_RechazaRecursosQueYaEstanReservados()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var estadoConfirmada = new EstadoCita { NombreEstado = "Confirmada" };
        db.EstadosCita.Add(estadoConfirmada);
        var fecha = ProximaFecha(DayOfWeek.Monday).Date.AddHours(9);
        var otroPaciente = new Paciente { Nombres = "Luis", Apellidos = "Prueba", Estado = "activo" };
        db.Pacientes.Add(otroPaciente);
        await db.SaveChangesAsync();

        db.Citas.AddRange(
            new Cita
            {
                IdPaciente = datos.Paciente.IdPaciente,
                IdProfesional = datos.Profesional.IdProfesional,
                IdServicio = datos.Servicio.IdServicio,
                IdConsultorio = datos.Consultorio.IdConsultorio,
                IdEstado = estadoConfirmada.IdEstado,
                FechaHora = fecha,
                DuracionMinutos = 30,
                Estado = "Confirmada"
            },
            new Cita
            {
                IdPaciente = otroPaciente.IdPaciente,
                IdServicio = datos.Servicio.IdServicio,
                FechaHora = fecha.AddDays(1),
                Estado = "Solicitada"
            });
        await db.SaveChangesAsync();
        var solicitud = await db.Citas.SingleAsync(c => c.IdPaciente == otroPaciente.IdPaciente);

        var error = await Assert.ThrowsAsync<CitaConflictException>(() =>
            CrearServicio(db).ConfirmarYAsignarCitaAsync(solicitud.IdCita, new CitaConfirmacionAsignacionDto
            {
                IdCita = solicitud.IdCita,
                IdProfesional = datos.Profesional.IdProfesional,
                IdConsultorio = datos.Consultorio.IdConsultorio,
                Fecha = fecha.Date,
                HoraInicio = fecha.TimeOfDay
            }));

        Assert.Contains("profesional", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Solicitada", (await db.Citas.SingleAsync(c => c.IdCita == solicitud.IdCita)).Estado);
        Assert.Empty(await db.CitasHistorialEstado.ToListAsync());
        Assert.Empty(await db.Auditorias.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_AceptaAliasProgramadaCuandoElCatalogoUsaAgendada()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        DateTime fecha = ProximaFecha(DayOfWeek.Monday).AddHours(9);

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
        }, idUsuarioActor: 31, ipOrigen: "127.0.0.3");

        Assert.NotNull(cita);
        Assert.Equal("Agendada", cita.Estado);
        var historial = await db.CitasHistorialEstado.SingleAsync();
        Assert.Equal(cita.IdCita, historial.IdCita);
        Assert.Equal("Agendada", historial.EstadoTexto);
        var auditoria = await db.Auditorias.SingleAsync();
        Assert.Equal(cita.IdCita, auditoria.IdRegistro);
        Assert.Equal(31, auditoria.IdUsuario);
        Assert.Equal("127.0.0.3", auditoria.IpOrigen);
        Assert.Contains("Agendada", auditoria.DatosNuevos);
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
        DateTime fecha = ProximaFecha(DayOfWeek.Monday).AddHours(10);

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

        var excepcion = await Assert.ThrowsAnyAsync<InvalidOperationException>(accion);
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
            FechaHora = ProximaFecha(DayOfWeek.Monday).AddHours(10),
            Estado = "Programada"
        });

        var excepcion = await Assert.ThrowsAnyAsync<InvalidOperationException>(accion);
        Assert.Contains("especialidades", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearAsync_RechazaCitaDuranteAusenciaProfesional()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        DateTime fecha = ProximaFecha(DayOfWeek.Monday).AddHours(10);

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

        var excepcion = await Assert.ThrowsAnyAsync<InvalidOperationException>(accion);
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

        var fechaHabil = DateTime.Today.AddDays(1);
        while (fechaHabil.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            fechaHabil = fechaHabil.AddDays(1);

        var resultado = await CrearServicio(db).ValidarHorarioClinicaAsync(
            fechaHabil.Date.AddHours(17), 60);

        Assert.True(resultado.EsValido);
    }

    [Fact]
    public async Task ValidarHorarioClinica_AceptaSabadoCuandoLaConfiguracionLoIncluye()
    {
        await using var db = CrearDb();
        db.ConfiguracionesGenerales.AddRange(
            new ConfiguracionGeneral { Clave = "horario_apertura", Valor = "07:00" },
            new ConfiguracionGeneral { Clave = "horario_cierre", Valor = "18:00" },
            new ConfiguracionGeneral { Clave = "dias_atencion", Valor = "1,2,3,4,5,6" });
        await db.SaveChangesAsync();

        var fechaSabado = DateTime.Today.AddDays(1);
        while (fechaSabado.DayOfWeek != DayOfWeek.Saturday)
            fechaSabado = fechaSabado.AddDays(1);

        var resultado = await CrearServicio(db).ValidarHorarioClinicaAsync(
            fechaSabado.Date.AddHours(9), 60);

        Assert.True(resultado.EsValido);
    }

    [Fact]
    public async Task ValidarHorarioClinica_RechazaDomingoAunqueLaConfiguracionLoIncluya()
    {
        await using var db = CrearDb();
        db.ConfiguracionesGenerales.AddRange(
            new ConfiguracionGeneral { Clave = "horario_apertura", Valor = "07:00" },
            new ConfiguracionGeneral { Clave = "horario_cierre", Valor = "18:00" },
            new ConfiguracionGeneral { Clave = "dias_atencion", Valor = "1,2,3,4,5,6,7" });
        await db.SaveChangesAsync();

        var fecha = DateTime.Today.AddDays(1);
        while (fecha.DayOfWeek != DayOfWeek.Sunday)
            fecha = fecha.AddDays(1);

        var resultado = await CrearServicio(db).ValidarHorarioClinicaAsync(fecha.Date.AddHours(9), 60);

        Assert.False(resultado.EsValido);
        Assert.Contains("no tiene atención", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
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

        var fechaHabil = DateTime.Today.AddDays(1);
        while (fechaHabil.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            fechaHabil = fechaHabil.AddDays(1);

        var resultado = await CrearServicio(db).ValidarHorarioClinicaAsync(
            fechaHabil.Date.AddHours(17).AddMinutes(1), 60);

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
    public async Task CambiarEstadoAsync_GuardaHistorialConActorAlCambiarEstado()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var estadoConfirmada = new EstadoCita { NombreEstado = "Confirmada" };
        db.EstadosCita.Add(estadoConfirmada);
        await db.SaveChangesAsync();

        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            IdEstado = db.EstadosCita.Single(e => e.NombreEstado == "Programada").IdEstado,
            FechaHora = ColombiaClock.Now.AddDays(1),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        await CrearServicio(db).CambiarEstadoAsync(
            cita.IdCita,
            "Confirmada",
            idUsuarioActor: 42,
            ipOrigen: "192.0.2.1");

        var historial = await db.CitasHistorialEstado.SingleAsync();
        Assert.Equal(cita.IdCita, historial.IdCita);
        Assert.Equal(estadoConfirmada.IdEstado, historial.IdEstado);
        Assert.Equal("Confirmada", historial.EstadoTexto);
        Assert.Equal(42, historial.IdUsuario);
        Assert.Contains("Programada", historial.Motivo);
        Assert.Contains("Confirmada", historial.Motivo);

        var auditoria = await db.Auditorias.SingleAsync();
        Assert.Equal(42, auditoria.IdUsuario);
        Assert.Equal("192.0.2.1", auditoria.IpOrigen);
        Assert.Contains("Programada", auditoria.DatosAnteriores);
        Assert.Contains("Confirmada", auditoria.DatosNuevos);
    }

    [Fact]
    public async Task CambiarEstadoAsync_NoPersisteEstadoSiFallaElGuardadoDelHistorial()
    {
        string databaseName = Guid.NewGuid().ToString();
        await using var db = CrearDb(databaseName, new FallarSiSeAgregaHistorialInterceptor());
        var datos = SeedBase(db);
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Confirmada" });
        await db.SaveChangesAsync();

        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            IdEstado = db.EstadosCita.Single(e => e.NombreEstado == "Programada").IdEstado,
            FechaHora = ColombiaClock.Now.AddDays(1),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).CambiarEstadoAsync(cita.IdCita, "Confirmada"));

        await using var verificationDb = CrearDb(databaseName);
        var persistida = await verificationDb.Citas.SingleAsync(c => c.IdCita == cita.IdCita);
        Assert.Equal("Programada", persistida.Estado);
        Assert.Empty(await verificationDb.CitasHistorialEstado.ToListAsync());
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
        db.EstadosCita.AddRange(
            new EstadoCita { NombreEstado = "En sala de espera" },
            new EstadoCita { NombreEstado = "En consulta" });
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

        await CrearServicio(db).CambiarEstadoAsync(cita.IdCita, "En sala de espera");
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

    [Fact]
    public async Task ActualizarAsync_RegistraCambioDeEstadoConUsuarioEnLaMismaOperacion()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var estadoConfirmada = new EstadoCita { NombreEstado = "Confirmada" };
        db.EstadosCita.Add(estadoConfirmada);
        await db.SaveChangesAsync();

        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            IdEstado = db.EstadosCita.Single(e => e.NombreEstado == "Programada").IdEstado,
            FechaHora = ProximaFecha(DayOfWeek.Monday).AddHours(9),
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();

        var actualizada = await CrearServicio(db).ActualizarAsync(cita.IdCita, new CitaApiUpdateDto
        {
            IdCita = cita.IdCita,
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = cita.FechaHora,
            Estado = "Confirmada",
            IdEstado = estadoConfirmada.IdEstado,
            IdUsuarioModificacion = 42
        }, operadorId: 42, ipOrigen: "127.0.0.4");

        Assert.Equal("Confirmada", actualizada?.Estado);
        var historial = await db.CitasHistorialEstado.SingleAsync();
        Assert.Equal(cita.IdCita, historial.IdCita);
        Assert.Equal(estadoConfirmada.IdEstado, historial.IdEstado);
        Assert.Equal(42, historial.IdUsuario);
        Assert.Contains("Programada", historial.Motivo);
        Assert.Contains("Confirmada", historial.Motivo);
        var auditoria = await db.Auditorias.SingleAsync();
        Assert.Equal(42, auditoria.IdUsuario);
        Assert.Equal("127.0.0.4", auditoria.IpOrigen);
        Assert.Contains("Programada", auditoria.DatosAnteriores);
        Assert.Contains("Confirmada", auditoria.DatosNuevos);
    }

    [Fact]
    public async Task ActualizarAsync_ReprogramarInvalidaPendienteYConservaHistorialEnviado()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        DateTime fechaOriginal = ProximaFecha(DayOfWeek.Monday).AddHours(9);
        var cita = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = fechaOriginal,
            Estado = "Programada"
        };
        db.Citas.Add(cita);
        await db.SaveChangesAsync();
        Guid versionAnterior = cita.ProgramacionId;
        var enviado = new RecordatorioCita
        {
            IdCita = cita.IdCita, ProgramacionId = Guid.NewGuid(), Canal = "email", Estado = "enviado",
            ProgramadoPara = ColombiaClock.Now.AddDays(-1), EnviadoEn = ColombiaClock.Now.AddDays(-1),
            CreadoEn = ColombiaClock.Now.AddDays(-1)
        };
        var pendiente = new RecordatorioCita
        {
            IdCita = cita.IdCita, ProgramacionId = versionAnterior, Canal = "email", Estado = "pendiente",
            ProgramadoPara = ColombiaClock.Now, ProximoIntentoEn = ColombiaClock.Now,
            CreadoEn = ColombiaClock.Now
        };
        db.RecordatoriosCita.AddRange(enviado, pendiente);
        await db.SaveChangesAsync();

        await CrearServicio(db).ActualizarAsync(cita.IdCita, new CitaApiUpdateDto
        {
            IdCita = cita.IdCita,
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = fechaOriginal.AddHours(1),
            Estado = "Programada",
            IdEstado = db.EstadosCita.Single(e => e.NombreEstado == "Programada").IdEstado
        });

        var recordatorios = await db.RecordatoriosCita.OrderBy(r => r.IdRecordatorio).ToListAsync();
        Assert.Equal(3, recordatorios.Count);
        Assert.Equal("enviado", recordatorios[0].Estado);
        Assert.NotNull(recordatorios[0].EnviadoEn);
        Assert.Equal("cancelado", recordatorios[1].Estado);
        Assert.Null(recordatorios[1].ReservaId);
        Assert.Equal("pendiente", recordatorios[2].Estado);
        Assert.NotEqual(versionAnterior, recordatorios[2].ProgramacionId);
        Assert.Equal(cita.ProgramacionId, recordatorios[2].ProgramacionId);
    }

    private static CitaService CrearServicio(AppDbContext db) =>
        new(db, NullLogger<CitaService>.Instance);

    private static AppDbContext CrearDb(
        string? databaseName = null,
        ISaveChangesInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString());
        if (interceptor is not null)
            builder.AddInterceptors(interceptor);
        return new AppDbContext(builder.Options);
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
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Solicitada" });
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
    // AGENDAMIENTO DIRECTO POR PACIENTE
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
        var profesionalesDelServicio = await CrearServicio(db).ObtenerProfesionalesPorServicioAsync(
            datos.Servicio.IdServicio);
        Assert.Single(profesionalesDelServicio);
        Assert.Equal(datos.Profesional.IdProfesional, profesionalesDelServicio[0].IdProfesional);
    }

    [Fact]
    public async Task ObtenerProfesionalesPorServicioAsync_UsaGeneralComoFallbackSinEspecialidadExplicita()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);

        db.ProfesionalEspecialidades.RemoveRange(
            await db.ProfesionalEspecialidades
                .Where(pe => pe.IdProfesional == datos.Profesional.IdProfesional)
                .ToListAsync());
        await db.SaveChangesAsync();

        var profesionales = await CrearServicio(db)
            .ObtenerProfesionalesPorServicioAsync(datos.Servicio.IdServicio);

        Assert.Single(profesionales);
        Assert.Equal(datos.Profesional.IdProfesional, profesionales[0].IdProfesional);
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
        var excepcion = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
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
        var excepcion = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
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
    public async Task AgendarCitaPacienteAsync_RechazaFechaPasada()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var dto = new CitaSolicitudPacienteDto
        {
            Fecha = DateTime.Today.AddDays(-1),
            HoraInicio = TimeSpan.FromHours(9),
            IdServicio = datos.Servicio.IdServicio,
            IdProfesional = datos.Profesional.IdProfesional,
            Notas = "Prueba"
        };

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearServicio(db).AgendarCitaPacienteAsync(datos.Paciente.IdPaciente, dto));

        Assert.Contains("futuras", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.Citas.ToListAsync());
    }

    [Fact]
    public async Task AgendarCitaPacienteAsync_CreaSolicitudPendienteSinReservarRecursos()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var fecha = ProximaFecha(DayOfWeek.Monday);
        var dto = new CitaSolicitudPacienteDto
        {
            Fecha = fecha.Date,
            HoraInicio = TimeSpan.FromHours(9),
            IdServicio = datos.Servicio.IdServicio,
            IdProfesional = datos.Profesional.IdProfesional,
            Notas = "Muela del juicio"
        };

        var cita = await CrearServicio(db).AgendarCitaPacienteAsync(datos.Paciente.IdPaciente, dto);

        Assert.Equal(datos.Paciente.IdPaciente, cita.IdPaciente);
        Assert.Null(cita.IdProfesional);
        Assert.Equal(datos.Profesional.IdProfesional, cita.IdProfesionalPreferido);
        Assert.Null(cita.IdConsultorio);
        Assert.Equal(fecha.Date.AddHours(9), cita.FechaHora);
        Assert.Equal("Solicitada", cita.Estado);
        var historial = await db.CitasHistorialEstado.SingleAsync();
        Assert.Equal(cita.IdCita, historial.IdCita);
        Assert.Equal("Solicitada", historial.EstadoTexto);
    }

    [Fact]
    public async Task AgendarCitaPacienteAsync_AceptaSolicitudSinProfesionalPreferido()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var fecha = ProximaFecha(DayOfWeek.Monday);

        var solicitud = await CrearServicio(db).AgendarCitaPacienteAsync(
            datos.Paciente.IdPaciente,
            new CitaSolicitudPacienteDto
            {
                Fecha = fecha.Date,
                HoraInicio = TimeSpan.FromHours(9),
                IdServicio = datos.Servicio.IdServicio
            },
            idUsuarioActor: datos.Paciente.IdUsuario,
            ipOrigen: "127.0.0.5");

        Assert.Equal("Solicitada", solicitud.Estado);
        Assert.Null(solicitud.IdProfesional);
        Assert.Null(solicitud.IdProfesionalPreferido);
        Assert.Null(solicitud.IdConsultorio);
        var auditoria = await db.Auditorias.SingleAsync();
        Assert.Equal(datos.Paciente.IdUsuario, auditoria.IdUsuario);
        Assert.Equal("127.0.0.5", auditoria.IpOrigen);
        Assert.Contains("Solicitada", auditoria.DatosNuevos);
    }

    [Fact]
    public async Task RegistrarListaEsperaAsync_GuardaRangoYPreferenciaSinReservarCita()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        DateTime desde = ColombiaClock.Today.AddDays(1);
        DateTime hasta = desde.AddDays(7);

        var entrada = await CrearServicio(db).RegistrarListaEsperaAsync(
            datos.Paciente.IdPaciente,
            new ListaEsperaSolicitudDto
            {
                IdServicio = datos.Servicio.IdServicio,
                IdProfesionalPreferido = datos.Profesional.IdProfesional,
                FechaDeseadaInicio = desde,
                FechaDeseadaFin = hasta
            });

        Assert.Equal("pendiente", entrada.Estado);
        Assert.Equal(desde, entrada.FechaDeseadaInicio);
        Assert.Equal(hasta.AddDays(1).AddTicks(-1), entrada.FechaDeseadaFin);
        Assert.Null(entrada.IdCitaAsignada);
        Assert.Empty(await db.Citas.ToListAsync());
    }

    [Fact]
    public async Task CancelarAsync_AsignaAutomaticamenteElEspacioAlPrimerPacienteEnListaDeEspera()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Confirmada" });
        var fecha = ProximaFecha(DayOfWeek.Monday).Date.AddHours(9);
        var citaCancelada = new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = fecha,
            Estado = "Programada",
            DuracionMinutos = 60
        };
        var entrada = new ListaEsperaCita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdServicio = datos.Servicio.IdServicio,
            IdProfesionalPreferido = datos.Profesional.IdProfesional,
            FechaDeseadaInicio = fecha.Date,
            FechaDeseadaFin = fecha.Date.AddDays(1).AddTicks(-1),
            Estado = "pendiente"
        };
        db.AddRange(citaCancelada, entrada);
        await db.SaveChangesAsync();

        bool cancelada = await CrearServicio(db).CancelarAsync(
            citaCancelada.IdCita,
            "Paciente solicita cancelación");

        Assert.True(cancelada);
        var citaAsignada = await db.Citas.SingleAsync(c => c.IdCita != citaCancelada.IdCita);
        Assert.Equal("Confirmada", citaAsignada.Estado);
        Assert.Equal(datos.Profesional.IdProfesional, citaAsignada.IdProfesional);
        Assert.Equal(fecha, citaAsignada.FechaHora);
        Assert.Equal("asignada", entrada.Estado);
        Assert.Equal(citaAsignada.IdCita, entrada.IdCitaAsignada);
        var auditoriaAsignacion = await db.Auditorias.SingleAsync(a =>
            a.TablaAfectada == "Cita" && a.IdRegistro == citaAsignada.IdCita);
        Assert.Equal("INSERT", auditoriaAsignacion.Accion);
        Assert.Null(auditoriaAsignacion.IdUsuario);
        Assert.Equal("ListaEsperaAutomatica", auditoriaAsignacion.IpOrigen);
        Assert.Contains("Confirmada", auditoriaAsignacion.DatosNuevos);
        Assert.Collection(
            await db.CitasHistorialEstado.OrderBy(h => h.IdHistorial).ToListAsync(),
            historialCancelacion => Assert.Equal(citaCancelada.IdCita, historialCancelacion.IdCita),
            historialAsignacion => Assert.Equal(citaAsignada.IdCita, historialAsignacion.IdCita));
    }

    [Fact]
    public async Task ObtenerHorariosDisponiblesPacienteAsync_ExcluyeHorariosOcupados()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var fecha = ProximaFecha(DayOfWeek.Monday);
        db.Citas.Add(new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            FechaHora = fecha.Date.AddHours(9),
            DuracionMinutos = 60,
            Estado = "Programada"
        });
        await db.SaveChangesAsync();

        var horarios = await CrearServicio(db).ObtenerHorariosDisponiblesPacienteAsync(
            datos.Paciente.IdPaciente,
            datos.Servicio.IdServicio,
            datos.Profesional.IdProfesional,
            fecha);

        Assert.DoesNotContain(TimeSpan.FromHours(9), horarios);
        Assert.Contains(TimeSpan.FromHours(10), horarios);

        var solicitud = await CrearServicio(db).AgendarCitaPacienteAsync(
            datos.Paciente.IdPaciente,
            new CitaSolicitudPacienteDto
            {
                IdServicio = datos.Servicio.IdServicio,
                IdProfesional = datos.Profesional.IdProfesional,
                Fecha = fecha.Date,
                HoraInicio = TimeSpan.FromHours(9)
            });

        Assert.Equal("Solicitada", solicitud.Estado);
        Assert.Null(solicitud.IdProfesional);
        Assert.Null(solicitud.IdConsultorio);
        Assert.Equal(2, await db.Citas.CountAsync());
    }

    [Fact]
    public async Task ObtenerHorariosDisponiblesPacienteAsync_ExcluyeSoloLosHorariosAfectadosPorBloqueo()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var fecha = ProximaFecha(DayOfWeek.Monday).Date;
        db.BloqueosProfesional.Add(new BloqueoProfesional
        {
            IdProfesional = datos.Profesional.IdProfesional,
            FechaInicio = fecha.AddHours(9),
            FechaFin = fecha.AddHours(10)
        });
        await db.SaveChangesAsync();

        var horarios = await CrearServicio(db).ObtenerHorariosDisponiblesPacienteAsync(
            datos.Paciente.IdPaciente,
            datos.Servicio.IdServicio,
            datos.Profesional.IdProfesional,
            fecha);

        Assert.DoesNotContain(TimeSpan.FromHours(9), horarios);
        Assert.Contains(TimeSpan.FromHours(10), horarios);
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
        db.RecordatoriosCita.Add(new RecordatorioCita
        {
            IdCita = cita.IdCita,
            ProgramacionId = cita.ProgramacionId,
            Canal = "email",
            Estado = "pendiente",
            ProgramadoPara = ColombiaClock.Now,
            ProximoIntentoEn = ColombiaClock.Now,
            CreadoEn = ColombiaClock.Now
        });
        await db.SaveChangesAsync();

        // Act
        bool resultado = await CrearServicio(db).CancelarAsync(
            cita.IdCita,
            "Cancelación administrativa",
            idUsuarioActor: 17,
            ipOrigen: "192.0.2.2");

        // Assert
        Assert.True(resultado);
        var citaActualizada = await db.Citas.FindAsync(cita.IdCita);
        Assert.True(
            string.Equals(citaActualizada?.Estado, "Cancelada", StringComparison.OrdinalIgnoreCase),
            $"Se esperaba 'Cancelada', se obtuvo: '{citaActualizada?.Estado}'");
        Assert.Equal("Cancelación administrativa", citaActualizada?.MotivoCancelacion);
        var recordatorioCancelado = await db.RecordatoriosCita.SingleAsync();
        Assert.Equal("cancelado", recordatorioCancelado.Estado);
        Assert.Null(recordatorioCancelado.ReservaId);
        var historial = await db.CitasHistorialEstado.SingleAsync();
        Assert.Equal(cita.IdCita, historial.IdCita);
        Assert.Equal(17, historial.IdUsuario);
        Assert.Contains("Cancelación administrativa", historial.Motivo);
        var auditoria = await db.Auditorias.SingleAsync();
        Assert.Equal(17, auditoria.IdUsuario);
        Assert.Equal("192.0.2.2", auditoria.IpOrigen);
        Assert.Contains("Programada", auditoria.DatosAnteriores);
        using var snapshotNuevo = System.Text.Json.JsonDocument.Parse(auditoria.DatosNuevos!);
        Assert.Equal("Cancelada", snapshotNuevo.RootElement.GetProperty("Estado").GetString());
        Assert.Equal(
            "Cancelación administrativa",
            snapshotNuevo.RootElement.GetProperty("MotivoCancelacion").GetString());
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
    public async Task VerificarConflictoCompletoAsync_IgnoraCitasRechazadas()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        var estadoRechazada = new EstadoCita { NombreEstado = "Rechazada" };
        db.EstadosCita.Add(estadoRechazada);
        await db.SaveChangesAsync();
        var inicio = new DateTime(2026, 12, 10, 9, 0, 0);
        db.Citas.Add(new Cita
        {
            IdPaciente = datos.Paciente.IdPaciente,
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
            IdConsultorio = datos.Consultorio.IdConsultorio,
            IdEstado = estadoRechazada.IdEstado,
            EstadoCita = estadoRechazada,
            FechaHora = inicio,
            DuracionMinutos = 60,
            Estado = "Programada"
        });
        await db.SaveChangesAsync();

        var conflicto = await CrearServicio(db).VerificarConflictoCompletoAsync(
            datos.Profesional.IdProfesional,
            datos.Paciente.IdPaciente,
            datos.Consultorio.IdConsultorio,
            inicio.AddMinutes(15));

        Assert.False(conflicto.HayConflicto);
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

    [Fact]
    public async Task ObtenerKpisGestion_ProcesaMilCitasSinDegradacionExcesiva()
    {
        await using var db = CrearDb();
        var referencia = ColombiaClock.Now;
        var inicio = new DateTime(referencia.Year, referencia.Month, 1);

        var citas = Enumerable.Range(1, 1000).Select(index => new Cita
        {
            IdPaciente = ((index - 1) % 20) + 1,
            FechaHora = inicio.AddHours(((index - 1) % (27 * 24))),
            Estado = index % 4 == 0 ? "Cancelada" : "Programada",
            DuracionMinutos = 60
        });
        db.Citas.AddRange(citas);
        await db.SaveChangesAsync();

        var cronometro = Stopwatch.StartNew();
        var kpis = await CrearServicio(db).ObtenerKpisGestionAsync(ColombiaClock.Now);
        cronometro.Stop();

        Assert.Equal(1000, kpis.Total);
        Assert.True(cronometro.Elapsed < TimeSpan.FromSeconds(5),
            $"El cálculo de KPIs tardó {cronometro.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task HayConflictoHorario_AtiendeCienConsultasConsecutivasSinResultadosContradictorios()
    {
        const string dbName = "db_conflictos_concurrentes";
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var inicial = new AppDbContext(opciones))
        {
            inicial.Pacientes.Add(new Paciente { IdPaciente = 1, Nombres = "Ana", Estado = "activo" });
            inicial.Profesionales.Add(new Profesional
            {
                IdProfesional = 1,
                Nombres = "Carlos",
                Apellidos = "Ortiz",
                Estado = "activo",
                RegistroMedico = "RM-CONCURRENCIA"
            });
            await inicial.SaveChangesAsync();
        }

        var fecha = ColombiaClock.Now.AddDays(5).AddHours(10);
        var tareas = Enumerable.Range(1, 100).Select(async index =>
        {
            await using var contexto = new AppDbContext(opciones);
            var servicio = new CitaService(contexto, NullLogger<CitaService>.Instance);
            return await servicio.HayConflictoHorarioAsync(
                idProfesional: 1,
                fechaHora: fecha.AddMinutes(index % 3),
                ct: CancellationToken.None);
        });

        var resultados = await Task.WhenAll(tareas);
        Assert.All(resultados, resultado => Assert.False(resultado));
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
        Assert.Equal("pendiente", registro.Estado);
        Assert.Equal(1, registro.Intentos);
        Assert.True(registro.ProximoIntentoEn > ColombiaClock.Now);
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

        var (reenvios, fallidosReenvio, idsReenviados) =
            await service.EnviarRecordatoriosAsync([cita.IdCita]);

        Assert.Equal(0, reenvios);
        Assert.Equal(0, fallidosReenvio);
        Assert.Empty(idsReenviados);
        Assert.Single(await db.RecordatoriosCita.ToListAsync());
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

        public Task SendFacturaRecordatorioAsync(
            string recipientEmail,
            string nombrePaciente,
            IReadOnlyList<FacturaRecordatorioEmail> facturas,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(
            string correoDestino,
            CancellationToken cancellationToken = default)
            => Task.FromResult((true, "OK"));
    }

    private sealed class FallarSiSeAgregaHistorialInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            bool agregaHistorial = eventData.Context?.ChangeTracker
                .Entries<CitaHistorialEstado>()
                .Any(entry => entry.State == EntityState.Added) == true;

            if (agregaHistorial)
                throw new InvalidOperationException("Fallo intencional al guardar el historial.");

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
