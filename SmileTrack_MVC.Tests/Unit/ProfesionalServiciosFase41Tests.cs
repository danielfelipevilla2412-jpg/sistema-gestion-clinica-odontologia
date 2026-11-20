using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ProfesionalServiciosFase41Tests
{
    private static AppDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Profesional Profesional, Servicio Servicio1, Servicio Servicio2)> PrepararAsync(
        AppDbContext db, bool segundaEspecialidadProfesional = false)
    {
        var profesional = new Profesional
        {
            Nombres = "Prueba",
            Apellidos = "Asignaciones",
            RegistroMedico = $"RM-{Guid.NewGuid():N}",
            Estado = "activo"
        };
        var especialidad1 = new Especialidad { Nombre = "Endodoncia" };
        var especialidad2 = new Especialidad { Nombre = "Ortodoncia" };
        db.Profesionales.Add(profesional);
        db.Especialidades.AddRange(especialidad1, especialidad2);
        await db.SaveChangesAsync();

        var servicio1 = new Servicio
        {
            Nombre = "Tratamiento endodóntico",
            Estado = "activo",
            IdEspecialidad = especialidad1.IdEspecialidad,
            Precio = 100000m
        };
        var servicio2 = new Servicio
        {
            Nombre = "Control ortodóntico",
            Estado = "activo",
            IdEspecialidad = especialidad2.IdEspecialidad,
            Precio = 80000m
        };
        db.Servicios.AddRange(servicio1, servicio2);
        db.ProfesionalEspecialidades.Add(new Profesional_Especialidad
        {
            IdProfesional = profesional.IdProfesional,
            IdEspecialidad = especialidad1.IdEspecialidad,
            Principal = true
        });
        if (segundaEspecialidadProfesional)
            db.ProfesionalEspecialidades.Add(new Profesional_Especialidad
            {
                IdProfesional = profesional.IdProfesional,
                IdEspecialidad = especialidad2.IdEspecialidad,
                Principal = false
            });
        await db.SaveChangesAsync();
        return (profesional, servicio1, servicio2);
    }

    [Fact]
    public async Task ActualizarServiciosAsync_RechazaEspecialidadIncompatibleSinPersistir()
    {
        await using var db = CrearContexto();
        var (profesional, _, servicio2) = await PrepararAsync(db);
        var sut = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);

        var resultado = await sut.ActualizarServiciosAsync(profesional.IdProfesional,
            new[] { new ServicioProfesionalAsignacionApiRequest { IdServicio = servicio2.IdServicio, Activo = true } });

        Assert.False(resultado.Success);
        Assert.Equal(422, resultado.ErrorStatusCode);
        Assert.Empty(await db.ProfesionalServicios.ToListAsync());
    }

    [Fact]
    public async Task ActualizarServiciosAsync_ActualizaPrecioSinDuplicarRelacion()
    {
        await using var db = CrearContexto();
        var (profesional, servicio1, _) = await PrepararAsync(db);
        db.ProfesionalServicios.Add(new ProfesionalServicio
        {
            IdProfesional = profesional.IdProfesional,
            IdServicio = servicio1.IdServicio,
            PrecioPersonalizado = 110000m,
            Activo = true
        });
        await db.SaveChangesAsync();
        var sut = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);

        var resultado = await sut.ActualizarServiciosAsync(profesional.IdProfesional,
            new[] { new ServicioProfesionalAsignacionApiRequest
            {
                IdServicio = servicio1.IdServicio, PrecioPersonalizado = 125000m, Activo = true
            } });

        Assert.True(resultado.Success, resultado.Message);
        var asignacion = Assert.Single(await db.ProfesionalServicios.ToListAsync());
        Assert.Equal(125000m, asignacion.PrecioPersonalizado);
        Assert.True(asignacion.Activo);
        var consulta = await sut.ObtenerServiciosAsync(profesional.IdProfesional);
        Assert.True(consulta.Success);
        Assert.Equal(125000m, Assert.Single(consulta.Data).PrecioEfectivo);
    }

    [Fact]
    public async Task ActualizarServiciosAsync_ListaVaciaDesactivaSinEliminar()
    {
        await using var db = CrearContexto();
        var (profesional, servicio1, _) = await PrepararAsync(db);
        db.ProfesionalServicios.Add(new ProfesionalServicio
        {
            IdProfesional = profesional.IdProfesional,
            IdServicio = servicio1.IdServicio,
            Activo = true
        });
        await db.SaveChangesAsync();
        var sut = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);

        var resultado = await sut.ActualizarServiciosAsync(profesional.IdProfesional,
            Array.Empty<ServicioProfesionalAsignacionApiRequest>());

        Assert.True(resultado.Success, resultado.Message);
        Assert.False(Assert.Single(await db.ProfesionalServicios.ToListAsync()).Activo);
        Assert.Empty((await sut.ObtenerServiciosAsync(profesional.IdProfesional)).Data);
    }

    [Fact]
    public async Task ActualizarServiciosAsync_AceptaEspecialidadSecundaria()
    {
        await using var db = CrearContexto();
        var (profesional, servicio1, servicio2) = await PrepararAsync(db, segundaEspecialidadProfesional: true);
        var sut = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);

        var resultado = await sut.ActualizarServiciosAsync(profesional.IdProfesional,
            new[]
            {
                new ServicioProfesionalAsignacionApiRequest { IdServicio = servicio1.IdServicio, Activo = true },
                new ServicioProfesionalAsignacionApiRequest { IdServicio = servicio2.IdServicio, Activo = true }
            });

        Assert.True(resultado.Success, resultado.Message);
        var consulta = await sut.ObtenerServiciosAsync(profesional.IdProfesional);
        Assert.True(consulta.Success);
        Assert.Equal(2, consulta.Data.Count);
        Assert.Equal(new[] { servicio1.IdServicio, servicio2.IdServicio }.OrderBy(x => x),
            consulta.Data.Select(s => s.IdServicio).OrderBy(x => x));
    }
}
