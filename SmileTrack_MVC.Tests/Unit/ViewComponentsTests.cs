using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels.Components;
using SmileTrack_MVC.ViewComponents;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

/// <summary>
/// Pruebas de los ViewComponents de SmileTrack (carpeta ViewComponents/). Se instancia la clase
/// real, se invoca Invoke/InvokeAsync y se verifica el ViewViewComponentResult y su Model/ViewData.
/// Los componentes sin dependencias se instancian directo; los que reciben AppDbContext usan una
/// base InMemory sembrada con las filas mínimas necesarias.
/// </summary>
public sealed class ViewComponentsTests
{
    private static AppDbContext CrearDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        return new AppDbContext(options);
    }

    // ─────────────────────────── AccesosRapidos ───────────────────────────

    [Fact]
    public void AccesosRapidosViewComponent_ConLista_RetornaViewModelConLosAccesos()
    {
        var accesos = new List<AccesoRapidoItem>
        {
            new AccesoRapidoItem { Titulo = "Agenda", Url = "/agenda", Icono = "calendar" },
            new AccesoRapidoItem { Titulo = "Pacientes", Url = "/pacientes", Icono = "person" }
        };

        var resultado = new AccesosRapidosViewComponent().Invoke(accesos, titulo: "Accesos", columnas: "3");

        var viewResult = Assert.IsType<ViewViewComponentResult>(resultado);
        var modelo = Assert.IsType<AccesosRapidosViewModel>(viewResult.ViewData.Model);
        Assert.Equal(2, modelo.Accesos.Count);
        Assert.Equal("Agenda", modelo.Accesos[0].Titulo);
        Assert.Equal("Accesos", modelo.Titulo);
        Assert.Equal("3", modelo.Columnas);
    }

    [Fact]
    public void AccesosRapidosViewComponent_ListaNula_DevuelveListaVacia()
    {
        var resultado = new AccesosRapidosViewComponent().Invoke(null!);

        var modelo = Assert.IsType<AccesosRapidosViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Empty(modelo.Accesos);
        Assert.Equal("auto", modelo.Columnas);
    }

    // ─────────────────────────── BuscadorTiempoReal ───────────────────────────

    [Fact]
    public void BuscadorTiempoRealViewComponent_ParametrosPersonalizados_SeExponenEnViewData()
    {
        var resultado = new BuscadorTiempoRealViewComponent()
            .Invoke(placeholder: "Buscar cita", targetSelector: ".searchable-item", id: "buscadorCitas");

        var viewResult = Assert.IsType<ViewViewComponentResult>(resultado);
        Assert.Equal("Buscar cita", viewResult.ViewData["Placeholder"]);
        Assert.Equal(".searchable-item", viewResult.ViewData["TargetSelector"]);
        Assert.Equal("buscadorCitas", viewResult.ViewData["InputId"]);
    }

    [Fact]
    public void BuscadorTiempoRealViewComponent_SinParametros_UsaValoresPorDefecto()
    {
        var resultado = new BuscadorTiempoRealViewComponent().Invoke();

        var viewResult = Assert.IsType<ViewViewComponentResult>(resultado);
        Assert.Equal("Buscar...", viewResult.ViewData["Placeholder"]);
        Assert.Equal(".searchable-item", viewResult.ViewData["TargetSelector"]);
        Assert.Equal("searchRealTime", viewResult.ViewData["InputId"]);
    }

    // ─────────────────────────── IndicadoresTiempo ───────────────────────────

    [Fact]
    public void IndicadoresTiempoViewComponent_ConIndicadores_LosIncluyeEnElViewModel()
    {
        var indicadores = new List<IndicadorTiempoItem>
        {
            new IndicadorTiempoItem { Estado = "enEspera", Etiqueta = "En espera", Clase = "warning" }
        };

        var resultado = new IndicadoresTiempoViewComponent().Invoke(indicadores);

        var modelo = Assert.IsType<IndicadoresTiempoViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Single(modelo.Indicadores);
        Assert.Equal("En espera", modelo.Indicadores[0].Etiqueta);
    }

    [Fact]
    public void IndicadoresTiempoViewComponent_SinIndicadores_DevuelveListaVacia()
    {
        var resultado = new IndicadoresTiempoViewComponent().Invoke();

        var modelo = Assert.IsType<IndicadoresTiempoViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Empty(modelo.Indicadores);
    }

    // ─────────────────────────── NotificacionesInteligentes ───────────────────────────

    [Fact]
    public async Task NotificacionesInteligentesViewComponent_NotificacionesPersonalizadas_SeDevuelvenTalCual()
    {
        using var db = CrearDb(Guid.NewGuid().ToString());
        var componente = new NotificacionesInteligentesViewComponent(db);
        var personalizadas = new List<NotificacionItem>
        {
            new NotificacionItem { Titulo = "Mensaje manual", Tipo = TipoNotificacion.Urgent }
        };

        var resultado = await componente.InvokeAsync(notificacionesPersonalizadas: personalizadas);

        var modelo = Assert.IsType<NotificacionesViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Single(modelo.Notificaciones);
        Assert.Equal("Mensaje manual", modelo.Notificaciones[0].Titulo);
        Assert.True(modelo.TieneNotificaciones);
    }

    [Fact]
    public async Task NotificacionesInteligentesViewComponent_TipoProfesional_ConCitaSinConfirmar_GeneraNotificacion()
    {
        string dbName = Guid.NewGuid().ToString();
        using var db = CrearDb(dbName);
        db.Pacientes.Add(new Paciente { IdPaciente = 1, Nombres = "Juan", Apellidos = "Pérez", Estado = "activo" });
        db.Profesionales.Add(new Profesional { IdProfesional = 1, Nombres = "Ana", Apellidos = "Gómez", RegistroMedico = "RM-1", Estado = "activo" });
        db.Citas.Add(new Cita
        {
            IdCita = 1,
            IdPaciente = 1,
            IdProfesional = 1,
            Estado = "programada",
            FechaHora = DateTime.Now.AddHours(2)
        });
        await db.SaveChangesAsync();

        var componente = new NotificacionesInteligentesViewComponent(db);
        var resultado = await componente.InvokeAsync(tipo: "profesional", idProfesional: 1);

        var modelo = Assert.IsType<NotificacionesViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Contains(modelo.Notificaciones, n => n.Id == "citasSinConfirmar");
    }

    [Fact]
    public async Task NotificacionesInteligentesViewComponent_TipoPaciente_ConCitaProxima_GeneraNotificacion()
    {
        string dbName = Guid.NewGuid().ToString();
        using var db = CrearDb(dbName);
        db.Pacientes.Add(new Paciente { IdPaciente = 7, Nombres = "Luis", Apellidos = "Ramos", Estado = "activo" });
        db.Citas.Add(new Cita
        {
            IdCita = 2,
            IdPaciente = 7,
            Estado = "programada",
            FechaHora = DateTime.Now.AddHours(3)
        });
        await db.SaveChangesAsync();

        var componente = new NotificacionesInteligentesViewComponent(db);
        var resultado = await componente.InvokeAsync(tipo: "paciente", idPaciente: 7);

        var modelo = Assert.IsType<NotificacionesViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Contains(modelo.Notificaciones, n => n.Id == "proximasCitas");
    }

    // ─────────────────────────── PanelEstadisticas ───────────────────────────

    [Fact]
    public void PanelEstadisticasViewComponent_ConEstadisticas_RetornaViewModelConTituloYClase()
    {
        var estadisticas = new List<EstadisticaItem>
        {
            new EstadisticaItem { Etiqueta = "Citas hoy", Valor = 12, Color = "green" }
        };

        var resultado = new PanelEstadisticasViewComponent()
            .Invoke(estadisticas, titulo: "KPIs del día", claseAdicional: "mb-4");

        var modelo = Assert.IsType<PanelEstadisticasViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Single(modelo.Estadisticas);
        Assert.Equal(12, modelo.Estadisticas[0].Valor);
        Assert.Equal("KPIs del día", modelo.Titulo);
        Assert.Equal("mb-4", modelo.ClaseAdicional);
    }

    [Fact]
    public void PanelEstadisticasViewComponent_ListaNula_DevuelveListaVacia()
    {
        var resultado = new PanelEstadisticasViewComponent().Invoke(null!);

        var modelo = Assert.IsType<PanelEstadisticasViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Empty(modelo.Estadisticas);
    }

    // ─────────────────────────── PanelRendimiento ───────────────────────────

    [Fact]
    public void PanelRendimientoViewComponent_PorcentajeAlto_DevuelveMensajeExcelente()
    {
        var resultado = new PanelRendimientoViewComponent().Invoke(85);

        var modelo = Assert.IsType<PanelRendimientoViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Equal(85, modelo.Porcentaje);
        Assert.Equal("Excelente avance", modelo.Mensaje);
    }

    [Fact]
    public void PanelRendimientoViewComponent_PorcentajeFueraDeRango_SeLimitaA100()
    {
        var resultado = new PanelRendimientoViewComponent().Invoke(150);

        var modelo = Assert.IsType<PanelRendimientoViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Equal(100, modelo.Porcentaje);
        Assert.Equal("Excelente avance", modelo.Mensaje);
    }

    [Fact]
    public void PanelRendimientoViewComponent_PorcentajeBajo_DevuelveMensajeDeSeguimiento()
    {
        var resultado = new PanelRendimientoViewComponent().Invoke(30);

        var modelo = Assert.IsType<PanelRendimientoViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Equal(30, modelo.Porcentaje);
        Assert.Equal("Requiere seguimiento", modelo.Mensaje);
    }

    // ─────────────────────────── ProximaCitaWidget ───────────────────────────

    [Fact]
    public async Task ProximaCitaWidgetViewComponent_ConCitaFutura_RetornaWidgetConDatos()
    {
        string dbName = Guid.NewGuid().ToString();
        using var db = CrearDb(dbName);
        var fechaCita = DateTime.Now.AddHours(3);
        db.Pacientes.Add(new Paciente { IdPaciente = 1, Nombres = "Juan", Apellidos = "Pérez", Estado = "activo" });
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Limpieza dental", Estado = "activo" });
        db.Consultorios.Add(new Consultorio { IdConsultorio = 1, Nombre = "Consultorio 101", Estado = "disponible" });
        db.Citas.Add(new Cita
        {
            IdCita = 5,
            IdPaciente = 1,
            IdServicio = 1,
            IdConsultorio = 1,
            Estado = "programada",
            FechaHora = fechaCita
        });
        await db.SaveChangesAsync();

        var componente = new ProximaCitaWidgetViewComponent(db);
        var resultado = await componente.InvokeAsync(idPaciente: 1);

        var modelo = Assert.IsType<ProximaCitaWidgetViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.True(modelo.TieneCita);
        Assert.Equal(5, modelo.IdCita);
        Assert.Equal("Juan Pérez", modelo.NombrePaciente);
        Assert.Equal("Limpieza dental", modelo.NombreServicio);
        Assert.Equal("Consultorio 101", modelo.NombreConsultorio);
        Assert.Equal(fechaCita, modelo.FechaHora);
        Assert.Equal("Primera visita", modelo.UltimaAtencion);
    }

    [Fact]
    public async Task ProximaCitaWidgetViewComponent_SinCitas_DevuelveModeloVacio()
    {
        using var db = CrearDb(Guid.NewGuid().ToString());
        var componente = new ProximaCitaWidgetViewComponent(db);

        var resultado = await componente.InvokeAsync();

        var modelo = Assert.IsType<ProximaCitaWidgetViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.False(modelo.TieneCita);
        Assert.Null(modelo.IdCita);
    }

    // ─────────────────────────── ToggleVistas ───────────────────────────

    [Fact]
    public void ToggleVistasViewComponent_SinParametros_DevuelveTresVistasPorDefecto()
    {
        var resultado = new ToggleVistasViewComponent().Invoke();

        var modelo = Assert.IsType<ToggleVistasViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Equal("lista", modelo.VistaActiva);
        Assert.Equal(3, modelo.Vistas.Count);
        Assert.Contains(modelo.Vistas, v => v.Id == "semana");
        Assert.Contains(modelo.Vistas, v => v.Id == "dia");
        Assert.Contains(modelo.Vistas, v => v.Id == "lista");
    }

    [Fact]
    public void ToggleVistasViewComponent_VistasPersonalizadas_SeRespetan()
    {
        var vistas = new List<ToggleVistaItem>
        {
            new ToggleVistaItem { Id = "mes", Etiqueta = "Mes", Icono = "calendar_month" }
        };

        var resultado = new ToggleVistasViewComponent().Invoke(vistaActiva: "mes", vistas: vistas);

        var modelo = Assert.IsType<ToggleVistasViewModel>(
            Assert.IsType<ViewViewComponentResult>(resultado).ViewData.Model);
        Assert.Equal("mes", modelo.VistaActiva);
        Assert.Single(modelo.Vistas);
        Assert.Equal("Mes", modelo.Vistas[0].Etiqueta);
    }
}
