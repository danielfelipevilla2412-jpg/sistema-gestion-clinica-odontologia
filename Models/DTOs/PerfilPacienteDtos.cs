using System.Text.Json.Serialization;

namespace SmileTrack_MVC.Models.DTOs;

public sealed class InfoBasicaPacienteDto
{
    public int IdPaciente { get; set; }
    public int IdUsuario { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    [JsonIgnore]
    public string Documento { get; set; } = string.Empty;
    public string DocumentoOculto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public DateTime? FechaNacimiento { get; set; }
    public int Edad { get; set; }
    public string Genero { get; set; } = string.Empty;
    public string? EstadoCivil { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Departamento { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public ProfesionalAsignadoDto? ProfesionalAsignado { get; set; }
}

public sealed class ProfesionalAsignadoDto
{
    public int IdProfesional { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Especialidad { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}

public sealed class InfoMedicaPacienteDto
{
    public string? GrupoSanguineo { get; set; }
    public string? Alergias { get; set; }
    public string? MedicamentosActuales { get; set; }
    public string? AntecedentesMedicos { get; set; }
    public string? AntecedentesFamiliares { get; set; }
    public string? EpsAseguradora { get; set; }
    public string? TipoAfiliacion { get; set; }
    public string? NumeroPoliza { get; set; }
    public string? ContactoEmergencia { get; set; }
    public string? TelefonoEmergencia { get; set; }
    public string? ParentescoEmergencia { get; set; }
}

public sealed class EstadisticasPacienteDto
{
    public int TotalCitas { get; set; }
    public int CitasPendientes { get; set; }
    public int CitasCompletadas { get; set; }
    public int CitasCanceladas { get; set; }
    public ProximaCitaDto? ProximaCita { get; set; }
    public bool TieneHistoriaClinica { get; set; }
    public DateTime? FechaUltimaConsulta { get; set; }
    public int TotalTratamientos { get; set; }
    public int Antiguedad { get; set; }
    public string FrecuenciaVisitas { get; set; } = string.Empty;
}

public sealed class ProximaCitaDto
{
    public int IdCita { get; set; }
    public DateTime FechaHora { get; set; }
    public string Profesional { get; set; } = string.Empty;
    public string Servicio { get; set; } = string.Empty;
    public string Consultorio { get; set; } = string.Empty;
}

public sealed class ResumenCompletoPacienteDto
{
    public InfoBasicaPacienteDto InfoBasica { get; set; } = new();
    public InfoMedicaPacienteDto InfoMedica { get; set; } = new();
    public EstadisticasPacienteDto Estadisticas { get; set; } = new();
}