using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

// Yeray: Centraliza la fusión conservadora para trasladar datos sin mezclar historias clínicas con contenido.
public sealed record ResultadoFusionPaciente(bool Fusionada, string Codigo, string Mensaje);

// Yeray: Reasigna relaciones en una transacción y audita bloqueos sin modificar pacientes.
public sealed class PacienteFusionService(
    AppDbContext context,
    AuditoriaHistoriaClinicaService auditoria)
{
    public async Task<ResultadoFusionPaciente> FusionarAsync(
        HttpContext httpContext,
        int idPrincipal,
        int idDuplicado,
        CancellationToken cancellationToken = default)
    {
        if (idPrincipal <= 0 || idDuplicado <= 0 || idPrincipal == idDuplicado)
            return new(false, "ids-invalidos", "Selecciona dos pacientes diferentes y válidos.");

        var executionStrategy = context.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(() => FusionarEnTransaccionAsync(
            httpContext,
            idPrincipal,
            idDuplicado,
            cancellationToken));
    }

    private async Task<ResultadoFusionPaciente> FusionarEnTransaccionAsync(
        HttpContext httpContext,
        int idPrincipal,
        int idDuplicado,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var pacientes = await context.Pacientes
            .Where(p => p.IdPaciente == idPrincipal || p.IdPaciente == idDuplicado)
            .ToListAsync(cancellationToken);
        if (pacientes.Count != 2)
            return new(false, "no-encontrados", "No se encontraron ambos pacientes.");

        var principal = pacientes.Single(p => p.IdPaciente == idPrincipal);
        var duplicado = pacientes.Single(p => p.IdPaciente == idDuplicado);

        if (!string.Equals(principal.Estado, "activo", StringComparison.OrdinalIgnoreCase))
            return new(false, "principal-inactivo", "El paciente principal debe estar activo.");

        if (principal.IdUsuario.HasValue && duplicado.IdUsuario.HasValue)
        {
            return await RegistrarBloqueoAsync(
                httpContext,
                idPrincipal,
                idDuplicado,
                "Ambos pacientes tienen usuario de acceso. Requiere revisión manual.",
                "conflicto-usuarios",
                transaction,
                cancellationToken);
        }

        var historias = await context.HistoriasClinicas
            .Where(h => h.IdPaciente == idPrincipal || h.IdPaciente == idDuplicado)
            .ToListAsync(cancellationToken);

        HistoriaClinica? historiaPrincipal = historias.SingleOrDefault(h => h.IdPaciente == idPrincipal);
        HistoriaClinica? historiaDuplicada = historias.SingleOrDefault(h => h.IdPaciente == idDuplicado);
        bool principalTieneContenido = historiaPrincipal is not null
            && await TieneContenidoClinicoAsync(historiaPrincipal, idPrincipal, cancellationToken);
        bool duplicadoTieneContenido = historiaDuplicada is not null
            && await TieneContenidoClinicoAsync(historiaDuplicada, idDuplicado, cancellationToken);

        if (principalTieneContenido && duplicadoTieneContenido)
        {
            return await RegistrarBloqueoAsync(
                httpContext,
                idPrincipal,
                idDuplicado,
                "Ambos pacientes tienen historia clínica. Requiere revisión manual.",
                "conflicto-historias",
                transaction,
                cancellationToken);
        }

            int? idUsuarioTransferido = null;
            if (!principal.IdUsuario.HasValue && duplicado.IdUsuario.HasValue)
            {
                idUsuarioTransferido = duplicado.IdUsuario;
                duplicado.IdUsuario = null;
                await context.SaveChangesAsync(cancellationToken);
            }

        await ConsolidarHistoriaAsync(
            historiaPrincipal,
            historiaDuplicada,
            principalTieneContenido,
            duplicadoTieneContenido,
            principal.IdPaciente,
            cancellationToken);
        ConsolidarDatosPaciente(principal, duplicado);
            principal.IdUsuario ??= idUsuarioTransferido;
        await ReasignarRelacionesAsync(idPrincipal, idDuplicado, cancellationToken);

        duplicado.Estado = "inactivo";

        auditoria.Agregar(
            httpContext,
            "UPDATE",
            "Paciente",
            idPrincipal,
            $"Fusión de pacientes; principal={idPrincipal}; duplicado={idDuplicado}; secundario desactivado.");
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return new(true, "fusionada", "Pacientes fusionados. El registro secundario quedó inactivo.");
    }

    private async Task<bool> TieneContenidoClinicoAsync(
        HistoriaClinica historia,
        int idPaciente,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(historia.ObservacionesGenerales))
            return true;

        int idHistoria = historia.IdHistoria;
        if (await context.ConsultasClinicas.AnyAsync(c => c.IdHistoria == idHistoria, cancellationToken)
            || await context.NotasClinicas.AnyAsync(n => n.IdHistoria == idHistoria, cancellationToken)
            || await context.RegistrosOdontograma.AnyAsync(r => r.IdHistoria == idHistoria, cancellationToken)
            || await context.DocumentosClinicos.AnyAsync(d => d.IdHistoria == idHistoria, cancellationToken)
            || await context.PlanesTratamiento.AnyAsync(p => p.IdHistoria == idHistoria, cancellationToken))
        {
            return true;
        }

        return await context.ControlesPostoperatorios.AnyAsync(
            control => context.Citas.Any(cita =>
                cita.IdCita == control.IdCita && cita.IdPaciente == idPaciente),
            cancellationToken);
    }

    private async Task ConsolidarHistoriaAsync(
        HistoriaClinica? historiaPrincipal,
        HistoriaClinica? historiaDuplicada,
        bool principalTieneContenido,
        bool duplicadoTieneContenido,
        int idPacientePrincipal,
        CancellationToken cancellationToken)
    {
        if (historiaPrincipal is null && historiaDuplicada is not null)
        {
            historiaDuplicada.IdPaciente = idPacientePrincipal;
            return;
        }

        if (historiaPrincipal is null || historiaDuplicada is null)
            return;

        if (duplicadoTieneContenido && !principalTieneContenido)
        {
            context.HistoriasClinicas.Remove(historiaPrincipal);
            await context.SaveChangesAsync(cancellationToken);
            historiaDuplicada.IdPaciente = idPacientePrincipal;
            return;
        }

        context.HistoriasClinicas.Remove(historiaDuplicada);
    }

    private static void ConsolidarDatosPaciente(Paciente principal, Paciente duplicado)
    {
        principal.Telefono = PreferirActual(principal.Telefono, duplicado.Telefono);
        principal.Correo = PreferirActual(principal.Correo, duplicado.Correo);
        principal.Direccion = PreferirActual(principal.Direccion, duplicado.Direccion);
        principal.Ciudad = PreferirActual(principal.Ciudad, duplicado.Ciudad);
        principal.Genero = PreferirActual(principal.Genero, duplicado.Genero);
        principal.GrupoSanguineo = PreferirActual(principal.GrupoSanguineo, duplicado.GrupoSanguineo);
        principal.Alergias = PreferirActual(principal.Alergias, duplicado.Alergias);
        principal.Medicamentos = PreferirActual(principal.Medicamentos, duplicado.Medicamentos);
        principal.AntecedentesMedicos = PreferirActual(principal.AntecedentesMedicos, duplicado.AntecedentesMedicos);
        principal.ContactoEmergencia = PreferirActual(principal.ContactoEmergencia, duplicado.ContactoEmergencia);
        principal.TelefonoEmergencia = PreferirActual(principal.TelefonoEmergencia, duplicado.TelefonoEmergencia);
        principal.ArchivoAdjunto = PreferirActual(principal.ArchivoAdjunto, duplicado.ArchivoAdjunto);
        principal.EstadoCivil = PreferirActual(principal.EstadoCivil, duplicado.EstadoCivil);
        principal.Departamento = PreferirActual(principal.Departamento, duplicado.Departamento);
        principal.ParentescoEmergencia = PreferirActual(principal.ParentescoEmergencia, duplicado.ParentescoEmergencia);
        principal.EpsAseguradora = PreferirActual(principal.EpsAseguradora, duplicado.EpsAseguradora);
        principal.NumeroPoliza = PreferirActual(principal.NumeroPoliza, duplicado.NumeroPoliza);
        principal.TipoAfiliacion = PreferirActual(principal.TipoAfiliacion, duplicado.TipoAfiliacion);
        principal.MedicamentosActuales = PreferirActual(principal.MedicamentosActuales, duplicado.MedicamentosActuales);
        principal.AntecedentesFamiliares = PreferirActual(principal.AntecedentesFamiliares, duplicado.AntecedentesFamiliares);
        principal.IdProfesionalAsignado ??= duplicado.IdProfesionalAsignado;
        principal.IdUsuario ??= duplicado.IdUsuario;
    }

    private async Task ReasignarRelacionesAsync(
        int idPrincipal,
        int idDuplicado,
        CancellationToken cancellationToken)
    {
        foreach (var cita in await context.Citas.Where(x => x.IdPaciente == idDuplicado).ToListAsync(cancellationToken))
            cita.IdPaciente = idPrincipal;
        foreach (var factura in await context.Facturas.Where(x => x.IdPaciente == idDuplicado).ToListAsync(cancellationToken))
            factura.IdPaciente = idPrincipal;
        foreach (var alergia in await context.AlergiasPaciente.Where(x => x.IdPaciente == idDuplicado).ToListAsync(cancellationToken))
            alergia.IdPaciente = idPrincipal;
        foreach (var pqr in await context.PQRs.Where(x => x.IdPaciente == idDuplicado).ToListAsync(cancellationToken))
            pqr.IdPaciente = idPrincipal;
        foreach (var notificacion in await context.Notificaciones.Where(x => x.IdPaciente == idDuplicado).ToListAsync(cancellationToken))
            notificacion.IdPaciente = idPrincipal;

        var lecturasPrincipales = await context.NotificacionesLeidas
            .Where(x => x.IdPaciente == idPrincipal)
            .ToListAsync(cancellationToken);
        var lecturasDuplicadas = await context.NotificacionesLeidas
            .Where(x => x.IdPaciente == idDuplicado)
            .ToListAsync(cancellationToken);
        foreach (var lectura in lecturasDuplicadas)
        {
            var existente = lecturasPrincipales.FirstOrDefault(x => x.IdCita == lectura.IdCita);
            if (existente is null)
            {
                lectura.IdPaciente = idPrincipal;
                continue;
            }

            if (lectura.FechaLectura < existente.FechaLectura)
                existente.FechaLectura = lectura.FechaLectura;
            context.NotificacionesLeidas.Remove(lectura);
        }
    }

    private async Task<ResultadoFusionPaciente> RegistrarBloqueoAsync(
        HttpContext httpContext,
        int idPrincipal,
        int idDuplicado,
        string mensaje,
        string codigo,
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        auditoria.Agregar(
            httpContext,
            "INSERT",
            "Paciente",
            idDuplicado,
            $"Fusión bloqueada; principal={idPrincipal}; duplicado={idDuplicado}; motivo={mensaje}");
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return new(false, codigo, mensaje);
    }

    private static string? PreferirActual(string? actual, string? nuevo) =>
        string.IsNullOrWhiteSpace(actual) ? nuevo : actual;
}