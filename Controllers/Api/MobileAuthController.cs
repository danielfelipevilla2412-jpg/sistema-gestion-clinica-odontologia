using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Route("api/mobile/auth")]
public class MobileAuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public MobileAuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                message = "Correo, contraseña y rol son obligatorios."
            });
        }

        var result = await _authService.LoginAsync(
            request,
            cancellationToken);

        if (!result.Success)
        {
            return Unauthorized(new
            {
                success = false,
                message = result.Message
            });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            token = result.Token,
            user = result.User
        });
    }
}