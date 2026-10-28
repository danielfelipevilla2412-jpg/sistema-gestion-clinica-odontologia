using System;
using System.Collections.Generic;

namespace SmileTrack_MVC.Models.DTOs
{
    public class CitaReasignadaInfo
    {
        public int IdCita { get; set; }
        public int IdPaciente { get; set; }
        public DateTime FechaHora { get; set; }
        public bool Reasignada { get; set; }
        public int? IdNuevoProfesional { get; set; }
        public string? NombreNuevoProfesional { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }

    public class ResultadoReasignacionAusenciaDto
    {
        public int IdProfesional { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int TotalCitasAfectadas { get; set; }
        public int TotalReasignadas { get; set; }
        public int TotalCanceladas { get; set; }
        public List<CitaReasignadaInfo> DetallesCitas { get; set; } = new();
    }
}
