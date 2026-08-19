using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Models.ViewModels;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SmileTrack_MVC.Controllers;

public partial class GestionProfesionalesController(AppDbContext context, ILogger<GestionProfesionalesController> logger) : Controller
{
    private readonly AppDbContext _context = context;
    private readonly ILogger<GestionProfesionalesController> _logger = logger;

    private const string MensajeErrorFallback =
        "Ocurrió un error inesperado al cargar la página. Por favor intente nuevamente. Si el problema persiste, contacte al soporte.";

    private static bool EsTelefonoValido(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return false;
        string digitsOnly = new string(telefono.Where(char.IsDigit).ToArray());
        return digitsOnly.Length is >= 7 and <= 15;
    }

    private static bool EsRegistroMedicoValido(string? registro)
    {
        if (string.IsNullOrWhiteSpace(registro)) return false;
        registro = registro.Trim();
        if (registro.Length < 3 || registro.Length > 30) return false;
        return RegistroMedicoRegex().IsMatch(registro);
    }

    [GeneratedRegex(@"^[A-Za-z0-9\-\. ]+$")]
    private static partial Regex RegistroMedicoRegex();

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-profesionales/st-adm-07-gestion-profesionales")]
    public async Task<IActionResult> Stadm07GestionProfesionales([FromQuery] int? editId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? especialidad = null, [FromQuery] string? estado = null, CancellationToken ct = default)
    {
        try
        {
            await CargarDatosProfesionales(editId, BuildReturnUrl(), new PaginationQuery
            {
                Page = page,
                PageSize = pageSize,
                Search = search,
                Estado = estado,
                Profesional = especialidad
            }, ct);
            return View("~/Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando Stadm07GestionProfesionales (pagina={Pagina}, search={Search})", page, search);
            TempData["ErrorValidacion"] = MensajeErrorFallback;
            return View("~/Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/index.cshtml");
        }
    }

