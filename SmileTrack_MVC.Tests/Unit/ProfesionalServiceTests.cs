using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ProfesionalServiceTests
{
    [Fact]
    public async Task ObtenerVistaMVCAsync_AplicaFiltroYPaginacion()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
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