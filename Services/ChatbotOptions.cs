namespace SmileTrack_MVC.Services;

public sealed class ChatbotOptions
{
    public double HighRelevanceThreshold { get; set; } = 0.85;
    public double AmbiguityScoreMargin { get; set; } = 0.20;
    public double AmbiguityRelativeMargin { get; set; } = 0.15;
    public int MaxMessageLength { get; set; } = 500;
    public int MessagesPerMinute { get; set; } = 15;
    public int ConversationRetentionMinutes { get; set; } = 30;
    public int MaxConversationStates { get; set; } = 5000;
    public int TicketRequestsPer15Minutes { get; set; } = 5;
    public int GlobalRequestsPerMinute { get; set; } = 120;
}
