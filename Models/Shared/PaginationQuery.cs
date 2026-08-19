namespace SmileTrack_MVC.Models.Shared;

public class PaginationQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? Estado { get; set; }
    public string? Profesional { get; set; }
    public string? Fecha { get; set; }
}
