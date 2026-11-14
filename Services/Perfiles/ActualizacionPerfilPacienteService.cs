
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;

namespace SmileTrack_MVC.Services.Perfiles;

public class ActualizacionPerfilPacienteService
{
    private readonly AppDbContext _context;
    private readonly AuditoriaHistoriaClinicaService _auditoria;

    public ActualizacionPerfilPacienteService(
        AppDbContext context,
        AuditoriaHistoriaClinicaService auditoria)
    {
        _context = context;
        _auditoria = auditoria;
    }

    public async Task<ResultadoActualizacionPerfilDto>
        ActualizarAsync(
            int idUsuario,
            ActualizarDatosPacienteDto datos,
            HttpContext httpContext,
            CancellationToken ct = default)
    {
        ResultadoActualizacionPerfilDto Error(
            string mensaje, int codigo = 400) => new()
        {
            Success = false,
            Message = mensaje,
            StatusCode = codigo
        };

        if (datos == null)
            return Error("Debes enviar los datos personales.");

        string tipo = datos.TipoDocumento?
            .Trim().ToUpperInvariant() ?? "";

        string documento = datos.Documento?
            .Trim().ToUpperInvariant() ?? "";

        DateTime? fecha = datos.FechaNacimiento?.Date;

        if (tipo is not ("CC" or "TI" or "CE" or "PAS"))
            return Error("Tipo de documento inválido.");

        string patron = tipo == "PAS"
            ? "^[A-Z0-9]{5,20}$"
            : "^[0-9]{7,15}$";

        if (!Regex.IsMatch(documento, patron))
            return Error("Número de documento inválido.");

        if (fecha == null ||
            fecha < new DateTime(1900, 1, 1) ||
            fecha > DateTime.UtcNow.Date)
        {
            return Error("Fecha de nacimiento inválida.");
        }

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(
                u => u.IdUsuario == idUsuario, ct);

        if (usuario == null)
            return Error("Usuario no encontrado.", 404);

        var paciente = await _context.Pacientes
            .FirstOrDefaultAsync(
                p => p.IdUsuario == idUsuario, ct);

        bool existiaPaciente = paciente != null;

        var fichaExistente = await _context.Pacientes
            .FirstOrDefaultAsync(
                p => p.Documento == documento &&
                     p.IdUsuario != idUsuario, ct);

        if (fichaExistente != null)
        {
            bool puedeVincular =
                paciente == null &&
                fichaExistente.IdUsuario == null &&
                string.Equals(
                    fichaExistente.Estado, "activo",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    fichaExistente.TipoDocumento, tipo,
                    StringComparison.OrdinalIgnoreCase) &&
                Normalizar(fichaExistente.Nombres) ==
                    Normalizar(usuario.Nombre) &&
                Normalizar(fichaExistente.Apellidos) ==
                    Normalizar(usuario.Apellidos) &&
                fichaExistente.FechaNacimiento.Date == fecha.Value &&
                !EsProvisional(fichaExistente.Documento);

            if (!puedeVincular)
            {
                return Error(
                    "Los datos no coinciden con la ficha clínica " +
                    "existente. Solicita revisión en recepción.",
                    409);
            }

            fichaExistente.IdUsuario = idUsuario;
            usuario.FechaNacimiento =
                fichaExistente.FechaNacimiento.Date;

            _auditoria.Agregar(
                httpContext,
                "UPDATE",
                "Paciente",
                fichaExistente.IdPaciente,
                "Ficha clínica vinculada tras validar " +
                "documento, nombres, apellidos y nacimiento.");

            await _context.SaveChangesAsync(ct);

            return new ResultadoActualizacionPerfilDto
            {
                Success = true,
                Message = "Ficha clínica vinculada correctamente.",
                StatusCode = 200
            };
        }

        if (existiaPaciente &&
            !EsProvisional(paciente!.Documento) &&
            !string.IsNullOrWhiteSpace(paciente.Documento) &&
            (paciente.TipoDocumento != tipo ||
             paciente.Documento != documento ||
             paciente.FechaNacimiento.Date != fecha.Value))
        {
            return Error(
                "La ficha ya tiene una identidad registrada. " +
                "Solicita revisión en recepción para modificarla.",
                409);
        }

        if (paciente == null)
        {
            paciente = new Paciente
            {
                IdUsuario = idUsuario,
                Nombres = usuario.Nombre,
                Apellidos = usuario.Apellidos,
                Correo = usuario.Correo,
                FechaRegistro = DateTime.UtcNow.Date,
                Estado = "activo"
            };

            _context.Pacientes.Add(paciente);
        }

        paciente.TipoDocumento = tipo;
        paciente.Documento = documento;
        paciente.FechaNacimiento = fecha.Value;
        usuario.FechaNacimiento = fecha.Value;

        await _context.SaveChangesAsync(ct);

        return new ResultadoActualizacionPerfilDto
        {
            Success = true,
            Message = "Datos personales guardados correctamente.",
            StatusCode = 200
        };
    }

    private static bool EsProvisional(string? documento)
    {
        return !string.IsNullOrWhiteSpace(documento) &&
            (documento.StartsWith(
                "TEMP", StringComparison.OrdinalIgnoreCase) ||
             documento.StartsWith(
                "USR-", StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalizar(string? texto)
    {
        return string.Join(
            ' ',
            (texto ?? "").Split(
                ' ', StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }
}
