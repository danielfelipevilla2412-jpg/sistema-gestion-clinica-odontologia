    using System.Security.Claims;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using SmileTrack_MVC.Data;
    using SmileTrack_MVC.Models.Entities;
    using SmileTrack_MVC.Models.ViewModels;

    namespace SmileTrack_MVC.Controllers; 

    public class FacturacionPagosController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FacturacionPagosController> _logger;


        public FacturacionPagosController(AppDbContext context, ILogger<FacturacionPagosController> logger)
        {
            _context = context;
            _logger = logger;
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
