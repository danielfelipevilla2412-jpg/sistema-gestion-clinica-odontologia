using System;
using System.Text;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Controllers;

public class HistoriaClinicaStructuredDataTests
{
    [Fact]
    public async Task GuardarFormularioHistoria_GuardaConsultaRelacionadaSinCrearCita()
    {
        await using var db = await CrearBaseAsync();
        var (controller, paciente, _) = CrearControlador(db);
        db.DiagnosticosCie10.Add(new DiagnosticoCie10
        {
            Codigo = "K02.1",
            Descripcion = "Caries de la dentina",
            VersionCatalogo = "prueba",
            Fuente = "catálogo de pruebas"
        });
        await db.SaveChangesAsync();

        await controller.GuardarFormularioHistoria(new HistoriaFormularioGuardarRequest
        {
            PacienteId = paciente.IdPaciente,
            MotivoConsulta = "Dolor al masticar",
            AntecedentesMedicos = "Hipertensión controlada",
            Alergias = "Penicilina",
            SintomasSignos = ["Dolor", "Sensibilidad"],
            DiagnosticoPrincipalCodigo = "K021",
            EvolucionClinica = "Evaluación inicial"
        });

        var consulta = await db.ConsultasClinicas
            .Include(c => c.Sintomas)
            .Include(c => c.Diagnosticos)
            .SingleAsync();
        var historia = await db.HistoriasClinicas.SingleAsync();
        Assert.Equal(historia.IdHistoria, consulta.IdHistoria);
        Assert.Null(consulta.IdCita);
        Assert.Null(consulta.IdNota);
        Assert.Equal("Dolor al masticar", consulta.MotivoConsulta);
        Assert.Equal("Hipertensión controlada", consulta.AntecedentesMedicos);
        Assert.Equal("Penicilina", consulta.Alergias);
        Assert.Equal(2, consulta.Sintomas.Count);
        Assert.Contains(consulta.Diagnosticos, d => d.CodigoCie10 == "K02.1" && d.EsPrincipal);
        Assert.DoesNotContain("formularioHistoria", historia.ObservacionesGenerales);
    }

    [Fact]
    public async Task GuardarFormularioHistoria_RechazaCodigoCie10NoCargadoSinCrearHistoria()
    {
        await using var db = await CrearBaseAsync();
        var (controller, paciente, _) = CrearControlador(db);

        var result = await controller.GuardarFormularioHistoria(new HistoriaFormularioGuardarRequest
        {
            PacienteId = paciente.IdPaciente,
            DiagnosticoPrincipalCodigo = "K02.1"
        });

        Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Empty(await db.HistoriasClinicas.ToListAsync());
        Assert.Empty(await db.ConsultasClinicas.ToListAsync());
    }

    [Fact]
    public async Task GuardarPlanTratamiento_GuardaServicioEstadoYPiezaFdi()
    {
        await using var db = await CrearBaseAsync();
        var (controller, paciente, profesional) = CrearControlador(db);
        var servicio = new Servicio
        {
            Nombre = "Restauración",
            Estado = "activo",
            Categoria = "operatoria"
        };
        db.Servicios.Add(servicio);
        await db.SaveChangesAsync();

        await controller.GuardarPlanTratamiento(new PlanTratamientoGuardarRequest
        {
            PacienteId = paciente.IdPaciente,
            ServicioId = servicio.IdServicio,
            NumeroFdi = "16",
            Estado = "propuesto",
            Observacion = "Valorar pieza temporal"
        });

        var plan = await db.PlanesTratamiento.SingleAsync();
        Assert.Equal(paciente.IdPaciente, plan.HistoriaClinica!.IdPaciente);
        Assert.Equal(servicio.IdServicio, plan.IdServicio);
        Assert.Equal(profesional.IdProfesional, plan.IdProfesional);
        Assert.Equal("16", plan.NumeroFdi);
        Assert.Equal("propuesto", plan.Estado);
        Assert.Equal("Valorar pieza temporal", plan.Observacion);
    }

