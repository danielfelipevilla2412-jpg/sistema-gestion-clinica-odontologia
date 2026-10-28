using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.Api;
using SmileTrack_MVC.Services;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/chatbot")]
public sealed class ChatbotApiController(IChatbotService chatbotService) : ControllerBase
{
    [HttpPost("message")]
    [ValidateAntiForgeryToken]
    public ActionResult<ChatbotResponse> Message(ChatbotRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 500)
            return BadRequest(new { message = "Escribe una consulta de hasta 500 caracteres." });

        var role = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirst("role")?.Value
            ?? User.FindFirst("rol")?.Value;

        var response = chatbotService.Respond(
            role,
            User.Identity?.Name,
            request.CurrentPath,
            request.Message);

        return Ok(response);
    }
}