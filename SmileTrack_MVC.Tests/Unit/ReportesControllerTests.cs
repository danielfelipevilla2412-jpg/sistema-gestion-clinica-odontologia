using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ReportesControllerTests
{
    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ReportesController CrearControlador(AppDbContext db)
    {
        var controller = new ReportesController(db);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Name, "Admin Prueba"),
                    new Claim(ClaimTypes.Role, "Administrador")
                ], "TestAuth"))
            }
        };

        return controller;
    }

    private static Paciente CrearPaciente(int id, string nombres, string apellidos) => new()
    {
        IdPaciente = id,
        TipoDocumento = "CC",
        Documento = $"90{id:D2}",
        Nombres = nombres,
        Apellidos = apellidos,
        FechaNacimiento = new DateTime(1990, 1, 1),
        FechaRegistro = DateTime.Today,
        Estado = "activo"
    };

    [Fact]
    public async Task VistaAdmin_ConDatos_RetornaViewConModeloReal()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, "Ana", "López"));
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 50000m, Estado = "activo", Categoria = "general" });
        db.Citas.Add(new Cita { IdCita = 1, IdPaciente = 1, IdServicio = 1, FechaHora = DateTime.Today.AddDays(-1).AddHours(10), Estado = "Atendida" });
        db.Facturas.Add(new Factura { IdFactura = 1, NumeroFactura = "FAC-2026-00001", IdPaciente = 1, FechaFactura = DateTime.Today, Total = 123000m, Estado = "pagada" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.VistaAdmin(null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportesViewModel>(view.Model);
        Assert.Equal("Admin Prueba", model.NombreUsuario);
        Assert.Equal("Administrador", model.Rol);
        Assert.Equal(123000m, model.IngresosTotales);
        Assert.Equal(1, model.CitasCompletadas);
        Assert.Contains(model.DistribucionServicios, d => d.Nombre == "Consulta" && d.Cantidad == 1);
    }

    [Fact]
    public async Task VistaAdmin_SinDatos_UsaValoresPorDefecto()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.VistaAdmin(null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportesViewModel>(view.Model);
        Assert.Equal(45200000m, model.IngresosTotales);
        Assert.Equal(48, model.NuevosPacientes);
        Assert.Equal(342, model.CitasCompletadas);
        Assert.Empty(model.DistribucionServicios);
    }

    [Fact]
    public async Task VistaAdmin_CategoriaFinanciero_FiltraReportesRecientes()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.VistaAdmin("Financiero");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportesViewModel>(view.Model);
        Assert.Equal("financiero", model.FiltroCategoria);
        var reporte = Assert.Single(model.ReportesRecientes);
        Assert.Equal("Financiero", reporte.Categoria);
    }

    [Fact]
    public async Task VistaProf_ConCitas_RetornaViewConModelo()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, "Ana", "López"));
        db.Citas.Add(new Cita { IdCita = 1, IdPaciente = 1, FechaHora = DateTime.Today.AddHours(9), Estado = "Atendida" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.VistaProf();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportesProfesionalViewModel>(view.Model);
        Assert.Equal("Admin Prueba", model.DoctorName);
        Assert.Equal(1, model.TotalCitas);
        Assert.Single(model.UltimasCitas);
    }

    [Fact]
    public async Task VistaRecepcion_ConDatos_RetornaViewConAgendaYCatalogos()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, "Laura", "Díaz"));
        db.Profesionales.Add(new Profesional { IdProfesional = 1, Nombres = "Carlos", Apellidos = "Ruiz", RegistroMedico = "RM-1", Estado = "activo" });
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 50000m, Estado = "activo", Categoria = "general" });
        db.Citas.Add(new Cita { IdCita = 1, IdPaciente = 1, IdProfesional = 1, IdServicio = 1, FechaHora = DateTime.Today.AddHours(9), Estado = "Atendida" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.VistaRecepcion();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportesOperativosViewModel>(view.Model);
        Assert.Single(model.Agenda);
        Assert.Single(model.PacientesDisponibles);
        Assert.Single(model.ProfesionalesDisponibles);
        Assert.Single(model.ServiciosDisponibles);
    }

    [Fact]
    public void DescargarReporte_RetornaArchivoCsvConNombre()
    {
        var controller = CrearControlador(CrearContext());

        var result = controller.DescargarReporte(5);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", file.ContentType);
        Assert.StartsWith("Reporte_5_", file.FileDownloadName);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public void TodosProcedimientos_RedirigeAVistaAdmin()
    {
        var controller = CrearControlador(CrearContext());

        var result = controller.TodosProcedimientos();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("VistaAdmin", redirect.ActionName);
    }

    [Fact]
    public void TodosReportes_RedirigeAVistaAdmin()
    {
        var controller = CrearControlador(CrearContext());

        var result = controller.TodosReportes();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("VistaAdmin", redirect.ActionName);
    }
}