    // Yeray: Verifica que las sesiones se registren como eventos y que superar la estimación solo genere una advertencia.
    [Fact]
    public async Task RegistrarSesionPlan_PermiteSuperarEstimacionYAuditaElEvento()
    {
        await using var db = await CrearBaseAsync();
        var (controller, paciente, profesional) = CrearControlador(db);
        var servicio = new Servicio { Nombre = "Control", Estado = "activo", Categoria = "operatoria" };
        db.Servicios.Add(servicio);
        await db.SaveChangesAsync();
        var historia = new HistoriaClinica
        {
            IdPaciente = paciente.IdPaciente,
            FechaApertura = DateTime.UtcNow,
            Activa = true
        };
        db.HistoriasClinicas.Add(historia);
        await db.SaveChangesAsync();
        var plan = new PlanTratamiento
        {
            IdHistoria = historia.IdHistoria,
            IdServicio = servicio.IdServicio,
            IdProfesional = profesional.IdProfesional,
            SesionesEstimadas = 0
        };
        db.PlanesTratamiento.Add(plan);
        await db.SaveChangesAsync();

        var result = await controller.RegistrarSesionPlan(plan.IdPlan, new PlanSesionRegistrarRequest(), CancellationToken.None);

        Assert.IsType<JsonResult>(result);
        var sesion = await db.SesionesPlan.SingleAsync();
        Assert.Equal(plan.IdPlan, sesion.IdPlan);
        Assert.Equal(profesional.IdProfesional, sesion.IdProfesionalRegistro);
        Assert.False(sesion.Anulada);
        Assert.Contains("superan la estimación", (string?)result.GetType().GetProperty("Value")?.GetValue(result)?.GetType().GetProperty("message")?.GetValue(result.GetType().GetProperty("Value")?.GetValue(result)));
        Assert.Contains("Plan_Sesion", (await db.Auditorias.SingleAsync()).TablaAfectada);
    }

    // Yeray: Comprueba que anular conserva el evento, exige motivo y registra la corrección sin borrarlo.
    [Fact]
    public async Task AnularSesionPlan_ConservaRegistroYAuditaMotivo()
    {
        await using var db = await CrearBaseAsync();
        var (controller, paciente, profesional) = CrearControlador(db);
        var servicio = new Servicio { Nombre = "Control", Estado = "activo", Categoria = "operatoria" };
        db.Servicios.Add(servicio);
        await db.SaveChangesAsync();
        var historia = new HistoriaClinica
        {
            IdPaciente = paciente.IdPaciente,
            FechaApertura = DateTime.UtcNow,
            Activa = true
        };
        db.HistoriasClinicas.Add(historia);
        await db.SaveChangesAsync();
        var plan = new PlanTratamiento
        {
            IdHistoria = historia.IdHistoria,
            IdServicio = servicio.IdServicio,
            IdProfesional = profesional.IdProfesional
        };
        db.PlanesTratamiento.Add(plan);
        await db.SaveChangesAsync();
        var sesion = new PlanSesion
        {
            IdPlan = plan.IdPlan,
            IdProfesionalRegistro = profesional.IdProfesional,
            FechaSesion = DateTime.UtcNow
        };
        db.SesionesPlan.Add(sesion);
        await db.SaveChangesAsync();

        var result = await controller.AnularSesionPlan(
            sesion.IdSesion,
            new PlanSesionAnularRequest { Motivo = "Registro duplicado" },
            CancellationToken.None);

        Assert.IsType<JsonResult>(result);
        var stored = await db.SesionesPlan.SingleAsync();
        Assert.True(stored.Anulada);
        Assert.Equal("Registro duplicado", stored.MotivoAnulacion);
        Assert.Equal(profesional.IdProfesional, stored.IdProfesionalAnula);
        var auditoria = await db.Auditorias.SingleAsync();
        Assert.Equal("UPDATE", auditoria.Accion);
        Assert.Equal("Plan_Sesion", auditoria.TablaAfectada);
    }

