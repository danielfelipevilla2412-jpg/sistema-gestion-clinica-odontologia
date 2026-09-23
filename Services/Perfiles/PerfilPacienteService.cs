using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;

namespace SmileTrack_MVC.Services.Perfiles;

public sealed class PerfilPacienteService : IPerfilPacienteService
{
    private readonly AppDbContext _context;

    public PerfilPacienteService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<InfoBasicaPacienteDto?> ObtenerInfoBasicaAsync(int idPaciente, CancellationToken ct = default)
    {
        var paciente = await _context.Pacientes
            .AsNoTracking()
            .Include(p => p.ProfesionalAsignado)
            .ThenInclude(profesional => profesional!.Usuario)
            .FirstOrDefaultAsync(p => p.IdPaciente == idPaciente, ct);

        if (paciente is null)
            return null;

        return new InfoBasicaPacienteDto
        {
            IdPaciente = paciente.IdPaciente,
            IdUsuario = paciente.IdUsuario ?? 0,
            NombreCompleto = paciente.NombresCompleto,
            Nombres = paciente.Nombres,
            Apellidos = paciente.Apellidos,
            TipoDocumento = paciente.TipoDocumento,
            Documento = paciente.Documento,
            DocumentoOculto = OcultarDocumento(paciente.Documento),
            Correo = paciente.Correo ?? paciente.Usuario?.Correo ?? string.Empty,
            Telefono = paciente.Telefono ?? string.Empty,
            FechaNacimiento = paciente.FechaNacimiento,
            Edad = CalcularEdad(paciente.FechaNacimiento),
            Genero = paciente.Genero ?? "No especificado",
            EstadoCivil = paciente.EstadoCivil,
            Direccion = paciente.Direccion,
            Ciudad = paciente.Ciudad,
            Departamento = paciente.Departamento,
            Estado = paciente.Estado,
            FechaRegistro = paciente.FechaRegistro,
            ProfesionalAsignado = paciente.ProfesionalAsignado is null ? null : new ProfesionalAsignadoDto
            {
                IdProfesional = paciente.ProfesionalAsignado.IdProfesional,
                NombreCompleto = paciente.ProfesionalAsignado.Usuario is not null
                    ? $"{paciente.ProfesionalAsignado.Usuario.Nombre} {paciente.ProfesionalAsignado.Usuario.Apellidos}".Trim()
                    : $"{paciente.ProfesionalAsignado.Nombres} {paciente.ProfesionalAsignado.Apellidos}".Trim(),
                Telefono = paciente.ProfesionalAsignado.Telefono
            }
        };
    }

