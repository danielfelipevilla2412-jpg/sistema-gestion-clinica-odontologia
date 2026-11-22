using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;

namespace SmileTrack_MVC.Services;

internal static class PermisoReagendamiento
{
    internal const string ClaveProfesional = "permiso_profesional_reagendar";
    internal static string Clave(string accion) => $"permiso_recepcion_{accion}";

    internal static async Task<bool> PermitidoAsync(AppDbContext context, CancellationToken ct, string accion = "reagendar", bool profesional = false)
    {
        var clave = profesional ? ClaveProfesional : Clave(accion);
        var valor = await context.ConfiguracionesGenerales.AsNoTracking()
            .Where(c => c.Clave == clave).Select(c => c.Valor).SingleOrDefaultAsync(ct);
        return valor == null || valor == "true";
    }
}