    [Fact]
    public async Task GuardarFormularioHistoria_VinculaConsultaConNotaYCitaProxima()
    {
        await using var db = await CrearBaseAsync();
        var (controller, paciente, profesional) = CrearControlador(db);
        var citaConsulta = new Cita
        {
            IdPaciente = paciente.IdPaciente,
            IdProfesional = profesional.IdProfesional,
            FechaHora = DateTime.Now.AddDays(1),
            Estado = "programada"
        };
        var citaProxima = new Cita
        {
            IdPaciente = paciente.IdPaciente,
            IdProfesional = profesional.IdProfesional,
            FechaHora = DateTime.Now.AddDays(30),
            Estado = "programada"
        };
        db.Citas.AddRange(citaConsulta, citaProxima);
        db.DiagnosticosCie10.Add(new DiagnosticoCie10
        {
            Codigo = "K02.1",
            Descripcion = "Caries de la dentina",
            VersionCatalogo = "prueba",
            Fuente = "catálogo de pruebas"
        });
        await db.SaveChangesAsync();

        await controller.GuardarFormularioHistoria(new HistoriaFormularioGuardarRequest
        {
            PacienteId = paciente.IdPaciente,
            CitaId = citaConsulta.IdCita,
            ProximaCitaId = citaProxima.IdCita,
            DiagnosticoPrincipalCodigo = "K02.1",
            MotivoConsulta = "Control"
        });

        var consulta = await db.ConsultasClinicas.SingleAsync();
        Assert.Equal(citaConsulta.IdCita, consulta.IdCita);
        Assert.Equal(citaProxima.IdCita, consulta.IdProximaCita);
    }

    [Fact]
    public async Task ExportarHistoriaClinicaPdf_DevuelvePdfParaPacienteAsignado()
    {
        await using var db = await CrearBaseAsync();
        var (controller, paciente, _) = CrearControlador(db);
        db.HistoriasClinicas.Add(new HistoriaClinica
        {
            IdPaciente = paciente.IdPaciente,
            FechaApertura = DateTime.UtcNow,
            Activa = true
        });
        await db.SaveChangesAsync();

        var result = await controller.ExportarHistoriaClinicaPdf(paciente.IdPaciente, CancellationToken.None);

        var archivo = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", archivo.ContentType);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(archivo.FileContents, 0, 5));
    }

    private static async Task<AppDbContext> CrearBaseAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var profesional = new Profesional
        {
            IdUsuario = 7,
            Nombres = "Ana",
            Apellidos = "Odontóloga",
            RegistroMedico = "RM-7"
        };
        var paciente = new Paciente
        {
            TipoDocumento = "CC",
            Documento = "123456",
            Nombres = "Luis",
            Apellidos = "Paciente",
            FechaNacimiento = new DateTime(1990, 1, 1)
        };
        db.Profesionales.Add(profesional);
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
        paciente.IdProfesionalAsignado = profesional.IdProfesional;
        await db.SaveChangesAsync();
        return db;
    }

    private static (HistoriaClinicaController Controller, Paciente Paciente, Profesional Profesional) CrearControlador(
        AppDbContext db)
    {
        var profesional = db.Profesionales.Single();
        var paciente = db.Pacientes.Single();
        var citaService = new CitaService(db, NullLogger<CitaService>.Instance);
        var historiaService = new HistoriaClinicaService(
            db,
            NullLogger<HistoriaClinicaService>.Instance,
            citaService);
        var controller = new HistoriaClinicaController(
            db,
            historiaService,
            new AuditoriaHistoriaClinicaService(db),
            new ConfigurationBuilder().Build(),
            NullLogger<HistoriaClinicaController>.Instance);
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Role, "Profesional")
        ], "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return (controller, paciente, profesional);
    }
}
