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
    // =========================================================================
    // API REST - CRUD real de facturas (reemplaza la persistencia simulada
    // que existía en localStorage del lado del frontend)
    // =========================================================================

    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("facturacion-y-pagos/api/facturas/{id:int}")]
    public async Task<IActionResult> ApiObtenerFactura(int id, CancellationToken ct = default)
    {
        var factura = await _context.Facturas
            .AsNoTracking()
            .Include(f => f.Paciente)
            .Include(f => f.Detalles)
            .FirstOrDefaultAsync(f => f.IdFactura == id, ct);

        if (factura == null)
        {
            return NotFound(new { success = false, message = "Factura no encontrada." });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                id = factura.IdFactura,
                numero = factura.NumeroFactura,
                fecha = factura.FechaFactura,
                paciente = factura.Paciente != null
                    ? $"{factura.Paciente.Nombres} {factura.Paciente.Apellidos}"
                    : "Paciente",
                subtotal = factura.Subtotal,
                total = factura.Total,
                montoPagado = factura.MontoPagado,
                estado = factura.Estado,
                notas = factura.Notas,
                items = factura.Detalles.Select(d => new
                {
                    id = d.IdDetalle,
                    idServicio = d.IdServicio,
                    descripcion = d.Descripcion,
                    cantidad = d.Cantidad,
                    precioUnitario = d.PrecioUnitario,
                    subtotal = d.SubtotalLinea
                })
            }
        });
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("facturacion-y-pagos/api/facturas")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiCrearFactura(
        [FromBody] CrearFacturaRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            bool pacienteExiste = await _context.Pacientes.AnyAsync(
                p => p.IdPaciente == request.IdPaciente && p.Estado == "activo", ct);

            if (!pacienteExiste)
            {
                return BadRequest(new { success = false, message = "El paciente seleccionado no es válido." });
            }

            if (request.Items.Any(i => i.Cantidad <= 0 || i.PrecioUnitario < 0))
            {
                return BadRequest(new { success = false, message = "Uno o más ítems de la factura tienen valores inválidos." });
            }

            int? idOperador = ObtenerIdUsuarioActual();
            if (idOperador is null)
            {
                return Unauthorized(new { success = false, message = "No se pudo identificar al usuario que genera la factura." });
            }

            var detalles = request.Items.Select(i => new DetalleFactura
            {
                IdServicio = i.IdServicio,
                Descripcion = i.Descripcion.Trim(),
                Cantidad = i.Cantidad,
                PrecioUnitario = i.PrecioUnitario,
                SubtotalLinea = i.Cantidad * i.PrecioUnitario
            }).ToList();

            decimal total = detalles.Sum(d => d.SubtotalLinea);

            var factura = new Factura
            {
                NumeroFactura = await GenerarNumeroFacturaAsync(ct),
                FechaFactura = DateTime.Now,
                Subtotal = total,
                Total = total,
                Estado = "pendiente",
                IdPaciente = request.IdPaciente,
                Notas = string.IsNullOrWhiteSpace(request.Notas) ? null : request.Notas.Trim(),
                GeneradaPor = idOperador.Value,
                MontoPagado = 0,
                Detalles = detalles
            };

            _context.Facturas.Add(factura);
            await _context.SaveChangesAsync(ct);

            return Ok(new
            {
                success = true,
                message = "Factura registrada correctamente.",
                data = new { id = factura.IdFactura, numero = factura.NumeroFactura, total = factura.Total }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando factura para el paciente {IdPaciente}.", request.IdPaciente);
            return StatusCode(500, new { success = false, message = "No fue posible registrar la factura." });
        }
    }

    [HttpPut]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("facturacion-y-pagos/api/facturas/{id:int}/pago")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiRegistrarPago(
        int id,
        [FromBody] RegistrarPagoFacturaRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var factura = await _context.Facturas.FirstOrDefaultAsync(f => f.IdFactura == id, ct);
            if (factura == null)
            {
                return NotFound(new { success = false, message = "Factura no encontrada." });
            }

            if (factura.Estado == "anulada")
            {
                return BadRequest(new { success = false, message = "No se puede registrar un pago sobre una factura anulada." });
            }

            if (factura.Estado == "pagada")
            {
                return BadRequest(new { success = false, message = "La factura ya se encuentra pagada." });
            }

            decimal nuevoMontoPagado = factura.MontoPagado + request.MontoPagado;
            if (nuevoMontoPagado > factura.Total)
            {
                return BadRequest(new { success = false, message = "El monto pagado no puede superar el total de la factura." });
            }

            factura.MontoPagado = nuevoMontoPagado;
            factura.Estado = nuevoMontoPagado >= factura.Total ? "pagada" : "parcial";
            factura.FechaPago = DateTime.Now;

            await _context.SaveChangesAsync(ct);

            return Ok(new
            {
                success = true,
                message = "Pago registrado correctamente.",
                data = new { id = factura.IdFactura, estado = factura.Estado, montoPagado = factura.MontoPagado }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando pago de la factura {IdFactura}.", id);
            return StatusCode(500, new { success = false, message = "No fue posible registrar el pago." });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [Route("facturacion-y-pagos/api/facturas/{id:int}/anular")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiAnularFactura(
        int id,
        [FromBody] AnularFacturaRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var factura = await _context.Facturas.FirstOrDefaultAsync(f => f.IdFactura == id, ct);
            if (factura == null)
            {
                return NotFound(new { success = false, message = "Factura no encontrada." });
            }

            if (factura.Estado == "anulada")
            {
                return BadRequest(new { success = false, message = "La factura ya se encuentra anulada." });
            }

            factura.Estado = "anulada";
            factura.Notas = string.IsNullOrWhiteSpace(request?.Motivo)
                ? factura.Notas
                : $"{factura.Notas} | Anulada: {request.Motivo.Trim()}".Trim(' ', '|');

            await _context.SaveChangesAsync(ct);

            return Ok(new { success = true, message = "Factura anulada correctamente.", data = new { id = factura.IdFactura } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error anulando la factura {IdFactura}.", id);
            return StatusCode(500, new { success = false, message = "No fue posible anular la factura." });
        }
    }

    private async Task<string> GenerarNumeroFacturaAsync(CancellationToken ct)
    {
        int anio = DateTime.Now.Year;
        int consecutivo = await _context.Facturas.CountAsync(f => f.FechaFactura.Year == anio, ct) + 1;
        string candidato = $"FAC-{anio}-{consecutivo:D5}";

        // Evita colisiones si hubo facturas anuladas/borradas fuera de este flujo.
        while (await _context.Facturas.AnyAsync(f => f.NumeroFactura == candidato, ct))
        {
            consecutivo++;
            candidato = $"FAC-{anio}-{consecutivo:D5}";
        }

        return candidato;
    }

    private int? ObtenerIdUsuarioActual()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out int id) && id > 0 ? id : null;
    }
}