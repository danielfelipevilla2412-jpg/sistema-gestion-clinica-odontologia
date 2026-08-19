using Microsoft.EntityFrameworkCore;

namespace SmileTrack_MVC.Models.Shared;

public class PagedResult<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public IReadOnlyList<T> Items { get; set; } = [];
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) => new()
    {
        Page = page < 1 ? 1 : page,
        PageSize = pageSize < 1 ? 10 : pageSize,
        TotalCount = 0,
        Items = []
    };
}

public static class PagedResultExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 500) pageSize = 500;

            int totalCount = await query.CountAsync(ct);
            if (ct.IsCancellationRequested)
            {
                return PagedResult<T>.Empty(page, pageSize);
            }

            var items = totalCount == 0
                ? []
                : await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            return new PagedResult<T>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = items
            };
        }
        catch (OperationCanceledException)
        {
            return PagedResult<T>.Empty(page, pageSize);
        }
        catch (InvalidOperationException ioex) when (ioex.InnerException is Microsoft.Data.SqlClient.SqlException)
        {
            throw new InvalidOperationException(
                "Error de base de datos al paginar los resultados. Verifique su consulta.", ioex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Fallo inesperado al paginar resultados (Tipo={typeof(T).Name}, Pagina={page}, Tamano={pageSize}).",
                ex);
        }
    }
}
