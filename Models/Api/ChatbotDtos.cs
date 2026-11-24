using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

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
    public string Type { get; init; } = "link";
    public string Value { get; init; } = string.Empty;
}

public sealed class ChatbotTicketRequest
{
    [Required, StringLength(200, MinimumLength = 5)]
    public string Subject { get; init; } = string.Empty;

    [Required]
    public string Category { get; init; } = string.Empty;

    [Required]
    public string Module { get; init; } = string.Empty;

    [Required]
    public string Severity { get; init; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; init; } = string.Empty;

    public bool Confirmed { get; init; }

    [StringLength(64, MinimumLength = 16)]
    public string? IdempotencyKey { get; init; }

    public IFormFile? Attachment { get; init; }
}

public sealed class ChatbotResponse
{
    public string Role { get; init; } = string.Empty;
    public string RoleLabel { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool NeedsClarification { get; init; }
    public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ChatbotAction> Actions { get; init; } = Array.Empty<ChatbotAction>();
    public IReadOnlyList<ChatbotKnowledgeSource> Sources { get; init; } = Array.Empty<ChatbotKnowledgeSource>();
}

public sealed class ChatbotKnowledgeSource
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

public sealed class ChatbotTicketSummary
{
    public string Reference { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Module { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? AnsweredAt { get; init; }
    public string? Response { get; init; }
}