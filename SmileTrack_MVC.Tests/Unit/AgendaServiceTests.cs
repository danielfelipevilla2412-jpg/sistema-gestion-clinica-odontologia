using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class AgendaServiceTests
{
    [Fact]
    public async Task ObtenerAgendaAsync_UsaLaDuracionRegistradaEnLaCita()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        var paciente = new Paciente { Nombres = "Ana", Apellidos = "Prueba", Estado = "activo" };
        var profesional = new Profesional { Nombres = "Dra.", Apellidos = "Prueba", Estado = "activo" };
        var servicio = new Servicio { Nombre = "Procedimiento", Estado = "activo" };
        var consultorio = new Consultorio { Nombre = "Consultorio 1", Estado = "activo" };
        var estado = new EstadoCita { NombreEstado = "Programada" };
        var estadoRechazada = new EstadoCita { NombreEstado = "Rechazada" };
        db.AddRange(paciente, profesional, servicio, consultorio, estado, estadoRechazada);
        await db.SaveChangesAsync();

        var fechaHora = DateTime.Today.AddDays(1).AddHours(9);
        db.Citas.Add(new Cita
        {
            IdPaciente = paciente.IdPaciente,
            IdProfesional = profesional.IdProfesional,
            IdServicio = servicio.IdServicio,
            IdConsultorio = consultorio.IdConsultorio,
            IdEstado = estado.IdEstado,
            Estado = estado.NombreEstado,
            FechaHora = fechaHora,
            DuracionMinutos = 90
        });
        db.Citas.Add(new Cita
        {
            IdPaciente = paciente.IdPaciente,
            IdProfesional = profesional.IdProfesional,
            IdServicio = servicio.IdServicio,
            IdConsultorio = consultorio.IdConsultorio,
            IdEstado = estadoRechazada.IdEstado,
            EstadoCita = estadoRechazada,
            Estado = estadoRechazada.NombreEstado,
            FechaHora = fechaHora.AddHours(2),
            DuracionMinutos = 60
        });
        await db.SaveChangesAsync();

        var agenda = new AgendaService(db, new CitaService(db, NullLogger<CitaService>.Instance));

        var resultado = await agenda.ObtenerAgendaAsync(fechaHora, null, null);

        var cita = resultado.Dias
            .Single(dia => dia.Fecha.Date == fechaHora.Date)
            .Citas.Single();
        Assert.Equal("10:30", cita.HoraFin);
    }

    [Fact]
    public async Task ObtenerAgendaAsync_NormalizaClaseEstadoParaVariantesDeEstado()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        var paciente = new Paciente { Nombres = "Ana", Apellidos = "Prueba", Estado = "activo" };
        var profesional = new Profesional { Nombres = "Dra.", Apellidos = "Prueba", Estado = "activo" };
        var servicio = new Servicio { Nombre = "Consulta", Estado = "activo" };
        db.AddRange(paciente, profesional, servicio);
        await db.SaveChangesAsync();

        var fecha = DateTime.Today.AddDays(1).Date;
        db.Citas.AddRange(
            new Cita { IdPaciente = paciente.IdPaciente, IdProfesional = profesional.IdProfesional, IdServicio = servicio.IdServicio, Estado = "Atendida", FechaHora = fecha.AddHours(8), DuracionMinutos = 30 },
            new Cita { IdPaciente = paciente.IdPaciente, IdProfesional = profesional.IdProfesional, IdServicio = servicio.IdServicio, Estado = "Completada", FechaHora = fecha.AddHours(9), DuracionMinutos = 30 },
            new Cita { IdPaciente = paciente.IdPaciente, IdProfesional = profesional.IdProfesional, IdServicio = servicio.IdServicio, Estado = "No asistió", FechaHora = fecha.AddHours(10), DuracionMinutos = 30 },
            new Cita { IdPaciente = paciente.IdPaciente, IdProfesional = profesional.IdProfesional, IdServicio = servicio.IdServicio, Estado = "Confirmada", FechaHora = fecha.AddHours(11), DuracionMinutos = 30 },
            new Cita { IdPaciente = paciente.IdPaciente, IdProfesional = profesional.IdProfesional, IdServicio = servicio.IdServicio, Estado = "Programada", FechaHora = fecha.AddHours(12), DuracionMinutos = 30 });
        await db.SaveChangesAsync();

        var agenda = new AgendaService(db, new CitaService(db, NullLogger<CitaService>.Instance));

        var resultado = await agenda.ObtenerAgendaAsync(fecha, null, null);

        var clases = resultado.Dias
            .Single(dia => dia.Fecha.Date == fecha.Date)
            .Citas
            .ToDictionary(c => c.Hora, c => c.ClaseEstado);

        Assert.Equal("attended", clases["08:00"]);
        Assert.Equal("attended", clases["09:00"]);
        Assert.Equal("cancelled", clases["10:00"]);
        Assert.Equal("confirmed", clases["11:00"]);
        Assert.Equal("reserved", clases["12:00"]);
    }

    [Fact]
    public async Task ObtenerAgendaAsync_MuestraLasCitasAunqueElDiaEsteMarcadoComoCerrado()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        var paciente = new Paciente { Nombres = "Ana", Apellidos = "Prueba", Estado = "activo" };
        var profesional = new Profesional { Nombres = "Dra.", Apellidos = "Prueba", Estado = "activo" };
        var servicio = new Servicio { Nombre = "Consulta", Estado = "activo" };
        var estado = new EstadoCita { NombreEstado = "Programada" };
        db.AddRange(paciente, profesional, servicio, estado);
        db.ConfiguracionesGenerales.Add(new ConfiguracionGeneral
        {
            Clave = "dias_atencion",
            Valor = "1,2,3,4,5,6"
        });
        await db.SaveChangesAsync();

        var domingo = DateTime.Today.AddDays(
            ((int)DayOfWeek.Sunday - (int)DateTime.Today.DayOfWeek + 7) % 7);
        db.Citas.Add(new Cita
        {
            IdPaciente = paciente.IdPaciente,
            IdProfesional = profesional.IdProfesional,
            IdServicio = servicio.IdServicio,
            IdEstado = estado.IdEstado,
            Estado = estado.NombreEstado,
            FechaHora = domingo.AddHours(9),
            DuracionMinutos = 60
        });
        await db.SaveChangesAsync();

        var agenda = new AgendaService(db, new CitaService(db, NullLogger<CitaService>.Instance));

        var resultado = await agenda.ObtenerAgendaAsync(domingo, null, null);

        var diaCerrado = Assert.Single(resultado.Dias, dia => dia.Fecha.Date == domingo.Date);
        Assert.True(diaCerrado.Cerrado);
        Assert.Single(diaCerrado.Citas);
    }
}
