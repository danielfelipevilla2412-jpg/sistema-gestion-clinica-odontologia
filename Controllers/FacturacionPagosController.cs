using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Controllers;

public class FacturacionPagosController : Controller
{
    private readonly AppDbContext _context;

    public FacturacionPagosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("facturacion-y-pagos/st-adm-12-facturacion")]
    public async Task<IActionResult> Stadm12Facturacion()
    {
        var facturasDb = await _context.Facturas
            .Include(f => f.Paciente)
            .OrderByDescending(f => f.FechaFactura)
            .ToListAsync();

        string[] colores = new[] { "blue", "green", "purple", "orange", "red" };
        var facturas = facturasDb.Select((f, idx) => new
        {
            id = f.IdFactura,
            number = f.NumeroFactura,
            patient = f.Paciente != null ? $"{f.Paciente.Nombres} {f.Paciente.Apellidos}" : "Paciente",
            doc = f.Paciente != null ? f.Paciente.Documento : "N/A",
            date = f.FechaFactura.ToString("yyyy-MM-dd"),
            total = f.Total,
            pending = f.Estado == "pagada" ? 0 : f.Estado == "parcial" ? f.Total / 2 : f.Total,
            status = f.Estado,
            avatar = f.Paciente != null ? $"{f.Paciente.Nombres.FirstOrDefault()}{f.Paciente.Apellidos.FirstOrDefault()}".ToUpper() : "PA",
            color = colores[idx % colores.Length],
            history = new object[] { }
        }).ToList();

        ViewData["FacturasJson"] = System.Text.Json.JsonSerializer.Serialize(facturas);
        return View("~/Views/Facturacion_Y_Pagos/st-adm-12-facturacion/gestionfacturacion.cshtml", facturas);
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("facturacion-y-pagos/st-adm-13-reportes-financieros")]
    public async Task<IActionResult> Stadm13ReportesFinancieros()
    {
        var facturasDb = await _context.Facturas
            .Include(f => f.Paciente)
            .OrderByDescending(f => f.FechaFactura)
            .ToListAsync();

        decimal income = facturasDb.Sum(f => f.Total);
        decimal received = facturasDb.Where(f => f.Estado == "pagada").Sum(f => f.Total)
                      + facturasDb.Where(f => f.Estado == "parcial").Sum(f => f.Total / 2);
        decimal pending = income - received;
        decimal margin = income > 0 ? Math.Round(received / income * 100, 0) : 0;

        var hoy = DateTime.Today;
        string[] nombresMeses = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
        var barChart = new List<object>();
        for (int i = 5; i >= 0; i--)
        {
            var mesRef = hoy.AddMonths(-i);
            decimal totalMes = facturasDb
                .Where(f => f.FechaFactura.Year == mesRef.Year && f.FechaFactura.Month == mesRef.Month)
                .Sum(f => f.Total);
            barChart.Add(new
            {
                month = nombresMeses[mesRef.Month - 1],
                value = totalMes,
                label = totalMes >= 1000000 ? $"${totalMes / 1000000:0.#}M" : $"${totalMes:0}",
                active = i == 0
            });
        }

        string[] coloresDonut = new[] { "#2563eb", "#16a34a", "#d97706", "#7c3aed", "#dc2626", "#0891b2" };
        var donutChart = facturasDb
            .GroupBy(f => string.IsNullOrWhiteSpace(f.Notas) ? "Servicios Generales" : f.Notas)
            .Select((g, idx) => new
            {
                label = g.Key,
                value = income > 0 ? (int)Math.Round(g.Sum(f => f.Total) / income * 100) : 0,
                color = coloresDonut[idx % coloresDonut.Length]
            })
            .ToList();

        string[] coloresAvatar = new[] { "blue", "green", "purple", "orange", "red" };
        var transactions = facturasDb.Select((f, idx) => new
        {
            id = f.IdFactura,
            number = f.NumeroFactura,
            patient = f.Paciente != null ? $"{f.Paciente.Nombres} {f.Paciente.Apellidos}" : "Paciente",
            service = string.IsNullOrWhiteSpace(f.Notas) ? "Servicio Odontológico" : f.Notas,
            date = f.FechaFactura.ToString("yyyy-MM-dd"),
            amount = f.Total,
            status = f.Estado == "pagada" ? "pagado" : f.Estado == "anulada" ? "anulado" : "pendiente",
            avatar = f.Paciente != null ? $"{f.Paciente.Nombres.FirstOrDefault()}{f.Paciente.Apellidos.FirstOrDefault()}".ToUpper() : "PA",
            color = coloresAvatar[idx % coloresAvatar.Length]
        }).ToList();

        var reporte = new
        {
            kpis = new { income, received, pending, margin },
            barChart,
            donutChart,
            transactions
        };

        ViewData["ReportesFinancierosJson"] = System.Text.Json.JsonSerializer.Serialize(reporte);
        ViewData["TotalIngresos"] = income;
        ViewData["TotalFacturas"] = facturasDb.Count;
        return View("~/Views/Facturacion_Y_Pagos/st-adm-13-reportes-financieros/index.cshtml");
    }

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("facturacion-y-pagos/st-rec-04-generar-factura")]
    public async Task<IActionResult> Strec04GenerarFactura()
    {
        var pacientes = await _context.Pacientes.Where(p => p.Estado == "activo").ToListAsync();
        var servicios = await _context.Servicios.Where(s => s.Estado == "activo").ToListAsync();
        ViewData["Pacientes"] = pacientes;
        ViewData["Servicios"] = servicios;
        return View("~/Views/Facturacion_Y_Pagos/st-rec-04-generar-factura/index.cshtml");
    }
}