    public async Task<InfoMedicaPacienteDto?> ObtenerInfoMedicaAsync(int idPaciente, CancellationToken ct = default)
    {
        return await _context.Pacientes
            .AsNoTracking()
            .Where(p => p.IdPaciente == idPaciente)
            .Select(p => new InfoMedicaPacienteDto
            {
                GrupoSanguineo = p.GrupoSanguineo,
                Alergias = p.Alergias,
                MedicamentosActuales = p.MedicamentosActuales ?? p.Medicamentos,
                AntecedentesMedicos = p.AntecedentesMedicos,
                AntecedentesFamiliares = p.AntecedentesFamiliares,
                EpsAseguradora = p.EpsAseguradora,
                TipoAfiliacion = p.TipoAfiliacion,
                NumeroPoliza = p.NumeroPoliza,
                ContactoEmergencia = p.ContactoEmergencia,
                TelefonoEmergencia = p.TelefonoEmergencia,
                ParentescoEmergencia = p.ParentescoEmergencia
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EstadisticasPacienteDto> ObtenerEstadisticasAsync(int idPaciente, CancellationToken ct = default)
    {
        var citas = await _context.Citas.AsNoTracking()
            .Where(c => c.IdPaciente == idPaciente)
            .Select(c => new { c.IdCita, c.FechaHora, c.Estado })
            .ToListAsync(ct);

        var pendientes = citas.Where(c => EsEstado(c.Estado, "agendada", "programada", "confirmada")).ToList();
        var completadas = citas.Where(c => EsEstado(c.Estado, "completada", "atendida")).ToList();
        var canceladas = citas.Count(c => EsEstado(c.Estado, "cancelada", "no asistió", "no asistio"));
        var ahora = DateTime.Now;

        var proxima = await _context.Citas.AsNoTracking()
            .Include(c => c.Profesional).ThenInclude(p => p!.Usuario)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.IdPaciente == idPaciente && c.FechaHora >= ahora &&
                (c.Estado == "Agendada" || c.Estado == "Programada" || c.Estado == "Confirmada"))
            .OrderBy(c => c.FechaHora)
            .FirstOrDefaultAsync(ct);

        var fechaRegistro = await _context.Pacientes.AsNoTracking()
            .Where(p => p.IdPaciente == idPaciente)
            .Select(p => (DateTime?)p.FechaRegistro)
            .FirstOrDefaultAsync(ct);
        var antiguedad = CalcularAntiguedad(fechaRegistro);

        return new EstadisticasPacienteDto
        {
            TotalCitas = citas.Count,
            CitasPendientes = pendientes.Count,
            CitasCompletadas = completadas.Count,
            CitasCanceladas = canceladas,
            ProximaCita = proxima is null ? null : new ProximaCitaDto
            {
                IdCita = proxima.IdCita,
                FechaHora = proxima.FechaHora,
                Profesional = proxima.Profesional?.Usuario is not null
                    ? $"{proxima.Profesional.Usuario.Nombre} {proxima.Profesional.Usuario.Apellidos}".Trim()
                    : proxima.Profesional is not null ? $"{proxima.Profesional.Nombres} {proxima.Profesional.Apellidos}".Trim() : "Por asignar",
                Servicio = proxima.Servicio?.Nombre ?? "Consulta general",
                Consultorio = proxima.Consultorio?.Nombre ?? "Por asignar"
            },
            TieneHistoriaClinica = await _context.HistoriasClinicas.AsNoTracking().AnyAsync(h => h.IdPaciente == idPaciente, ct),
            FechaUltimaConsulta = completadas.OrderByDescending(c => c.FechaHora).Select(c => (DateTime?)c.FechaHora).FirstOrDefault(),
            Antiguedad = antiguedad,
            FrecuenciaVisitas = DeterminarFrecuencia(citas.Count, antiguedad)
        };
    }

    public async Task<ResumenCompletoPacienteDto?> ObtenerResumenCompletoAsync(int idPaciente, CancellationToken ct = default)
    {
        var basica = await ObtenerInfoBasicaAsync(idPaciente, ct);
        if (basica is null)
            return null;

        var infoMedica = await ObtenerInfoMedicaAsync(idPaciente, ct) ?? new InfoMedicaPacienteDto();
        var estadisticas = await ObtenerEstadisticasAsync(idPaciente, ct);
        return new ResumenCompletoPacienteDto { InfoBasica = basica, InfoMedica = infoMedica, Estadisticas = estadisticas };
    }

    private static bool EsEstado(string estado, params string[] estados) => estados.Any(e => string.Equals(estado, e, StringComparison.OrdinalIgnoreCase));
    private static int CalcularEdad(DateTime? nacimiento)
    {
        if (!nacimiento.HasValue) return 0;
        var edad = DateTime.Today.Year - nacimiento.Value.Year;
        if (nacimiento.Value.Date > DateTime.Today.AddYears(-edad)) edad--;
        return Math.Max(edad, 0);
    }
    private static int CalcularAntiguedad(DateTime? registro) => registro.HasValue ? Math.Max(0, DateTime.Today.Year - registro.Value.Year - (registro.Value.Date > DateTime.Today.AddYears(-(DateTime.Today.Year - registro.Value.Year)) ? 1 : 0)) : 0;
    private static string DeterminarFrecuencia(int total, int antiguedad) => total == 0 ? "Nueva" : antiguedad == 0 ? "Nueva" : (double)total / antiguedad >= 4 ? "Alta" : (double)total / antiguedad >= 2 ? "Media" : "Baja";
    private static string OcultarDocumento(string documento) => string.IsNullOrWhiteSpace(documento) || documento.Length < 4 ? "****" : $"***{documento[^4..]}";
}