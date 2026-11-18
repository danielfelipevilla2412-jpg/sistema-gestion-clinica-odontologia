using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

public static class PacienteAlergiaSync
{
    public static async Task SynchronizeAsync(
        AppDbContext context,
        Paciente paciente,
        string? alergias,
        CancellationToken cancellationToken = default)
    {
        var sustancias = (alergias ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var alergiasActivas = await context.AlergiasPaciente
            .Where(a => a.IdPaciente == paciente.IdPaciente && a.Activa)
            .ToListAsync(cancellationToken);

        foreach (var existente in alergiasActivas)
        {
            if (!sustancias.Contains(existente.Sustancia, StringComparer.OrdinalIgnoreCase))
                existente.Activa = false;
        }

        foreach (var sustancia in sustancias)
        {
            if (alergiasActivas.Any(a =>
                    string.Equals(a.Sustancia, sustancia, StringComparison.OrdinalIgnoreCase)))
                continue;

            context.AlergiasPaciente.Add(new AlergiaPaciente
            {
                IdPaciente = paciente.IdPaciente,
                Sustancia = sustancia,
                Tipo = "otro",
                Severidad = "leve",
                FechaRegistro = DateTime.UtcNow,
                Activa = true
            });
        }

        paciente.Alergias = sustancias.Count == 0 ? null : string.Join(", ", sustancias);
    }
}
