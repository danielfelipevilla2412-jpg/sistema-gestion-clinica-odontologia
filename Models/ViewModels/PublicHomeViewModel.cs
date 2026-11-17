namespace SmileTrack_MVC.Models.ViewModels;

public sealed class PublicHomeViewModel
{
    public IReadOnlyList<PublicServiceViewModel> Services { get; init; } =
        Array.Empty<PublicServiceViewModel>();
}

public sealed class PublicServiceViewModel
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int DurationMinutes { get; init; }
}
