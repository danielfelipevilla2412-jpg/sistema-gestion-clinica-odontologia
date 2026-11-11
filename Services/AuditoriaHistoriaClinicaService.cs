using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

public sealed class AuditoriaHistoriaClinicaService(AppDbContext context)
{
    private static readonly HashSet<string> AccionesPermitidas = new(StringComparer.Ordinal)
    {
        "SELECT",
        "INSERT",
        "UPDATE",
        "DELETE"
    };

    public void Agregar(
        HttpContext httpContext,
        string accion,
        string tablaAfectada,
        int? idRegistro,
        string descripcion)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(tablaAfectada);
        ArgumentException.ThrowIfNullOrWhiteSpace(descripcion);

        if (!AccionesPermitidas.Contains(accion))
            throw new ArgumentOutOfRangeException(nameof(accion), "La acción de auditoría no está permitida.");

        if (!int.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out int idUsuario) ||
            idUsuario <= 0)
        {
            throw new InvalidOperationException("No se pudo identificar al usuario que realiza la operación clínica.");
        }

        if (tablaAfectada.Length > 100 || descripcion.Length > 500)
            throw new ArgumentException("Los metadatos de auditoría superan la longitud permitida.");

        context.Auditorias.Add(new Auditoria
        {
            IdUsuario = idUsuario,
            TablaAfectada = tablaAfectada,
            IdRegistro = idRegistro,
            Accion = accion,
            IpOrigen = httpContext.Connection.RemoteIpAddress?.ToString(),
            Descripcion = descripcion,
            Fecha = DateTime.UtcNow
        });
    }

    public async Task RegistrarAsync(
        HttpContext httpContext,
        string accion,
        string tablaAfectada,
        int? idRegistro,
        string descripcion,
        CancellationToken cancellationToken = default)
    {
        Agregar(httpContext, accion, tablaAfectada, idRegistro, descripcion);
        await context.SaveChangesAsync(cancellationToken);
    }
}
