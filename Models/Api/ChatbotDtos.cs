namespace SmileTrack_MVC.Models.Api;

public sealed class ChatbotRequest
{
    public string Message { get; init; } = string.Empty;
    public string? CurrentPath { get; init; }
}

public sealed class ChatbotAction
{
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

public sealed class ChatbotResponse
{
    public string Role { get; init; } = string.Empty;
    public string RoleLabel { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ChatbotAction> Actions { get; init; } = Array.Empty<ChatbotAction>();
}