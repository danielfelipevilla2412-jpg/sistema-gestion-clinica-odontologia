using SmileTrack_MVC.Models.Api;

namespace SmileTrack_MVC.Services;

public interface IChatbotService
{
    ChatbotResponse Respond(string? role, string? userName, string? currentPath, string message);
}