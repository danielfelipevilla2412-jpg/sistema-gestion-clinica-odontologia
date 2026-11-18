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
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
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

    private static ReportesController CrearControladorProfesional(AppDbContext db)
    {
        var controller = new ReportesController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Name, "Dra. Ana Profesional"),
                    new Claim(ClaimTypes.Role, "Profesional")
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
        db.Citas.Add(new Cita
        {
            IdCita = 1,
            IdPaciente = 1,
            IdServicio = 1,
            FechaHora = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 2, 10, 0, 0),
            Estado = "Atendida"
        });
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
    public async Task VistaAdmin_SinDatos_MuestraMetricasEnCero()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.VistaAdmin(null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportesViewModel>(view.Model);
        Assert.Equal(0m, model.IngresosTotales);
        Assert.Equal(0, model.NuevosPacientes);
        Assert.Equal(0, model.CitasCompletadas);
        Assert.Null(model.VariacionIngresos);
        Assert.Null(model.VariacionPacientes);
        Assert.Empty(model.DistribucionServicios);
    }

    [Fact]
    public async Task VistaAdmin_CategoriaFinanciero_FiltraReportesRecientes()
    {
        await using var db = CrearContext();
        db.ReportesGenerados.Add(new ReporteGenerado
        {
            Nombre = "Ingresos del mes",
            Categoria = "Financiero",
            Periodo = DateTime.Today.ToString("yyyy-MM"),
            FechaGeneracionUtc = DateTime.UtcNow,
            DatosJson = JsonSerializer.Serialize(new ReporteGeneradoSnapshot
            {
                Nombre = "Ingresos del mes",
                Categoria = "Financiero",
                Periodo = DateTime.Today.ToString("yyyy-MM")
            })
        });
        db.ReportesGenerados.Add(new ReporteGenerado
        {
            Nombre = "Pacientes del mes",
            Categoria = "Pacientes",
            Periodo = DateTime.Today.ToString("yyyy-MM"),
            FechaGeneracionUtc = DateTime.UtcNow,
            DatosJson = "{}"
        });
        await db.SaveChangesAsync();
        var controller = CrearControlador(db);

        var result = await controller.VistaAdmin("Financiero");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportesViewModel>(view.Model);
        Assert.Equal("financiero", model.FiltroCategoria);
        var reporte = Assert.Single(model.ReportesRecientes);
        Assert.Equal("Ingresos del mes", reporte.Nombre);
        Assert.Equal("Financiero", reporte.Categoria);
    }

    [Fact]
    public async Task GenerarReporte_PersisteMetricasAgregadasYPermiteDescargasCsvYPdf()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, "Ana", "López"));
        db.Facturas.Add(new Factura
        {
            IdFactura = 1,
            NumeroFactura = "FAC-TEST-1",
            IdPaciente = 1,
            FechaFactura = DateTime.Today,
            Total = 123000m,
            Estado = "pagada"
        });
        await db.SaveChangesAsync();
        var controller = CrearControlador(db);

        var result = await controller.GenerarReporte(new ReporteGeneradoRequest
        {
            Nombre = "Resumen financiero",
            Categoria = "Financiero",
            Periodo = DateTime.Today.ToString("yyyy-MM"),
            Notas = "Validación aislada"
        });

        var response = Assert.IsType<OkObjectResult>(result);
        var reporte = await db.ReportesGenerados.SingleAsync();
        var snapshot = JsonSerializer.Deserialize<ReporteGeneradoSnapshot>(reporte.DatosJson);
        Assert.NotNull(snapshot);
        Assert.Contains(snapshot!.Metricas, metric => metric.Indicador == "Ingresos pagados");
        Assert.DoesNotContain("Ana", reporte.DatosJson);
        Assert.Equal(1, reporte.GeneradoPor);
        Assert.Equal(200, response.StatusCode);

        var csvResult = await controller.DescargarReporte(reporte.IdReporte, "csv");
        var csv = Assert.IsType<FileContentResult>(csvResult);
        Assert.Equal("text/csv; charset=utf-8", csv.ContentType);
        Assert.Contains("Ingresos pagados", Encoding.UTF8.GetString(csv.FileContents));

        var pdfResult = await controller.DescargarReporte(reporte.IdReporte, "pdf");
        var pdf = Assert.IsType<FileContentResult>(pdfResult);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf.FileContents, 0, 4));
    }

    [Fact]
    public async Task GenerarReporte_Logistica_IncluyeMetricasRealesDeInventario()
    {
        await using var db = CrearContext();
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 50000m, Estado = "activo", Categoria = "general" });
        db.Inventarios.AddRange(
            new Inventario
            {
                IdItem = 1,
                Codigo = "INS-1",
                Nombre = "Gasa",
                StockActual = 2,
                StockMinimo = 5,
                PrecioUnitario = 100m
            },
            new Inventario
            {
                IdItem = 2,
                Codigo = "INS-2",
                Nombre = "Guantes",
                StockActual = 10,
                StockMinimo = 5,
                PrecioUnitario = 50m
            });
        await db.SaveChangesAsync();
        var controller = CrearControlador(db);

        var result = await controller.GenerarReporte(new ReporteGeneradoRequest
        {
            Nombre = "Inventario del mes",
            Categoria = "Logística",
            Periodo = DateTime.Today.ToString("yyyy-MM")
        });

        Assert.IsType<OkObjectResult>(result);
        var reporte = await db.ReportesGenerados.SingleAsync();
        var snapshot = JsonSerializer.Deserialize<ReporteGeneradoSnapshot>(reporte.DatosJson);
        Assert.NotNull(snapshot);
        Assert.Contains(snapshot!.Metricas, metric => metric.Indicador == "Servicios activos" && metric.Valor == "1");
        Assert.Contains(snapshot.Metricas, metric => metric.Indicador == "Artículos de inventario" && metric.Valor == "2");
        Assert.Contains(snapshot.Metricas, metric => metric.Indicador == "Artículos bajo el mínimo" && metric.Valor == "1");
        Assert.Contains(snapshot.Metricas, metric => metric.Indicador == "Valor estimado del inventario" && metric.Valor.Contains("700"));
    }

    [Fact]
    public async Task GenerarReporte_RechazaPeriodoFuturo()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.GenerarReporte(new ReporteGeneradoRequest
        {
            Nombre = "Reporte futuro",
            Categoria = "Financiero",
            Periodo = DateTime.Today.AddMonths(1).ToString("yyyy-MM")
        });

        Assert.IsType<ObjectResult>(result);
        Assert.Empty(await db.ReportesGenerados.ToListAsync());
    }

    [Fact]
    public async Task VistaProf_ConCitas_RetornaViewConModelo()
    {
        await using var db = CrearContext();
        var ahora = SmileTrack_MVC.Helpers.ColombiaClock.Now;
        var mesAnterior = new DateTime(ahora.Year, ahora.Month, 1).AddMonths(-1).AddDays(1).AddHours(9);
        db.Profesionales.Add(new Profesional
        {
            IdProfesional = 1,
            IdUsuario = 1,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "RM-1",
            Estado = "activo"
        });
        db.Profesionales.Add(new Profesional
        {
            IdProfesional = 2,
            IdUsuario = 2,
            Nombres = "Otro",
            Apellidos = "Profesional",
            RegistroMedico = "RM-2",
            Estado = "activo"
        });
        db.Pacientes.Add(CrearPaciente(1, "Ana", "López"));
        db.Pacientes.Add(CrearPaciente(2, "Paciente", "Ajeno"));
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Limpieza", Precio = 50000m, Estado = "activo", Categoria = "general" });
        db.Servicios.Add(new Servicio { IdServicio = 2, Nombre = "Ortodoncia", Precio = 80000m, Estado = "activo", Categoria = "general" });
        db.Citas.AddRange(
            new Cita { IdCita = 1, IdPaciente = 1, IdProfesional = 1, IdServicio = 1, FechaHora = ahora.Date.AddHours(9), Estado = "Atendida" },
            new Cita { IdCita = 2, IdPaciente = 2, IdProfesional = 2, IdServicio = 2, FechaHora = ahora.Date.AddHours(10), Estado = "Atendida" },
            new Cita { IdCita = 3, IdPaciente = 1, IdProfesional = 1, IdServicio = 1, FechaHora = mesAnterior, Estado = "Cancelada" });
        db.Facturas.AddRange(
            new Factura { IdFactura = 1, NumeroFactura = "FAC-PROF-1", IdPaciente = 1, IdProfesional = 1, FechaFactura = ahora.Date, Total = 40000m, Estado = "pagada" },
            new Factura { IdFactura = 2, NumeroFactura = "FAC-PROF-2", IdPaciente = 2, IdProfesional = 2, FechaFactura = ahora.Date, Total = 250000m, Estado = "pagada" });
        await db.SaveChangesAsync();

        var controller = CrearControladorProfesional(db);

        var result = await controller.VistaProf();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportesProfesionalViewModel>(view.Model);
        Assert.Equal("Dra. Ana Profesional", model.DoctorName);
        Assert.Equal(1, model.TotalCitas);
        Assert.Equal(1, model.PacientesUnicos);
        Assert.Equal(40000m, model.Ingresos);
        Assert.Equal(0, model.Cancelaciones);
        Assert.Equal("Limpieza", Assert.Single(model.Tratamientos).Nombre);
        Assert.All(model.UltimasCitas, cita => Assert.Equal("Ana López", cita.NombrePaciente));
        Assert.Contains(model.UltimasCitas, cita => cita.ClaveEstado == "reportesprof.status_completed");
        Assert.Contains(model.UltimasCitas, cita => cita.ClaveEstado == "reportesprof.status_cancelled");
        Assert.Contains(model.UltimasCitas, cita => cita.EstadoClase == "completada");
        Assert.Contains(model.UltimasCitas, cita => cita.EstadoClase == "cancelada");
        Assert.Equal("Ana López", Assert.Single(model.PacientesFrecuentes).Nombre);
    }

    [Fact]
    public async Task VistaProf_SinActividad_NoInventaIndicadores()
    {
        await using var db = CrearContext();
        db.Profesionales.Add(new Profesional
        {
            IdProfesional = 1,
            IdUsuario = 1,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "RM-1",
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var result = await CrearControladorProfesional(db).VistaProf();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReportesProfesionalViewModel>(view.Model);
        Assert.Equal(0, model.TotalCitas);
        Assert.Equal(0m, model.Ingresos);
        Assert.Equal(0, model.PacientesUnicos);
        Assert.Null(model.TendenciaCitas);
        Assert.Null(model.TendenciaIngresos);
        Assert.Empty(model.Tratamientos);
        Assert.Empty(model.PacientesFrecuentes);
        Assert.Empty(model.UltimasCitas);
        Assert.All(model.ProximosDias, dia => Assert.Equal(0, dia.CantidadCitas));
    }

    [Fact]
    public void VistaProf_RestringeLaRutaAlRolProfesional()
    {
        var attributes = typeof(ReportesController)
            .GetMethod(nameof(ReportesController.VistaProf))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false);
        var authorize = Assert.IsType<AuthorizeAttribute>(Assert.Single(attributes));

        Assert.Equal("Profesional", authorize.Roles);
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