    [HttpGet]
    [Authorize(Roles = "Administrador,Profesional")]
    [Route("gestion-de-profesionales/st-adm-14-reportes-clinicos")]
    public async Task<IActionResult> Stadm14ReportesClinicos([FromQuery] int? editId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? profesional = null, [FromQuery] string? mes = null, CancellationToken ct = default)
    {
        try
        {
            await CargarDatosProfesionales(editId, BuildReturnUrl(), null, ct);
            await CargarDatosReportesClinicos(page, pageSize, search, profesional, mes, ct);
            return View("~/Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando Stadm14ReportesClinicos (pagina={Pagina}, mes={Mes})", page, mes);
            TempData["ErrorValidacion"] = MensajeErrorFallback;
            return View("~/Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml");
        }
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("gestion-de-profesionales/st-odo-01-dashboard")]
    public async Task<IActionResult> Stodo01Dashboard([FromQuery] int? editId, CancellationToken ct = default)
    {
        try
        {
            await CargarDatosProfesionales(editId, BuildReturnUrl(), null, ct);

            string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                var profesional = await _context.Profesionales.FirstOrDefaultAsync(p => p.IdUsuario == userId, ct);
                if (profesional != null)
                {
                    var hoy = DateTime.Today;
                    var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
                    var finMes = inicioMes.AddMonths(1);

                    var citasDelMes = await _context.Citas
                        .Include(c => c.Servicio)
                        .Where(c => c.IdProfesional == profesional.IdProfesional && c.FechaHora >= inicioMes && c.FechaHora < finMes)
                        .AsNoTracking()
                        .ToListAsync(ct);

                    ViewData["OdoPacientesActivos"] = citasDelMes.Select(c => c.IdPaciente).Distinct().Count();
                    ViewData["OdoCitasHoy"] = citasDelMes.Count(c => c.FechaHora.Date == hoy);

                    decimal ingresosMes = citasDelMes
                        .Where(c => string.Equals(c.Estado, "Atendida", StringComparison.OrdinalIgnoreCase) && c.Servicio != null)
                        .Sum(c => c.Servicio!.Precio);
                    ViewData["OdoIngresosMes"] = ingresosMes;

                    ViewData["OdoCitasAtendidas"] = citasDelMes.Count(c => string.Equals(c.Estado, "Atendida", StringComparison.OrdinalIgnoreCase));
                    ViewData["OdoCitasProgramadas"] = citasDelMes.Count(c => string.Equals(c.Estado, "Agendada", StringComparison.OrdinalIgnoreCase) || string.Equals(c.Estado, "programada", StringComparison.OrdinalIgnoreCase));
                    ViewData["OdoCitasCanceladas"] = citasDelMes.Count(c => string.Equals(c.Estado, "Cancelada", StringComparison.OrdinalIgnoreCase));
                    ViewData["OdoCitasNoAsistio"] = citasDelMes.Count(c => string.Equals(c.Estado, "No asistio", StringComparison.OrdinalIgnoreCase) || string.Equals(c.Estado, "no asistió", StringComparison.OrdinalIgnoreCase));
                    ViewData["OdoTotalCitasMes"] = citasDelMes.Count;

                    ViewData["OdoProximasCitas"] = citasDelMes
                        .Where(c => c.FechaHora.Date == hoy && (string.Equals(c.Estado, "Agendada", StringComparison.OrdinalIgnoreCase) || string.Equals(c.Estado, "programada", StringComparison.OrdinalIgnoreCase)))
                        .OrderBy(c => c.FechaHora)
                        .Take(4)
                        .ToList();

                    ViewData["OdoUltimosPacientes"] = citasDelMes
                        .Where(c => string.Equals(c.Estado, "Atendida", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(c => c.FechaHora)
                        .Take(3)
                        .ToList();
                }
            }

            return View("~/Views/Gestion_De_Profesionales/st-odo-01-dashboard/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando Stodo01Dashboard para profesional {Usuario}", User.Identity?.Name ?? "anonimo");
            TempData["ErrorValidacion"] = MensajeErrorFallback;
            return View("~/Views/Gestion_De_Profesionales/st-odo-01-dashboard/index.cshtml");
        }
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("gestion-de-profesionales/st-odo-09-perfil-profesional")]
    public async Task<IActionResult> Stodo09PerfilProfesional([FromQuery] int? editId, CancellationToken ct = default)
    {
        try
        {
            await CargarDatosProfesionales(editId, BuildReturnUrl(), null, ct);

            string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                var profesional = await _context.Profesionales
                    .Include(p => p.Usuario)
                    .Include(p => p.Especialidades)
                    .ThenInclude(pe => pe.Especialidad)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.IdUsuario == userId, ct);

                ViewData["LoggedProfesional"] = profesional;
            }

            return View("~/Views/Gestion_De_Profesionales/st-odo-09-perfil-profesional/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando Stodo09PerfilProfesional");
            TempData["ErrorValidacion"] = MensajeErrorFallback;
            return View("~/Views/Gestion_De_Profesionales/st-odo-09-perfil-profesional/index.cshtml");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-profesionales/guardar-profesional")]
    public async Task<IActionResult> GuardarProfesional([FromForm] ProfesionalViewModel model, CancellationToken ct = default)
    {
        string returnUrlSafe = !string.IsNullOrWhiteSpace(model?.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? model.ReturnUrl
            : "/gestion-de-profesionales/st-adm-07-gestion-profesionales";
        int idOperacion = model?.IdProfesional ?? 0;
        string operacion = idOperacion > 0 ? "Actualizacion" : "Creacion";

        if (!ModelState.IsValid)
        {
            string? firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            string mensaje = firstError ?? "Datos inválidos en el formulario.";
            _logger.LogWarning("GuardarProfesional: ModelState invalido ({Operacion}). Detalle: {Error}", operacion, mensaje);
            TempData["ErrorValidacion"] = mensaje;
            return Redirect(returnUrlSafe);
        }

        if (model is null)
        {
            TempData["ErrorValidacion"] = "No se recibieron datos del profesional.";
            return Redirect(returnUrlSafe);
        }

        if (!EsRegistroMedicoValido(model.RegistroMedico))
        {
            TempData["ErrorValidacion"] = "El registro médico tiene un formato inválido. Use letras, números y guiones (3-30 caracteres).";
            _logger.LogWarning("GuardarProfesional: Formato RegistroMedico invalido ({Registro})", model.RegistroMedico);
            return Redirect(returnUrlSafe);
        }

        if (!EsTelefonoValido(model.Telefono))
        {
            TempData["ErrorValidacion"] = "El número de teléfono es inválido. Debe contener entre 7 y 15 dígitos.";
            _logger.LogWarning("GuardarProfesional: Telefono invalido ({Telefono})", model.Telefono);
            return Redirect(returnUrlSafe);
        }

        if (!string.IsNullOrWhiteSpace(model.Nombres) && model.Nombres.Trim().Length < 2)
        {
            TempData["ErrorValidacion"] = "Los nombres deben contener al menos 2 caracteres.";
            return Redirect(returnUrlSafe);
        }

        if (!string.IsNullOrWhiteSpace(model.Apellidos) && model.Apellidos.Trim().Length < 2)
        {
            TempData["ErrorValidacion"] = "Los apellidos deben contener al menos 2 caracteres.";
            return Redirect(returnUrlSafe);
        }

        if (model.IdUsuario != null && model.IdUsuario > 0)
        {
            bool usuarioValido = await _context.Usuarios.AnyAsync(u => u.IdUsuario == model.IdUsuario.Value, ct);
            if (!usuarioValido)
            {
                TempData["ErrorValidacion"] = "El usuario vinculado seleccionado no existe en el sistema.";
                _logger.LogWarning("GuardarProfesional: IdUsuario invalido Id={IdUsuario}", model.IdUsuario.Value);
                return Redirect(returnUrlSafe);
            }
        }

        try
        {
            bool registroMedicoDuplicado = await _context.Profesionales
                .AnyAsync(p => p.RegistroMedico == model.RegistroMedico && p.IdProfesional != idOperacion, ct);

            if (registroMedicoDuplicado)
            {
                TempData["ErrorValidacion"] = $"El registro médico '{model.RegistroMedico}' ya está asignado a otro profesional.";
                _logger.LogWarning("GuardarProfesional: RegistroMedico duplicado ({Registro}) en operacion {Operacion}", model.RegistroMedico, operacion);
                return Redirect(returnUrlSafe);
            }

            string estado = string.IsNullOrWhiteSpace(model.Estado) ? "activo" : model.Estado;
            int? idUsuario = model.IdUsuario is null or 0 ? (int?)null : model.IdUsuario;
            int idEspecialidad = model.IdEspecialidad is > 0 ? model.IdEspecialidad.Value : 0;

            if (idEspecialidad > 0)
            {
                bool espExiste = await _context.Especialidades.AnyAsync(e => e.IdEspecialidad == idEspecialidad, ct);
                if (!espExiste)
                {
                    TempData["ErrorValidacion"] = "La especialidad seleccionada no existe.";
                    return Redirect(returnUrlSafe);
                }
            }

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            Profesional? profesional;

            try
            {
                if (idOperacion > 0)
                {
                    profesional = await _context.Profesionales.Include(p => p.Especialidades)
                        .FirstOrDefaultAsync(p => p.IdProfesional == idOperacion, ct);

                    if (profesional == null)
                    {
                        await tx.RollbackAsync(ct);
                        TempData["ErrorValidacion"] = "El profesional que intenta actualizar no existe.";
                        _logger.LogWarning("GuardarProfesional: Intento actualizar profesional inexistente IdProfesional={Id}", idOperacion);
                        return Redirect(returnUrlSafe);
                    }

                    profesional.IdUsuario = idUsuario;
                    profesional.Nombres = model.Nombres?.Trim() ?? string.Empty;
                    profesional.Apellidos = model.Apellidos?.Trim() ?? string.Empty;
                    profesional.RegistroMedico = model.RegistroMedico.Trim();
                    profesional.Categoria = model.Categoria?.Trim();
                    profesional.Telefono = model.Telefono?.Trim();
                    profesional.Descripcion = model.Descripcion?.Trim();
                    profesional.Estado = EstadoCitaHelper.ResolveEstadoNombre(estado, profesional.Estado);
                    profesional.FechaIngreso ??= DateTime.Today;

                    _context.Profesionales.Update(profesional);
                }
                else
                {
                    profesional = new Profesional
                    {
                        IdUsuario = idUsuario,
                        Nombres = model.Nombres?.Trim() ?? string.Empty,
                        Apellidos = model.Apellidos?.Trim() ?? string.Empty,
                        RegistroMedico = model.RegistroMedico.Trim(),
                        Categoria = model.Categoria?.Trim(),
                        Telefono = model.Telefono?.Trim(),
                        Descripcion = model.Descripcion?.Trim(),
                        Estado = EstadoCitaHelper.ResolveEstadoNombre(estado, "activo"),
                        FechaIngreso = DateTime.Today
                    };
                    _context.Profesionales.Add(profesional);
                }

                await _context.SaveChangesAsync(ct);

                if (idEspecialidad > 0)
                {
                    var relaciones = await _context.ProfesionalEspecialidades
                        .Where(pe => pe.IdProfesional == profesional.IdProfesional)
                        .ToListAsync(ct);

                    if (relaciones.Count > 0)
                        _context.ProfesionalEspecialidades.RemoveRange(relaciones);

                    _context.ProfesionalEspecialidades.Add(new Profesional_Especialidad
                    {
                        IdProfesional = profesional.IdProfesional,
                        IdEspecialidad = idEspecialidad,
                        Principal = true
                    });
                    await _context.SaveChangesAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }

            _logger.LogInformation(
                "Auditoria: {Operacion} de profesional correcta. IdProfesional={IdProfesional}, RegistroMedico={RegistroMedico}, Usuario={Usuario}, Estado={Estado}",
                operacion,
                profesional.IdProfesional,
                profesional.RegistroMedico,
                User.Identity?.Name ?? "anonimo",
                profesional.Estado);

            TempData["MensajeExito"] = operacion == "Creacion"
                ? "El profesional se ha creado correctamente."
                : "El profesional se ha actualizado correctamente.";
        }
        catch (OperationCanceledException ocex)
        {
            _logger.LogWarning(ocex, "Operacion cancelada al guardar profesional Id={Id}", idOperacion);
            TempData["ErrorValidacion"] = "La operación fue cancelada antes de finalizar.";
        }
        catch (DbUpdateConcurrencyException cex)
        {
            _logger.LogError(cex, "Concurrencia al guardar profesional Id={Id}", idOperacion);
            TempData["ErrorValidacion"] = "Conflicto: los datos del profesional cambiaron durante la operación. Actualice y vuelva a intentar.";
        }
        catch (DbUpdateException dbex) when (EsViolacionIndiceUnico(dbex, out string? indice))
        {
            _logger.LogError(dbex, "Violacion UNIQUE al guardar profesional Id={Id}. Indice/mensaje: {Indice}", idOperacion, indice ?? "desconocido");
            TempData["ErrorValidacion"] = "No se pudo guardar: se detectó un dato duplicado (posiblemente registro médico o usuario vinculado).";
        }
        catch (DbUpdateException dbex) when (EsViolacionIntegridadReferencial(dbex))
        {
            _logger.LogError(dbex, "Violacion FK integridad al guardar profesional Id={Id}", idOperacion);
            TempData["ErrorValidacion"] = "No se pudo guardar: el usuario o especialidad vinculado no es válido.";
        }
        catch (DbUpdateException dbex)
        {
            _logger.LogError(dbex, "DbUpdateException al guardar profesional Id={Id}", idOperacion);
            TempData["ErrorValidacion"] = "Ocurrió un error al guardar en la base de datos.";
        }
        catch (SqlException sqlex)
        {
            _logger.LogCritical(sqlex, "SqlException al guardar profesional Id={Id}. Number={Number}", idOperacion, sqlex.Number);
            TempData["ErrorValidacion"] = "Error de conectividad con la base de datos. Intente nuevamente.";
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error inesperado al guardar profesional Id={Id}", idOperacion);
            TempData["ErrorValidacion"] = "Ocurrió un error inesperado. El incidente fue registrado.";
        }

        return Redirect(returnUrlSafe);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-profesionales/eliminar-profesional")]
    public async Task<IActionResult> EliminarProfesional([FromForm] int IdProfesional, [FromForm] string? ReturnUrl, CancellationToken ct = default)
    {
        string returnUrlSafe = !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl
            : "/gestion-de-profesionales/st-adm-07-gestion-profesionales";

        try
        {
            if (IdProfesional <= 0)
            {
                TempData["ErrorValidacion"] = "Identificador de profesional inválido.";
                _logger.LogWarning("EliminarProfesional: IdProfesional invalido ({Id})", IdProfesional);
                return Redirect(returnUrlSafe);
            }

            if (!User.IsInRole("Administrador"))
            {
                TempData["ErrorValidacion"] = "No tiene permisos suficientes para desactivar profesionales.";
                _logger.LogWarning("EliminarProfesional: Usuario {Usuario} sin permisos para Id={Id}",
                    User.Identity?.Name ?? "anonimo", IdProfesional);
                return Redirect(returnUrlSafe);
            }

            var profesional = await _context.Profesionales.FindAsync(IdProfesional, ct);
            if (profesional == null)
            {
                TempData["ErrorValidacion"] = "El profesional que intenta desactivar no existe.";
                _logger.LogWarning("EliminarProfesional: Profesional no encontrado Id={Id}", IdProfesional);
                return Redirect(returnUrlSafe);
            }

            bool tieneCitasActivas = await _context.Citas
                .AnyAsync(c => c.IdProfesional == IdProfesional
                    && c.Estado != "Cancelada"
                    && c.FechaHora >= DateTime.Today, ct);

            if (tieneCitasActivas && profesional.Estado != "inactivo")
            {
                TempData["ErrorValidacion"] = "No se puede desactivar: este profesional tiene citas agendadas pendientes.";
                _logger.LogWarning("EliminarProfesional: Intento desactivar profesional Id={Id} con citas activas.", IdProfesional);
                return Redirect(returnUrlSafe);
            }

            string estadoAnterior = profesional.Estado;
            profesional.Estado = "inactivo";
            _context.Profesionales.Update(profesional);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Auditoria: Profesional desactivado (baja logica). IdProfesional={IdProfesional}, RegistroMedico={RegistroMedico}, EstadoAnterior={EstadoAnterior}, UsuarioEliminador={Usuario}",
                profesional.IdProfesional,
                profesional.RegistroMedico,
                estadoAnterior,
                User.Identity?.Name ?? "anonimo");

            TempData["MensajeExito"] = "El profesional fue desactivado exitosamente.";
        }
        catch (OperationCanceledException ocex)
        {
            _logger.LogWarning(ocex, "Operacion cancelada en EliminarProfesional Id={Id}", IdProfesional);
            TempData["ErrorValidacion"] = "La operación fue cancelada.";
        }
        catch (DbUpdateConcurrencyException cex)
        {
            _logger.LogError(cex, "Concurrencia al desactivar profesional Id={Id}", IdProfesional);
            TempData["ErrorValidacion"] = "Conflicto: los datos cambiaron durante la operación. Actualice y vuelva a intentar.";
        }
        catch (DbUpdateException dbex) when (EsViolacionIntegridadReferencial(dbex))
        {
            _logger.LogError(dbex, "Violacion integridad al desactivar profesional Id={Id}", IdProfesional);
            TempData["ErrorValidacion"] = "No se puede desactivar este profesional porque tiene registros dependientes activos.";
        }
        catch (DbUpdateException dbex)
        {
            _logger.LogError(dbex, "DbUpdateException al desactivar profesional Id={Id}", IdProfesional);
            TempData["ErrorValidacion"] = "Ocurrió un error al intentar desactivar el profesional.";
        }
        catch (SqlException sqlex)
        {
            _logger.LogCritical(sqlex, "SqlException al desactivar profesional Id={Id}. Number={Number}", IdProfesional, sqlex.Number);
            TempData["ErrorValidacion"] = "Error de conectividad con la base de datos. Intente nuevamente.";
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error inesperado al desactivar profesional Id={Id}", IdProfesional);
            TempData["ErrorValidacion"] = "Ocurrió un error inesperado. El incidente fue registrado.";
        }

        return Redirect(returnUrlSafe);
    }

    private async Task CargarDatosReportesClinicos(int page, int pageSize, string? search = null, string? profesional = null, string? mes = null, CancellationToken ct = default)
    {
        try
        {
            var hoy = DateTime.Today;
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
            var finMes = inicioMes.AddMonths(1);

            var citasQuery = _context.Citas
                .Include(c => c.Paciente)
                .Include(c => c.Profesional)
                .ThenInclude(p => p!.Usuario)
                .Include(c => c.Servicio)
                .Where(c => c.Paciente != null)
                .AsNoTracking()
                .AsQueryable();

            if (User.IsInRole("Profesional"))
            {
                string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var prof = await _context.Profesionales.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdUsuario == userId, ct);
                    if (prof != null)
                    {
                        citasQuery = citasQuery.Where(c => c.IdProfesional == prof.IdProfesional);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchTerm = search.Trim();
                citasQuery = citasQuery.Where(c =>
                    (c.Paciente!.Nombres != null && c.Paciente.Nombres.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (c.Paciente.Apellidos != null && c.Paciente.Apellidos.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (c.Paciente.Documento != null && c.Paciente.Documento.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                );
            }

            if (!string.IsNullOrWhiteSpace(profesional))
            {
                string profTerm = profesional.Trim();
                citasQuery = citasQuery.Where(c =>
                    c.Profesional != null && (
                        ((c.Profesional.Nombres ?? "") + " " + (c.Profesional.Apellidos ?? "")).Contains(profTerm, StringComparison.OrdinalIgnoreCase) ||
                        (c.Profesional.Usuario != null && ((c.Profesional.Usuario.Nombre ?? "") + " " + (c.Profesional.Usuario.Apellidos ?? "")).Contains(profTerm, StringComparison.OrdinalIgnoreCase))
                    )
                );
            }

            if (!string.IsNullOrWhiteSpace(mes) && DateTime.TryParseExact(mes, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var filterMonth))
            {
                var filterStart = filterMonth;
                var filterEnd = filterMonth.AddMonths(1);
                citasQuery = citasQuery.Where(c => c.FechaHora >= filterStart && c.FechaHora < filterEnd);
            }

            var pacientesQuery = citasQuery
                .GroupBy(c => c.IdPaciente)
                .Select(g => g.OrderByDescending(c => c.FechaHora).First())
                .OrderByDescending(c => c.FechaHora);

            var pagedCitas = await pacientesQuery.ToPagedResultAsync(page, pageSize, ct);

            var profesionalesOptions = await _context.Profesionales
                .Where(p => p.Estado == "activo")
                .Select(p => (p.Nombres ?? "") + " " + (p.Apellidos ?? ""))
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync(ct);

            var todasCitasPacientes = await citasQuery.AsNoTracking().ToListAsync(ct);

            var reportes = pagedCitas.Items.Select(cita =>
            {
                var pacienteCitas = todasCitasPacientes.Where(c => c.IdPaciente == cita.IdPaciente).ToList();

                var nacimiento = cita.Paciente?.FechaNacimiento;
                int edad = nacimiento.HasValue
                    ? hoy.Year - nacimiento.Value.Year - (hoy < nacimiento.Value.AddYears(hoy.Year - nacimiento.Value.Year) ? 1 : 0)
                    : 0;

                var proximaCita = pacienteCitas
                    .Where(c => c.FechaHora.Date >= hoy)
                    .OrderBy(c => c.FechaHora)
                    .FirstOrDefault();

                string? alerta = string.IsNullOrWhiteSpace(cita.Notas) ? null : "observacion";

                return new ReporteClinicoViewModel
                {
                    Id = cita.IdCita,
                    NombrePaciente = $"{cita.Paciente?.Nombres} {cita.Paciente?.Apellidos}".Trim(),
                    Documento = cita.Paciente?.Documento ?? string.Empty,
                    Edad = edad,
                    UltimaConsulta = cita.FechaHora,
                    Diagnostico = cita.MotivoConsulta ?? "Consulta general",
                    ProfesionalNombre = cita.Profesional?.Usuario != null
                        ? $"{cita.Profesional.Usuario.Nombre} {cita.Profesional.Usuario.Apellidos}".Trim()
                        : "Sin profesional",
                    ProximaCita = proximaCita?.FechaHora,
                    Alerta = alerta,
                    Avatar = string.Concat((cita.Paciente?.Nombres ?? "P").Take(2).Select(ch => char.ToUpperInvariant(ch))),
                    Color = pacienteCitas.Count % 2 == 0 ? "green" : "blue"
                };
            }).ToList();

            var pagedReportes = new PagedResult<ReporteClinicoViewModel>
            {
                Page = pagedCitas.Page,
                PageSize = pagedCitas.PageSize,
                TotalCount = pagedCitas.TotalCount,
                Items = reportes
            };

            ViewData["ReportesClinicos"] = reportes;
            ViewData["ReportesClinicosPage"] = pagedReportes;
            ViewData["ProfesionalesReportes"] = profesionalesOptions;
            ViewData["TotalPacientesReportes"] = await _context.Pacientes.CountAsync(ct);

            var consultasQuery = _context.Citas.AsNoTracking();
            if (User.IsInRole("Profesional"))
            {
                string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var prof = await _context.Profesionales.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdUsuario == userId, ct);
                    if (prof != null)
                    {
                        consultasQuery = consultasQuery.Where(c => c.IdProfesional == prof.IdProfesional);
                    }
                }
            }

            ViewData["ConsultasMes"] = await consultasQuery.CountAsync(c => c.FechaHora >= inicioMes && c.FechaHora < finMes, ct);

            // Calcular SatisfaccionPromedio como % de citas atendidas sobre el total del mes
            // (opción (a) acordada: dato real calculado desde BD, no hardcoded)
            int totalCitasMes = await consultasQuery.CountAsync(c => c.FechaHora >= inicioMes && c.FechaHora < finMes, ct);
            int citasAtendidasMes = await consultasQuery.CountAsync(
                c => c.FechaHora >= inicioMes && c.FechaHora < finMes
                  && (c.Estado == "Atendida" || c.Estado == "atendida"), ct);
            ViewData["SatisfaccionPromedio"] = totalCitasMes > 0
                ? (int)Math.Round(citasAtendidasMes * 100.0 / totalCitasMes, 0)
                : 0;

            ViewData["SearchFilter"] = search;
            ViewData["ProfesionalFilter"] = profesional;
            ViewData["MesFilter"] = mes;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("CargarDatosReportesClinicos cancelado");
            InicializarViewDataReportesVacios();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error general en CargarDatosReportesClinicos. Se devuelven datos vacios.");
            InicializarViewDataReportesVacios();
        }
    }

    private void InicializarViewDataReportesVacios()
    {
        ViewData["ReportesClinicos"] = new List<ReporteClinicoViewModel>();
        ViewData["ReportesClinicosPage"] = PagedResult<ReporteClinicoViewModel>.Empty(1, 10);
        ViewData["ProfesionalesReportes"] = new List<string>();
        ViewData["TotalPacientesReportes"] = 0;
        ViewData["ConsultasMes"] = 0;
        ViewData["SatisfaccionPromedio"] = 0;
        ViewData["SearchFilter"] = string.Empty;
        ViewData["ProfesionalFilter"] = string.Empty;
        ViewData["MesFilter"] = string.Empty;
    }

    private async Task CargarDatosProfesionales(int? editId, string returnUrl, PaginationQuery? query = null, CancellationToken ct = default)
    {
        try
        {
            var pagination = query ?? new PaginationQuery();
            int page = pagination.Page < 1 ? 1 : pagination.Page;
            int pageSize = pagination.PageSize < 1 ? 10 : pagination.PageSize;

            ViewData["StatTotal"] = await _context.Profesionales.CountAsync(ct);
            ViewData["StatActivos"] = await _context.Profesionales.CountAsync(p => p.Estado == "activo", ct);
            ViewData["StatVacaciones"] = await _context.Profesionales.CountAsync(p => p.Estado == "vacaciones", ct);
            ViewData["StatInactivos"] = await _context.Profesionales.CountAsync(p => p.Estado == "inactivo", ct);

            var profesionalesQuery = _context.Profesionales
                .Include(p => p.Usuario)
                .Include(p => p.Especialidades)
                .ThenInclude(pe => pe.Especialidad)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(pagination.Search))
            {
                string searchTerm = pagination.Search.Trim();
                profesionalesQuery = profesionalesQuery.Where(p =>
                    (p.Usuario != null && ((p.Usuario.Nombre != null && p.Usuario.Nombre.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) || (p.Usuario.Apellidos != null && p.Usuario.Apellidos.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))) ||
                    (p.Nombres != null && p.Nombres.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Apellidos != null && p.Apellidos.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (p.RegistroMedico != null && p.RegistroMedico.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Especialidades.Any(pe => pe.Especialidad != null && pe.Especialidad.Nombre.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))));
            }

            if (!string.IsNullOrWhiteSpace(pagination.Profesional))
            {
                string especialidad = pagination.Profesional.Trim();
                profesionalesQuery = profesionalesQuery.Where(p => p.Especialidades.Any(pe => pe.Especialidad != null && pe.Especialidad.Nombre.Equals(especialidad, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(pagination.Estado))
            {
                string estado = pagination.Estado.Trim();
                profesionalesQuery = profesionalesQuery.Where(p => p.Estado != null && p.Estado.Equals(estado, StringComparison.OrdinalIgnoreCase));
            }

            profesionalesQuery = profesionalesQuery.OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres);

            var paged = await profesionalesQuery.ToPagedResultAsync(page, pageSize, ct);

            ViewData["Profesionales"] = paged.Items.ToList();
            ViewData["ProfesionalesPage"] = paged;
            ViewData["PaginationQuery"] = pagination;
            ViewData["SearchFilter"] = pagination.Search ?? string.Empty;
            ViewData["EspecialidadFilter"] = pagination.Profesional ?? string.Empty;
            ViewData["EstadoFilter"] = pagination.Estado ?? string.Empty;
            ViewData["Especialidades"] = await _context.Especialidades.AsNoTracking().OrderBy(e => e.Nombre).ToListAsync(ct);
            ViewData["Usuarios"] = await _context.Usuarios
                .Include(u => u.Rol)
                .Where(u => u.Rol != null && u.Rol.NombreRol == "Profesional")
                .OrderBy(u => u.Nombre).ThenBy(u => u.Apellidos)
                .AsNoTracking()
                .ToListAsync(ct);
            ViewData["ReturnUrl"] = returnUrl;

            if (editId is > 0)
            {
                try
                {
                    ViewData["EditingProfesional"] = await _context.Profesionales.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdProfesional == editId.Value, ct);
                }
                catch (Exception exEditar)
                {
                    _logger.LogWarning(exEditar, "Error cargando profesional para editar IdProfesional={Id}", editId.Value);
                    ViewData["EditingProfesional"] = null;
                }
            }
            else
            {
                ViewData["EditingProfesional"] = null;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("CargarDatosProfesionales cancelado");
            InicializarViewDataProfesionalesVacio(returnUrl);
        }
        catch (SqlException sqlex)
        {
            _logger.LogCritical(sqlex, "SqlException en CargarDatosProfesionales. Number={Number}", sqlex.Number);
            InicializarViewDataProfesionalesVacio(returnUrl);
            TempData["ErrorValidacion"] = MensajeErrorFallback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error general en CargarDatosProfesionales. Se devuelven listas vacias.");
            InicializarViewDataProfesionalesVacio(returnUrl);
        }
    }

    private void InicializarViewDataProfesionalesVacio(string returnUrl)
    {
        ViewData["StatTotal"] = 0;
        ViewData["StatActivos"] = 0;
        ViewData["StatVacaciones"] = 0;
        ViewData["StatInactivos"] = 0;
        ViewData["Profesionales"] = new List<Profesional>();
        ViewData["ProfesionalesPage"] = PagedResult<Profesional>.Empty(1, 10);
        ViewData["PaginationQuery"] = new PaginationQuery();
        ViewData["SearchFilter"] = string.Empty;
        ViewData["EspecialidadFilter"] = string.Empty;
        ViewData["EstadoFilter"] = string.Empty;
        ViewData["Especialidades"] = new List<Especialidad>();
        ViewData["Usuarios"] = new List<Usuario>();
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["EditingProfesional"] = null;
    }

    private static bool EsViolacionIndiceUnico(DbUpdateException dbex, out string? indiceAfectado)
    {
        indiceAfectado = null;
        var sqlEx = dbex.InnerException as SqlException ?? dbex.InnerException?.InnerException as SqlException;
        if (sqlEx == null) return false;
        if (sqlEx.Number is 2601 or 2627)
        {
            indiceAfectado = sqlEx.Message;
            return true;
        }
        return false;
    }

    private string BuildReturnUrl()
    {
        var queryParams = HttpContext.Request.Query
            .Where(kvp => !string.Equals(kvp.Key, "editId", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count == 0 ? null : kvp.Value.ToString());

        var queryString = QueryString.Create(queryParams);
        return HttpContext.Request.Path + queryString;
    }

    private static bool EsViolacionIntegridadReferencial(DbUpdateException dbex)
    {
        var sqlEx = dbex.InnerException as SqlException ?? dbex.InnerException?.InnerException as SqlException;
        if (sqlEx == null) return false;
        return sqlEx.Number is 547 or 515;
    }
}
