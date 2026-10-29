using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class NotificacionesStpac03Tests
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
    public void Controller_ExponeRutasRestDeNotificacionesYMotorOmnicanal()
    {
        var controllerSource = File.ReadAllText(RepoFile("Controllers", "GestionDeCitas", "GestionCitasController.cs"));

        Assert.Contains("Route(\"api/notificaciones\")", controllerSource, StringComparison.Ordinal);
        Assert.Contains("Route(\"api/notificaciones/unread-count\")", controllerSource, StringComparison.Ordinal);
        Assert.Contains("Route(\"api/notificaciones/{id:int}/leida\")", controllerSource, StringComparison.Ordinal);
        Assert.Contains("Route(\"api/notificaciones/leidas\")", controllerSource, StringComparison.Ordinal);
        Assert.Contains("Route(\"api/notificaciones/{id:int}\")", controllerSource, StringComparison.Ordinal);
        Assert.Contains("ConstruirNotificacionesPacienteAsync", controllerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VistaNotificaciones_ContieneModalDetallesYChipsFiltro()
    {
        var viewSource = File.ReadAllText(RepoFile("Views", "Gestion_De_Citas", "st-pac-03-notificaciones", "index.cshtml"));

        Assert.Contains("id=\"modalNotifDetail\"", viewSource, StringComparison.Ordinal);
        Assert.Contains("id=\"btnToggleUnread\"", viewSource, StringComparison.Ordinal);
        Assert.Contains("data-filter=\"waitlist\"", viewSource, StringComparison.Ordinal);
        Assert.Contains("data-filter=\"invoice\"", viewSource, StringComparison.Ordinal);
        Assert.Contains("data-filter=\"reminder\"", viewSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptNotificaciones_SoportaEliminacionYConexionApi()
    {
        var scriptSource = File.ReadAllText(RepoFile("wwwroot", "js", "Gestion_De_Citas", "st-pac-03-notificaciones", "notificaciones.js"));

        Assert.Contains("deleteOnServer", scriptSource, StringComparison.Ordinal);
        Assert.Contains("markReadOnServer", scriptSource, StringComparison.Ordinal);
        Assert.Contains("fetchNotificationsApi", scriptSource, StringComparison.Ordinal);
        Assert.Contains("openModalDetail", scriptSource, StringComparison.Ordinal);
    }
}
