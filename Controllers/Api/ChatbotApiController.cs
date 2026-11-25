using System.Security.Claims;
using System.Globalization;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmileTrack_MVC.Models.Api;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Models.Api.CentroDeAyuda;
using SmileTrack_MVC.Services.CentroDeAyuda;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/chatbot")]
public sealed class ChatbotApiController(
    IChatbotService chatbotService,
    ICentroDeAyudaService centroDeAyudaService) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> TicketRequests =
        new(StringComparer.Ordinal);
    private static readonly TimeSpan TicketRequestRetention = TimeSpan.FromMinutes(10);

    [HttpPost("message")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ChatbotMessageByUser")]
    public ActionResult<ChatbotResponse> Message(ChatbotRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 500)
            return BadRequest(new { message = "Escribe una consulta de hasta 500 caracteres." });

        var role = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirst("role")?.Value
            ?? User.FindFirst("rol")?.Value;

        var conversationKey = BuildConversationKey();
        var response = chatbotService.Respond(
            role,
            User.Identity?.Name,
            request.CurrentPath,
            request.Message,
            conversationKey);

        return Ok(response);
    }

    [HttpPost("tickets")]
    [ValidateAntiForgeryToken]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("ChatbotTicketByUser")]
    public async Task<IActionResult> CreateTicket(
        [FromForm] ChatbotTicketRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return UnprocessableEntity(new
            {
                success = false,
                message = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                    ?? "Completa los datos del ticket."
            });

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
            return Unauthorized(new { success = false, message = "No fue posible identificar al usuario." });

        if (!request.Confirmed)
            return BadRequest(new
            {
                success = false,
                requiresConfirmation = true,
                message = "Confirma expresamente la creación del ticket antes de enviarlo."
            });

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return BadRequest(new
            {
                success = false,
                message = "Falta la clave de idempotencia del formulario."
            });

        PurgeTicketRequests();
        var ticketKey = $"{userId}:{request.IdempotencyKey}";
        var now = DateTimeOffset.UtcNow;
        if (!TicketRequests.TryAdd(ticketKey, now))
            return Conflict(new
            {
                success = false,
                duplicate = true,
                message = "Este ticket ya fue enviado o se está procesando."
            });

        var ticketRequest = new CentroAyudaTicketRequest
        {
            Asunto = request.Subject,
            Categoria = request.Category,
            ModuloAfectado = request.Module,
            Severidad = request.Severity,
            Descripcion = request.Description,
            CapturaPantalla = request.Attachment
        };

        try
        {
            var (ticket, error) = await centroDeAyudaService.CrearTicketAsync(ticketRequest, userId, ct);
            if (error is not null || ticket is null)
                return UnprocessableEntity(new
                {
                    success = false,
                    message = error ?? "No fue posible registrar el ticket."
                });

            return Ok(new
            {
                success = true,
                message = "Ticket registrado correctamente.",
                data = ticket
            });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("tickets/mine")]
    [EnableRateLimiting("ChatbotTicketReadByUser")]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "No fue posible identificar al usuario." });

        var (items, total) = await centroDeAyudaService.ObtenerTicketsUsuarioAsync(
            userId.Value, page, pageSize, ct);
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 50);

        return Ok(new
        {
            success = true,
            data = items,
            pagination = new
            {
                page = safePage,
                pageSize = safePageSize,
                totalCount = total,
                totalPages = (int)Math.Ceiling(total / (double)safePageSize)
            }
        });
    }

    [HttpPost("conversation/reset")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ChatbotMessageByUser")]
    public IActionResult ResetConversation()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "No fue posible identificar al usuario." });

        chatbotService.ResetConversation(BuildConversationKey());
        return Ok(new { success = true, message = "Conversación reiniciada." });
    }

    [HttpGet("tickets/mine/{reference}")]
    [EnableRateLimiting("ChatbotTicketReadByUser")]
    public async Task<IActionResult> GetMyTicket(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 20)
            return BadRequest(new { success = false, message = "Referencia inválida." });

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "No fue posible identificar al usuario." });

        var ticket = await centroDeAyudaService.ObtenerTicketUsuarioAsync(
            reference, userId.Value, ct);

        return ticket is null
            ? NotFound(new { success = false, message = "Ticket no encontrado." })
            : Ok(new { success = true, data = ticket });
    }

    [HttpGet("tickets/mine/{reference}/attachment")]
    [EnableRateLimiting("ChatbotTicketReadByUser")]
    public async Task<IActionResult> GetMyTicketAttachment(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 20)
            return BadRequest(new { success = false, message = "Referencia inválida." });

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "No fue posible identificar al usuario." });

        var ticket = await centroDeAyudaService.ObtenerTicketUsuarioAsync(reference, userId.Value, ct);
        if (ticket is null || string.IsNullOrWhiteSpace(ticket.AttachmentPath))
            return NotFound(new { success = false, message = "Archivo no encontrado." });

        var relative = ticket.AttachmentPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(Path.Combine(HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>().ContentRootPath, "private_uploads", "centro-ayuda"))
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>().ContentRootPath, relative));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            return NotFound(new { success = false, message = "Archivo no encontrado." });

        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };

        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(fullPath, contentType, Path.GetFileName(fullPath), enableRangeProcessing: false);
    }

    private int? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out var userId) && userId > 0
            ? userId
            : null;
    }

    private string BuildConversationKey()
    {
        var userId = GetCurrentUserId()?.ToString(CultureInfo.InvariantCulture) ?? "anonymous";
        var sessionId = Request.Headers["X-Chatbot-Session"].ToString().Trim();
        if (sessionId.Length is < 16 or > 64 ||
            sessionId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            sessionId = "invalid";
        }
        return $"{userId}:{sessionId}";
    }

    private void PurgeTicketRequests()
    {
        var cutoff = DateTimeOffset.UtcNow - TicketRequestRetention;
        foreach (var item in TicketRequests)
        {
            if (item.Value < cutoff)
                TicketRequests.TryRemove(item.Key, out _);
        }
    }
}