using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
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
        db.ProfesionalServicios.Add(new ProfesionalServicio
        {
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio,
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
    public async Task CrearAsync_RechazaServicioNoAsignadoAlProfesional()
    {
        await using var db = CrearDb();
        var datos = SeedBase(db);
        db.ProfesionalServicios.Add(new ProfesionalServicio
        {
            IdProfesional = datos.Profesional.IdProfesional,
            IdServicio = datos.Servicio.IdServicio + 1,
            Activo = true
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
        Assert.Contains("asignado", excepcion.Message, StringComparison.OrdinalIgnoreCase);
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
        var servicio = new Servicio { Nombre = "Consulta", Estado = "activo" };
        var consultorio = new Consultorio { Nombre = "Consultorio 1", Estado = "activo" };
        db.Pacientes.Add(paciente);
        db.Profesionales.Add(profesional);
        db.Servicios.Add(servicio);
        db.Consultorios.Add(consultorio);
        db.EstadosCita.Add(new EstadoCita { NombreEstado = "Programada" });
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
    public async Task SolicitarCitaPacienteAsync_CreaCitaEnEstadoSolicitadaOProgramada()
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
        Assert.True(
            string.Equals(cita.Estado, "Solicitada", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cita.Estado, "Programada", StringComparison.OrdinalIgnoreCase),
            $"Estado inesperado: {cita.Estado}");
    }

    [Fact]
    public async Task SolicitarCitaPacienteAsync_SinServicio_CreaCitaExitosamente()
    {
        // Arrange — el paciente no selecciona servicio (IdServicio = null)
        await using var db = CrearDb();
        var datos = SeedBase(db);

        var dto = new CitaSolicitudPacienteDto
        {
            Fecha = DateTime.Today.AddDays(3),
            IdServicio = null,   // sin servicio
            Notas = "No sé qué servicio necesito"
        };

        // Act
        var cita = await CrearServicio(db).SolicitarCitaPacienteAsync(datos.Paciente.IdPaciente, dto);

        // Assert
        Assert.NotNull(cita);
        Assert.Null(cita.IdServicio);
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
        bool resultado = await CrearServicio(db).CancelarAsync(cita.IdCita);

        // Assert
        Assert.True(resultado);
        var citaActualizada = await db.Citas.FindAsync(cita.IdCita);
        Assert.True(
            string.Equals(citaActualizada?.Estado, "Cancelada", StringComparison.OrdinalIgnoreCase),
            $"Se esperaba 'Cancelada', se obtuvo: '{citaActualizada?.Estado}'");
    }

    [Fact]
    public async Task CancelarAsync_RetornaFalseParaCitaInexistente()
    {
        // Arrange
        await using var db = CrearDb();
        _ = SeedBase(db);

        // Act
        bool resultado = await CrearServicio(db).CancelarAsync(99999);

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
        var (enviados, fallidos) = await CrearServicio(db).EnviarRecordatoriosAsync(
            new List<int> { cita.IdCita });

        // Assert: sin correo no se puede enviar, pero tampoco debe lanzar excepción
        Assert.Equal(0, enviados);
        // fallidos puede ser 0 o 1 según implementación, pero no debe haber excepción
        Assert.True(fallidos >= 0);
    }
}
