using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

public sealed class PanelOperativoService : IPanelOperativoService
{
    private readonly AppDbContext _context;

    public PanelOperativoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PanelOperativoViewModel> ObtenerAsync(
        CancellationToken ct = default)
    {
        var ahora = DateTime.Now;
        var inicio = ahora.Date;
        var fin = inicio.AddDays(1);
        var cultura = new System.Globalization.CultureInfo("es-CO");

        var citas = await _context.Citas
            .AsNoTracking()
            .Where(c => c.FechaHora >= inicio && c.FechaHora < fin)
            .OrderBy(c => c.FechaHora)
            .Select(c => new PanelCitaProjection
            {
                Id = c.IdCita,
                FechaHora = c.FechaHora,
                Estado = c.Estado,
                PacienteNombres = c.Paciente == null ? null : c.Paciente.Nombres,
                PacienteApellidos = c.Paciente == null ? null : c.Paciente.Apellidos,
                Alergia = c.Paciente == null ? null : c.Paciente.Alergias,
                Telefono = c.Paciente == null ? null : c.Paciente.Telefono,
                Email = c.Paciente == null ? null : c.Paciente.Correo,
                Sangre = c.Paciente == null ? null : c.Paciente.GrupoSanguineo,
                FechaNacimiento = c.Paciente == null ? null : c.Paciente.FechaNacimiento,
                Antecedentes = c.Paciente == null ? null : c.Paciente.AntecedentesMedicos,
                ProfesionalNombres = c.Profesional == null ? null : c.Profesional.Nombres,
                ProfesionalApellidos = c.Profesional == null ? null : c.Profesional.Apellidos,
                Consultorio = c.Consultorio == null ? null : c.Consultorio.Nombre,
                Servicio = c.Servicio == null ? null : c.Servicio.Nombre
            })
            .ToListAsync(ct);

        var consultoriosDisponibles = await _context.Consultorios
            .AsNoTracking()
            .CountAsync(c => c.Estado == "disponible" || c.Estado == "activo", ct);

        var estados = citas
            .Select(c => MapEstadoLabel(c.Estado))
            .ToList();

        var total = estados.Count;
        var completadas = estados.Count(estado => estado == "Atendida");
        var canceladas = estados.Count(estado => estado == "Cancelada");

        var citasViewModel = citas
            .Select(c =>
            {
                var nombrePaciente = ConstruirNombre(c.PacienteNombres, c.PacienteApellidos);
                var alergia = string.IsNullOrWhiteSpace(c.Alergia) ? null : c.Alergia;

                return new PanelOperativoCitaViewModel
                {
                    Id = c.Id,
                    Hora = c.FechaHora.ToString("HH:mm"),
                    Paciente = nombrePaciente,
                    Profesional = ConstruirNombreProfesional(c.ProfesionalNombres, c.ProfesionalApellidos),
                    Alergia = alergia,
                    Consultorio = c.Consultorio ?? "Sin asignar",
                    Estado = MapEstadoLabel(c.Estado),
                    Highlight = alergia is not null,
                    Telefono = c.Telefono,
                    Email = c.Email,
                    Sangre = string.IsNullOrWhiteSpace(c.Sangre) ? "N/D" : c.Sangre,
                    Edad = c.FechaNacimiento.HasValue
                        ? $"{CalcularEdad(c.FechaNacimiento.Value, ahora.Date)} años"
                        : "Edad no registrada",
                    Antecedentes = string.IsNullOrWhiteSpace(c.Antecedentes)
                        ? "Información no registrada"
                        : c.Antecedentes,
                    Servicio = c.Servicio ?? "Servicio no especificado",
                    MedicamentosDisponibles = false,
                    Medicamentos = []
                };
            })
            .ToList();

        var proxima = citas
            .Where(c => c.FechaHora > ahora && MapEstadoLabel(c.Estado) == "Pendiente")
            .OrderBy(c => c.FechaHora)
            .Select(c => new PanelOperativoProximaCitaViewModel
            {
                MinutosRestantes = Math.Max(0, (int)(c.FechaHora - ahora).TotalMinutes),
                Hora = c.FechaHora.ToString("hh:mm tt"),
                Paciente = ConstruirNombre(c.PacienteNombres, c.PacienteApellidos),
                Tipo = c.Servicio ?? "Consulta",
                Profesional = ConstruirNombreProfesional(c.ProfesionalNombres, c.ProfesionalApellidos),
                Consultorio = c.Consultorio ?? "Sin asignar"
            })
            .FirstOrDefault();

        var alertas = citasViewModel
            .Where(c => c.Alergia is not null)
            .Select(c => new PanelOperativoAlertaViewModel
            {
                Tipo = "warning",
                Titulo = "Paciente con alergia",
                Descripcion = $"{c.Paciente} — Alérgico a {c.Alergia}"
            })
            .ToList();

        return new PanelOperativoViewModel
        {
            FechaHoy = inicio.ToString("dddd d 'de' MMMM yyyy", cultura),
            Kpis = new PanelOperativoKpisViewModel
            {
                CitasHoy = total,
                Completadas = completadas,
                Pendientes = Math.Max(0, total - completadas - canceladas),
                ConsultoriosDisponibles = consultoriosDisponibles
            },
            ProximaCita = proxima,
            Citas = citasViewModel,
            Alertas = alertas
        };
    }

    private static string MapEstadoLabel(string? estado)
    {
        var normalizado = (estado ?? string.Empty).Trim().ToLowerInvariant();
        return normalizado switch
        {
            "atendida" or "completada" or "realizada" => "Atendida",
            "cancelada" or "cancelado" or "no_asistida" or "no asistio" or "no asistió" or "no-show" => "Cancelada",
            _ => "Pendiente"
        };
    }

    private static string ConstruirNombre(string? nombres, string? apellidos)
    {
        var nombre = $"{nombres} {apellidos}".Trim();
        return string.IsNullOrWhiteSpace(nombre) ? "Paciente sin datos" : nombre;
    }

    private static string ConstruirNombreProfesional(string? nombres, string? apellidos)
    {
        var nombre = $"{nombres} {apellidos}".Trim();
        return string.IsNullOrWhiteSpace(nombre) ? "Sin asignar" : $"Dr(a). {nombre}";
    }

    private static int CalcularEdad(DateTime nacimiento, DateTime hoy)
    {
        var edad = hoy.Year - nacimiento.Year;
        if (nacimiento.Date > hoy.AddYears(-edad)) edad--;
        return edad;
    }

    private sealed class PanelCitaProjection
    {
        public int Id { get; init; }
        public DateTime FechaHora { get; init; }
        public string? Estado { get; init; }
        public string? PacienteNombres { get; init; }
        public string? PacienteApellidos { get; init; }
        public string? Alergia { get; init; }
        public string? Telefono { get; init; }
        public string? Email { get; init; }
        public string? Sangre { get; init; }
        public DateTime? FechaNacimiento { get; init; }
        public string? Antecedentes { get; init; }
        public string? ProfesionalNombres { get; init; }
        public string? ProfesionalApellidos { get; init; }
        public string? Consultorio { get; init; }
        public string? Servicio { get; init; }
    }
}
