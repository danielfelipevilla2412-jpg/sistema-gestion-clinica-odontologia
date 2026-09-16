using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
    public FacturacionApiController(IFacturacionService service, AppDbContext db) { _service = service; _db = db; }

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
        try { var r=await _service.RegistrarPagoAsync(id,request,ct); return r==null?NotFound(new{success=false,message="Factura no encontrada."}):Ok(new{success=true,message="Pago registrado correctamente.",data=r}); }
        catch(InvalidOperationException ex){return BadRequest(new{success=false,message=ex.Message});}
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