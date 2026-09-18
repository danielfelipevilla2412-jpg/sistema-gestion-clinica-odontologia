using System;
using System.Collections.Generic;

namespace SmileTrack_MVC.Models.DTOs
{
    public class ReporteComisionServicioDto
    {
        public int IdServicio { get; set; }
        public string NombreServicio { get; set; } = string.Empty;
        public int CantidadAtendida { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal SubtotalFacturado { get; set; }
        public decimal HonorariosGenerados { get; set; }
    }

    public class ReporteComisionProfesionalDto
    {
        public int IdProfesional { get; set; }
        public string NombreProfesional { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int TotalCitasAtendidas { get; set; }
        public decimal MontoTotalFacturado { get; set; }
        public decimal PorcentajeComision { get; set; }
        public decimal MontoTotalHonorarios { get; set; }
        public List<ReporteComisionServicioDto> DetalleServicios { get; set; } = new();
    }
}
