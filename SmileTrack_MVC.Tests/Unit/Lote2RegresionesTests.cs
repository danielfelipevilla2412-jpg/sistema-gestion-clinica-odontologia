using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class Lote2RegresionesTests
{
    private static string RepoFile(params string[] parts)
    {
        var sourceDirectory = Path.GetDirectoryName(
            new StackTrace(true).GetFrames()?
                .Select(frame => frame.GetFileName())
                .FirstOrDefault(file => !string.IsNullOrWhiteSpace(file)));
        var directory = sourceDirectory ?? Directory.GetCurrentDirectory();
        while (directory is not null && !File.Exists(Path.Combine(directory, "SmileTrack_MVC.csproj")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        Assert.False(directory is null, "No se encontró la raíz del repositorio.");
        return Path.Combine(new[] { directory! }.Concat(parts).ToArray());
    }

    [Fact]
    public void AgendaApoyo_FiltraPorNombreRealDelProfesional()
    {
        var source = File.ReadAllText(RepoFile(
            "wwwroot", "js", "Gestion_De_Citas", "st-aux-02-agenda-apoyo", "agenda-apoyo.js"));

        Assert.Contains("normalizar(c.profesional) === normalizar(profesional)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("includes(profesional)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AgendaApoyo_FiltrosDeTipoSeMantienenHorizontalesYConectados()
    {
        var script = File.ReadAllText(RepoFile(
            "wwwroot", "js", "Gestion_De_Citas", "st-aux-02-agenda-apoyo", "agenda-apoyo.js"));
        var styles = File.ReadAllText(RepoFile(
            "wwwroot", "css", "Gestion_De_Citas", "st-aux-02-agenda-apoyo", "agenda-apoyo.css"));
        var controller = File.ReadAllText(RepoFile(
            "Controllers", "GestionDeCitas", "GestionCitasController.cs"));

        Assert.Contains(
            ".filter-group[role=\"radiogroup\"] .filter-btn[role=\"radio\"][data-value]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "normalizarTipo(c.tipo) === normalizarTipo(tipo)",
            script,
            StringComparison.Ordinal);
        Assert.Contains("agendaSupportControllerInstance._filtroTipo = filterValue", script, StringComparison.Ordinal);
        Assert.Contains("applyCombinedFilters();", script, StringComparison.Ordinal);
        Assert.Contains("flex-direction: row", styles, StringComparison.Ordinal);
        Assert.Contains("n.Contains(\"consulta\")", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void HistorialParcial_SinPacienteIdNoSeleccionaElUltimoPacienteGlobal()
    {
        var source = File.ReadAllText(RepoFile("Controllers", "GestionDeCitas", "GestionCitasController.cs"));

        Assert.Contains("pacienteId is not null", source, StringComparison.Ordinal);
        Assert.Contains(": null;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderByDescending(c => c.FechaHora)\n                    .Select(c => c.Paciente)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void VistasAuxiliares_NoInterpolanDatosEnInnerHtml()
    {
        var scripts = new[]
        {
            RepoFile("wwwroot", "js", "Gestion_De_Citas", "st-aux-01-panel-operativo", "panel-operativo.js"),
            RepoFile("wwwroot", "js", "Gestion_De_Citas", "st-aux-02-agenda-apoyo", "agenda-apoyo.js"),
            RepoFile("wwwroot", "js", "Gestion_De_Pacientes", "st-aux-05-historial-parcial", "historial-parcial.js")
        };

        foreach (var script in scripts)
        {
            var source = File.ReadAllText(script);
            Assert.DoesNotContain("innerHTML", source, StringComparison.Ordinal);
        }
    }
}
