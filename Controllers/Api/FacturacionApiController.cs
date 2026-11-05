using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Facturacion;
using SmileTrack_MVC.Services.Facturacion;

namespace SmileTrack_MVC.Controllers.Api.Facturacion;

[ApiController]
[Route("api/facturas")]
[Authorize(Roles = "Administrador,Recepcionista")]
public sealed class FacturacionApiController : ControllerBase
{
    private readonly IFacturacionService _service;
    private readonly AppDbContext _db;
    private readonly ILogger<FacturacionApiController> _logger;
    public FacturacionApiController(IFacturacionService service, AppDbContext db, ILogger<FacturacionApiController> logger)
    {
        _service = service;
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // CRUD DE FACTURACIÓN
    // ============================================================
    // Aquí se encuentran las operaciones principales del CRUD:
    // CREATE  -> Crear
    // READ    -> Listar / Obtener
    // UPDATE  -> Actualizar
    // DELETE  -> Eliminar
    // ============================================================

    #region CRUD - READ
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? buscar, [FromQuery] string? estado, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
        => Ok(new { success = true, data = await _service.ListarAsync(buscar, estado, desde, hasta, ct) });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id, CancellationToken ct) { var r = await _service.ObtenerAsync(id, ct); return r == null ? NotFound(new { success=false, message="Factura no encontrada." }) : Ok(new { success=true, data=r }); }
    #endregion

    #region CRUD - CREATE
    [HttpPost("registrar-con-pago")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearConPago([FromBody] CrearFacturaConPagoRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(uid, out var idUsuario)) return Unauthorized(new { success=false, message="No se pudo identificar al usuario." });
        try
        {
            var result = await _service.CrearConPagoAsync(request, idUsuario, ct);
            return CreatedAtAction(nameof(Obtener), new { id = result.Id }, new { success=true, message="Factura y pago registrados correctamente.", data=result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success=false, message=ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo guardar la factura con el pago inicial.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "No se pudo guardar la factura y el abono por un error de base de datos. No se registraron los cambios; contacte al administrador."
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([FromBody] CrearFacturaRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier); if (!int.TryParse(uid, out var idUsuario)) return Unauthorized(new { success=false, message="No se pudo identificar al usuario." });
        try { var r = await _service.CrearAsync(request, idUsuario, ct); return CreatedAtAction(nameof(Obtener), new { id = r.Id }, new { success=true, message="Factura creada correctamente.", data=r }); }
        catch (InvalidOperationException ex) { return BadRequest(new { success=false, message=ex.Message }); }
    }
    #endregion

    #region CRUD - UPDATE
    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarFacturaRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try { var r = await _service.ActualizarAsync(id, request, ct); return r == null ? NotFound(new { success=false, message="Factura no encontrada." }) : Ok(new { success=true, message="Factura actualizada correctamente.", data=r }); }
        catch (InvalidOperationException ex) { return BadRequest(new { success=false, message=ex.Message }); }
    }
    #endregion

    #region CRUD - DELETE
    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        try { return await _service.EliminarAsync(id, ct) ? Ok(new { success=true, message="Factura eliminada correctamente." }) : NotFound(new { success=false, message="Factura no encontrada." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { success=false, message=ex.Message }); }
    }
    #endregion

    // ============================================================
    // OPERACIONES COMPLEMENTARIAS DE FACTURACIÓN
    // ============================================================
    // Estas operaciones no forman parte del CRUD básico:
    // registro de pagos, anulación y catálogos para las vistas.
    // ============================================================

    #region Operaciones complementarias
    [HttpPost("{id:int}/pagos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pago(int id, [FromBody] RegistrarPagoRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(uid, out var idUsuario)) return Unauthorized(new { success = false, message = "No se pudo identificar al usuario." });
        try { var r=await _service.RegistrarPagoAsync(id,request,idUsuario,ct); return r==null?NotFound(new{success=false,message="Factura no encontrada."}):Ok(new{success=true,message="Pago registrado correctamente.",data=r}); }
        catch(InvalidOperationException ex){return BadRequest(new{success=false,message=ex.Message});}
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "No se pudo guardar el abono de la factura {FacturaId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "No se pudo guardar el abono por un error de base de datos. No se registraron los cambios; contacte al administrador."
            });
        }
    }

    [HttpGet("{id:int}/pagos")]
    public async Task<IActionResult> Pagos(int id, CancellationToken ct)
    {
        var pagos = await _service.ListarPagosAsync(id, ct);
        return pagos is null
            ? NotFound(new { success = false, message = "Factura no encontrada." })
            : Ok(new { success = true, data = pagos });
    }

    [HttpPost("{id:int}/anulacion")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Anular(int id, [FromBody] AnularFacturaRequest? request, CancellationToken ct)
    {
        try { var r=await _service.AnularAsync(id,request,ct); return r==null?NotFound(new{success=false,message="Factura no encontrada."}):Ok(new{success=true,message="Factura anulada correctamente.",data=r}); }
        catch(InvalidOperationException ex){return BadRequest(new{success=false,message=ex.Message});}
    }

    [HttpGet("catalogos/pacientes")]
    public async Task<IActionResult> Pacientes(CancellationToken ct) => Ok(new { success=true, data=await _db.Pacientes.AsNoTracking().Where(x=>x.Estado=="activo").Select(x=>new { id=x.IdPaciente, nombre=x.Nombres+" "+x.Apellidos, documento=x.TipoDocumento+" "+x.Documento }).OrderBy(x=>x.nombre).ToListAsync(ct) });

    [HttpGet("catalogos/servicios")]
    public async Task<IActionResult> Servicios(CancellationToken ct) => Ok(new { success=true, data=await _db.Servicios.AsNoTracking().Where(x=>x.Estado=="activo").Select(x=>new { id=x.IdServicio, nombre=x.Nombre, descripcion=x.Descripcion, precio=x.Precio }).OrderBy(x=>x.nombre).ToListAsync(ct) });
#endregion
} 