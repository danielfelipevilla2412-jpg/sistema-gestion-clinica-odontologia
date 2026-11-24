namespace SmileTrack_MVC.Services;

public sealed record ChatbotAiPrompt(
    string Question,
    IReadOnlyList<ChatbotAiEvidence> Evidence);

public sealed record ChatbotAiEvidence(
    string SourceId,
    string Title,
    string Content);

public sealed record ChatbotAiDraft(
    string Answer,
    IReadOnlyList<string> SourceIds);

public interface IChatbotAiProvider
{
    bool IsAvailable { get; }

    Task<ChatbotAiDraft?> GenerateAnswerAsync(
        ChatbotAiPrompt prompt,
        CancellationToken cancellationToken);
}
