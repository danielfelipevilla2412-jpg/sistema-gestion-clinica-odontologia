using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
            HoraFin = new TimeSpan(9, 30, 0),
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
}