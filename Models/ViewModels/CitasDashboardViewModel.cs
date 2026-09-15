namespace SmileTrack_MVC.Models.ViewModels;

/// <summary>
/// ViewModel raíz del dashboard de citas para el administrador.
/// Reemplaza el saco de ViewData dinámico que tenía la vista anterior.
/// </summary>
public sealed class CitasDashboardViewModel
{
    public DashboardKpiViewModel        Kpis             { get; init; } = new();
    public DashboardEstadosViewModel    Estados          { get; init; } = new();
    public int                          OcupacionPorcentaje { get; init; }
    public IReadOnlyList<TopProfesionalViewModel> TopProfesionales { get; init; } = [];
}

/// <summary>Indicadores principales de la tarjeta de KPIs.</summary>
public sealed class DashboardKpiViewModel
{
    public int     PacientesActivos            { get; init; }
    public int     CitasHoy                   { get; init; }
    public int     ProfesionalesActivos        { get; init; }
    public decimal ValorEstimadoAtendidoMes    { get; init; }
    public int     VariacionCitasSemana        { get; init; }
}

/// <summary>
/// Distribución mensual de citas por estado.
/// Los porcentajes se calculan sobre el total real (atendidas + confirmadas + programadas + canceladas),
/// NO sobre el estado con mayor cantidad (bug del denominador maxEstado).
/// </summary>
public sealed class DashboardEstadosViewModel
{
    public int Total       { get; init; }
    public int Atendidas   { get; init; }
    public int Confirmadas { get; init; }
    public int Programadas { get; init; }
    public int Canceladas  { get; init; }

    public int PorcentajeAtendidas   => Calcular(Atendidas);
    public int PorcentajeConfirmadas => Calcular(Confirmadas);
    public int PorcentajeProgramadas => Calcular(Programadas);
    public int PorcentajeCanceladas  => Calcular(Canceladas);

    private int Calcular(int valor) =>
        Total <= 0 ? 0 : (int)Math.Round(valor * 100d / Total);
}

/// <summary>Entrada del ranking de los 3 profesionales con más citas del mes.</summary>
public sealed class TopProfesionalViewModel
{
    public int    IdProfesional { get; init; }
    public string Nombre       { get; init; } = "Sin nombre";
    public string Especialidad { get; init; } = "General";
    public int    TotalCitas   { get; init; }
}
