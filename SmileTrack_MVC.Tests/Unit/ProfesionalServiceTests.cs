using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ProfesionalServiceTests
{
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
                    DiaSemana = "Miércoles",
                    Active = true,
                    Start = "08:00",
                    End = "12:00"
                },
                new HorarioSemanalApiRequest
                {
                    DiaSemana = "Sábado",
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
    public async Task CrearAsync_GeneraContrasenaAutomaticaCuandoNoSeEnvioUna()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Roles.Add(new Rol { NombreRol = "Profesional" });
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
    }

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