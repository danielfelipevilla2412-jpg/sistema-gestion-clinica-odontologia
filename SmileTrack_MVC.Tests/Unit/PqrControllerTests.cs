using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class PqrControllerTests
{
    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PqrController CrearControlador(AppDbContext db, int idUsuario = 10, string rol = "Paciente")
    {
        var controller = new PqrController(db, NullLogger<PqrController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()),
                    new Claim(ClaimTypes.Role, rol)
                ], "TestAuth"))
            }
        };

        controller.ProblemDetailsFactory = new DefaultProblemDetailsFactory(
            Options.Create(new ApiBehaviorOptions()),
            Options.Create(new ProblemDetailsOptions()));

        return controller;
    }

    private static Paciente CrearPaciente(int id, int idUsuario) => new()
    {
        IdPaciente = id,
        IdUsuario = idUsuario,
        TipoDocumento = "CC",
        Documento = "2001",
        Nombres = "María",
        Apellidos = "Gómez",
        FechaNacimiento = new DateTime(1992, 5, 5),
        Estado = "activo"
    };

    [Theory]
    [InlineData("Profesional")]
    [InlineData("Auxiliar")]
    [InlineData("Recepcionista")]
    [InlineData("Administrador")]
    public async Task CrearPqr_PersonalGuardaReporteSinPaciente(string rol)
    {
        await using var db = CrearContext();
        db.Usuarios.Add(new Usuario { IdUsuario = 10, Nombre = "Daniel", Correo = "soporte@example.com" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db, rol: rol);
        var result = await controller.CrearPqr("peticion", "Error de agenda", "No se puede guardar la cita.",
            null, new FakeWebHostEnvironment(), moduloSoftware: "Agenda");

        Assert.IsType<JsonResult>(result);
        var reporte = await db.PQRs.SingleAsync();
        Assert.Null(reporte.IdPaciente);
        Assert.Equal(10, reporte.IdUsuario);
        Assert.Equal("queja", reporte.Tipo);
        Assert.Equal("Agenda", reporte.ModuloSoftware);
    }

    [Theory]
    [InlineData("10000000000")]
    [InlineData("12A45678")]
    public async Task CrearPqr_CedulaInvalidaRechazaLaSolicitud(string documento)
    {
        await using var db = CrearContext();
        var paciente = CrearPaciente(1, 10);
        paciente.Documento = documento;
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();

        var result = await CrearControlador(db).CrearPqr("queja", "Asunto válido",
            "Descripción suficientemente larga", null, new FakeWebHostEnvironment());

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.PQRs.ToListAsync());
    }

    [Fact]
    public async Task CrearPqr_PacienteGuardaProfesionalYFecha()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        db.Profesionales.Add(new() { IdProfesional = 7, Nombres = "Ana", Apellidos = "Pérez", Estado = "activo" });
        await db.SaveChangesAsync();
        var fecha = new DateOnly(2026, 1, 15);

        var controller = CrearControlador(db);
        var result = await controller.CrearPqr("queja", "Asunto válido", "Descripción suficientemente larga",
            null, new FakeWebHostEnvironment(), idProfesional: 7, fechaServicio: fecha);

        Assert.IsType<JsonResult>(result);
        var pqr = await db.PQRs.SingleAsync();
        Assert.Equal(7, pqr.IdProfesional);
        Assert.Equal(fecha, pqr.FechaServicio);
    }

    [Fact]
    public async Task ApiNotificacionesPqr_PacienteSoloRecibeRespuestasPropias()
    {
        await using var db = CrearContext();
        db.Pacientes.AddRange(CrearPaciente(1, 10), CrearPaciente(2, 20));
        db.PQRs.AddRange(
            new PqrEntity { IdPqr = 1, IdPaciente = 1, IdUsuario = 10, FechaRespuesta = DateTime.UtcNow, Asunto = "Respuesta propia" },
            new PqrEntity { IdPqr = 2, IdPaciente = 2, IdUsuario = 20, FechaRespuesta = DateTime.UtcNow, Asunto = "Respuesta ajena" },
            new PqrEntity { IdPqr = 3, IdPaciente = 1, IdUsuario = 10, Asunto = "Sin respuesta" },
            new PqrEntity { IdPqr = 4, IdPaciente = 1, IdUsuario = 20, FechaRespuesta = DateTime.UtcNow, Asunto = "Autor ajeno" },
            new PqrEntity { IdPqr = 5, IdPaciente = 1, FechaRespuesta = DateTime.UtcNow, Asunto = "Autor sin confirmar" });
        await db.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(await CrearControlador(db).ApiNotificacionesPqr(default));
        var aviso = Assert.Single(System.Text.Json.JsonSerializer.SerializeToElement(result.Value).EnumerateArray());
        Assert.Equal(1, aviso.GetProperty("id").GetInt32());
        Assert.EndsWith("st-pac-04-nueva-pqr?pqr=1", aviso.GetProperty("url").GetString());
    }

    [Fact]
    public async Task ApiNotificacionesPqr_AdministradorRecibeSolicitudesNuevas()
    {
        await using var db = CrearContext();
        db.PQRs.AddRange(
            new PqrEntity { IdPqr = 4, Estado = "recibida", Asunto = "Solicitud nueva" },
            new PqrEntity { IdPqr = 5, Estado = "en_proceso", Asunto = "En seguimiento" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db, rol: "Administrador");
        var result = Assert.IsType<OkObjectResult>(await controller.ApiNotificacionesPqr(default));
        var aviso = Assert.Single(System.Text.Json.JsonSerializer.SerializeToElement(result.Value).EnumerateArray());
        Assert.Equal(4, aviso.GetProperty("id").GetInt32());
        Assert.EndsWith("st-adm-17-gestion-pqr?pqr=4", aviso.GetProperty("url").GetString());
    }

    [Theory]
    [InlineData("Profesional")]
    [InlineData("Auxiliar")]
    [InlineData("Recepcionista")]
    public async Task ApiNotificacionesPqr_PersonalSoloRecibeRespuestasDeSusReportes(string rol)
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        db.PQRs.AddRange(
            new PqrEntity { IdPqr = 1, IdUsuario = 10, FechaRespuesta = DateTime.UtcNow },
            new PqrEntity { IdPqr = 2, IdUsuario = 20, FechaRespuesta = DateTime.UtcNow },
            new PqrEntity { IdPqr = 3, IdUsuario = 10 },
            new PqrEntity { IdPqr = 4, IdUsuario = 10, IdPaciente = 1, FechaRespuesta = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db, rol: rol);
        var result = Assert.IsType<OkObjectResult>(await controller.ApiNotificacionesPqr(default));
        var aviso = Assert.Single(System.Text.Json.JsonSerializer.SerializeToElement(result.Value).EnumerateArray());
        Assert.Equal(1, aviso.GetProperty("id").GetInt32());
        Assert.EndsWith("st-pac-04-nueva-pqr?pqr=1", aviso.GetProperty("url").GetString());
    }

    [Theory]
    [InlineData("Paciente")]
    [InlineData("Profesional")]
    public async Task ApiRespuestasPropias_DevuelveRespuestasOrdenadasSoloAlSolicitante(string rol)
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        db.PQRs.Add(new PqrEntity { IdPqr = 1, IdUsuario = 10, IdPaciente = rol == "Paciente" ? 1 : null });
        var fecha = new DateTime(2026, 1, 1);
        db.PqrHistorial.AddRange(
            new PqrHistorial { IdPqr = 1, TipoEvento = "respuesta", Respuesta = "Segunda", FechaEvento = fecha.AddDays(1) },
            new PqrHistorial { IdPqr = 1, TipoEvento = "estado", FechaEvento = fecha },
            new PqrHistorial { IdPqr = 1, TipoEvento = "respuesta", Respuesta = "Primera", FechaEvento = fecha });
        await db.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(await CrearControlador(db, rol: rol).ApiRespuestasPropias(1));
        var respuestas = System.Text.Json.JsonSerializer.SerializeToElement(result.Value).EnumerateArray();
        Assert.Equal(new[] { "Primera", "Segunda" }, respuestas.Select(r => r.GetProperty("texto").GetString()).ToArray());
        Assert.IsType<NotFoundResult>(await CrearControlador(db, idUsuario: 20, rol: rol).ApiRespuestasPropias(1));
    }

    [Theory]
    [InlineData("pacientes", 1)]
    [InlineData("soporte", 2)]
    public async Task ApiListarPqrs_CombinaOrigenConEstado(string origen, int esperado)
    {
        await using var db = CrearContext();
        db.PQRs.AddRange(
            new PqrEntity { IdPqr = 1, IdPaciente = 1, Estado = "en_proceso" },
            new PqrEntity { IdPqr = 2, ModuloSoftware = "Agenda", Estado = "en_proceso" },
            new PqrEntity { IdPqr = 3, ModuloSoftware = "Agenda", Estado = "cerrada" },
            new PqrEntity { IdPqr = 4, Estado = "en_proceso" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db, rol: "Administrador");
        var result = Assert.IsType<OkObjectResult>(await controller.ApiListarPqrs(
            origen: origen, estado: "en_proceso"));
        Assert.Equal(new[] { esperado }, ObtenerIdsPqr(result));
    }

    private static int Contar(IEnumerable datos)
    {
        int total = 0;
        foreach (var _ in datos) total++;
        return total;
    }

    [Fact]
    public async Task Stpac04NuevaPqr_ConPaciente_RetornaViewConViewData()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        db.PQRs.Add(new PqrEntity { IdPqr = 1, IdPaciente = 1, IdUsuario = 10, Asunto = "Queja", Descripcion = "Descripción válida", Estado = "recibida", FechaCreacion = DateTime.Now });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Stpac04NuevaPqr();

        var view = Assert.IsType<ViewResult>(result);
        var json = Assert.IsType<string>(view.ViewData["MisPqrsJson"]);
        Assert.Contains("Queja", json);
    }

    [Fact]
    public async Task Stpac04NuevaPqr_SinPacienteAsociado_RetornaViewConJsonVacio()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Stpac04NuevaPqr();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("[]", view.ViewData["MisPqrsJson"]);
    }

    [Fact]
    public async Task CrearPqr_AsuntoCorto_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.CrearPqr("peticion", "ab", "Descripción suficientemente larga", null, new FakeWebHostEnvironment());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CrearPqr_DescripcionCorta_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.CrearPqr("peticion", "Asunto válido", "corta", null, new FakeWebHostEnvironment());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CrearPqr_SinPacienteAsociado_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.CrearPqr("peticion", "Asunto válido", "Descripción suficientemente larga", null, new FakeWebHostEnvironment());

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var mensaje = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)!.ToString();
        Assert.Contains("paciente", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await db.PQRs.CountAsync());
    }

    [Fact]
    public async Task CrearPqr_TipoInvalido_NormalizaAPeticionYPersiste()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.CrearPqr("invalido", "Asunto válido", "Descripción suficientemente larga", null, new FakeWebHostEnvironment());

        var json = Assert.IsType<JsonResult>(result);
        Assert.True((bool)json.Value!.GetType().GetProperty("success")!.GetValue(json.Value)!);

        var pqr = await db.PQRs.SingleAsync();
        Assert.Equal("peticion", pqr.Tipo);
        Assert.Equal("recibida", pqr.Estado);
        Assert.Equal("media", pqr.Prioridad);
        Assert.Equal(1, pqr.IdPaciente);
    }

    [Fact]
    public async Task CrearPqr_ArchivoMuyGrande_RetornaBadRequest()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);
        var archivo = CrearArchivo(6 * 1024 * 1024, "image/jpeg", "foto.jpg");

        var result = await controller.CrearPqr("peticion", "Asunto válido", "Descripción suficientemente larga", archivo, new FakeWebHostEnvironment());

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var mensaje = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)!.ToString();
        Assert.Contains("5 MB", mensaje);
        Assert.Equal(0, await db.PQRs.CountAsync());
    }

    [Fact]
    public async Task CrearPqr_TipoArchivoNoPermitido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);
        var archivo = CrearArchivo(10, "text/plain", "nota.txt");

        var result = await controller.CrearPqr("peticion", "Asunto válido", "Descripción suficientemente larga", archivo, new FakeWebHostEnvironment());

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var mensaje = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)!.ToString();
        Assert.Contains("no permitido", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await db.PQRs.CountAsync());
    }

    [Fact]
    public async Task ApiListarPqrs_ConPqrs_RetornaOkConEstadisticas()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        db.PQRs.AddRange(
            new PqrEntity { IdPqr = 1, IdPaciente = 1, Asunto = "Uno", Descripcion = "Descripción válida", Estado = "recibida", FechaCreacion = DateTime.Now },
            new PqrEntity { IdPqr = 2, IdPaciente = 1, Asunto = "Dos", Descripcion = "Descripción válida", Estado = "en_proceso", FechaCreacion = DateTime.Now });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ApiListarPqrs(ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        var pqrs = Assert.IsAssignableFrom<IEnumerable>(ok.Value!.GetType().GetProperty("pqrs")!.GetValue(ok.Value));
        Assert.Equal(2, Contar(pqrs));
        var stats = ok.Value!.GetType().GetProperty("stats")!.GetValue(ok.Value)!;
        Assert.Equal(2, (int)stats.GetType().GetProperty("total")!.GetValue(stats)!);
    }

    [Fact]
    public async Task ApiListarPqrs_TreintaRegistros_PaginaEnBloquesDe25SinDuplicados()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        for (var id = 1; id <= 30; id++)
        {
            db.PQRs.Add(new PqrEntity
            {
                IdPqr = id,
                IdPaciente = 1,
                Tipo = "peticion",
                Asunto = $"Solicitud {id}",
                Descripcion = "Descripción de prueba",
                Estado = "recibida",
                FechaCreacion = new DateTime(2026, 11, 1).AddMinutes(id)
            });
        }
        await db.SaveChangesAsync();
        var controller = CrearControlador(db);

        var primera = Assert.IsType<OkObjectResult>(await controller.ApiListarPqrs(pagina: 1));
        var segunda = Assert.IsType<OkObjectResult>(await controller.ApiListarPqrs(pagina: 2));
        var idsPrimera = ObtenerIdsPqr(primera);
        var idsSegunda = ObtenerIdsPqr(segunda);

        Assert.Equal(25, idsPrimera.Count);
        Assert.Equal(5, idsSegunda.Count);
        Assert.Empty(idsPrimera.Intersect(idsSegunda));
        Assert.Equal(30, idsPrimera.Concat(idsSegunda).Distinct().Count());
        Assert.Equal(30, LeerEnteroPaginacion(primera, "totalFiltrado"));
        Assert.Equal(2, LeerEnteroPaginacion(primera, "totalPaginas"));
        Assert.Equal(25, LeerEnteroPaginacion(primera, "tamanoPagina"));
        Assert.Equal(1, LeerEnteroPaginacion(primera, "pagina"));
        Assert.Equal(2, LeerEnteroPaginacion(segunda, "pagina"));
        Assert.Equal(30, LeerEnteroEstadistica(segunda, "total"));
    }

    [Fact]
    public async Task ApiListarPqrs_Filtros_RecalculanTotalYPaginaSinModificarEstadisticasGlobales()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        for (var id = 1; id <= 30; id++)
        {
            db.PQRs.Add(new PqrEntity
            {
                IdPqr = id,
                IdPaciente = 1,
                Tipo = id <= 12 ? "queja" : "peticion",
                Asunto = id == 3 ? "Consulta especial" : $"Solicitud {id}",
                Descripcion = "Descripción de prueba",
                Estado = id <= 8 ? "en_proceso" : "recibida",
                FechaCreacion = new DateTime(2026, 11, 1).AddMinutes(id)
            });
        }
        await db.SaveChangesAsync();
        var controller = CrearControlador(db);

        var filtrada = Assert.IsType<OkObjectResult>(await controller.ApiListarPqrs(
            pagina: 2, tipo: "queja", estado: "en_proceso"));
        Assert.Equal(8, ObtenerIdsPqr(filtrada).Count);
        Assert.Equal(8, LeerEnteroPaginacion(filtrada, "totalFiltrado"));
        Assert.Equal(1, LeerEnteroPaginacion(filtrada, "totalPaginas"));
        Assert.Equal(1, LeerEnteroPaginacion(filtrada, "pagina"));
        Assert.Equal(30, LeerEnteroEstadistica(filtrada, "total"));

        var buscada = Assert.IsType<OkObjectResult>(await controller.ApiListarPqrs(
            buscar: "PQR-0003"));
        Assert.Equal(new[] { 3 }, ObtenerIdsPqr(buscada));
        Assert.Equal(1, LeerEnteroPaginacion(buscada, "totalFiltrado"));
    }

    private static object LeerPropiedad(object objeto, string nombre) =>
        objeto.GetType().GetProperty(nombre)?.GetValue(objeto)
        ?? throw new InvalidOperationException($"No existe la propiedad '{nombre}'.");

    private static int LeerEnteroPaginacion(OkObjectResult resultado, string propiedad) =>
        (int)LeerPropiedad(LeerPropiedad(resultado.Value!, "paginacion"), propiedad);

    private static int LeerEnteroEstadistica(OkObjectResult resultado, string propiedad) =>
        (int)LeerPropiedad(LeerPropiedad(resultado.Value!, "stats"), propiedad);

    private static System.Collections.Generic.List<int> ObtenerIdsPqr(OkObjectResult resultado)
    {
        var registros = (IEnumerable)LeerPropiedad(resultado.Value!, "pqrs");
        return registros.Cast<object>().Select(p => (int)LeerPropiedad(p, "id")).ToList();
    }

    [Fact]
    public async Task ApiActualizarEstado_ModelStateInvalido_RetornaValidationProblem()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);
        controller.ModelState.AddModelError("Estado", "Estado inválido.");

        var result = await controller.ApiActualizarEstado(1, new ActualizarEstadoPqrRequest { Estado = "resuelta" }, CancellationToken.None);

        var objeto = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(400, objeto.StatusCode);
    }

    [Fact]
    public async Task ApiActualizarEstado_PqrExistente_ActualizaElEstado()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        db.PQRs.Add(new PqrEntity { IdPqr = 1, IdPaciente = 1, Asunto = "Uno", Descripcion = "Descripción válida", Estado = "recibida", FechaCreacion = DateTime.Now });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ApiActualizarEstado(1, new ActualizarEstadoPqrRequest { Estado = "resuelta" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var pqr = await db.PQRs.FindAsync(1);
        Assert.Equal("resuelta", pqr!.Estado);
    }

    [Fact]
    public async Task ApiActualizarEstado_PqrInexistente_RetornaNotFound()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.ApiActualizarEstado(999, new ActualizarEstadoPqrRequest { Estado = "resuelta" }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ApiResponderPqr_RegistraRespuestaYCambiaEstadoAEnProceso()
    {
        await using var db = CrearContext();
        db.Usuarios.Add(new Usuario { IdUsuario = 10, Nombre = "Admin", Apellidos = "Prueba", Correo = "admin@test.local", Contrasena = "hash", IdRol = 1, Estado = "activo" });
        db.Pacientes.Add(CrearPaciente(1, 20));
        db.PQRs.Add(new PqrEntity { IdPqr = 1, IdPaciente = 1, Asunto = "Uno", Descripcion = "Descripción válida", Estado = "recibida", FechaCreacion = DateTime.Now });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db, idUsuario: 10);

        var result = await controller.ApiResponderPqr(1, new ResponderPqrRequest { Respuesta = "Hemos revisado su caso." }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var pqr = await db.PQRs.FindAsync(1);
        Assert.Equal("Hemos revisado su caso.", pqr!.Respuesta);
        Assert.Equal(10, pqr.AtendidaPor);
        Assert.Equal("en_proceso", pqr.Estado);
        Assert.NotNull(pqr.FechaRespuesta);
    }

    [Fact]
    public async Task ApiResponderPqr_PqrInexistente_RetornaNotFound()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.ApiResponderPqr(999, new ResponderPqrRequest { Respuesta = "Hemos revisado su caso." }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData("resuelta")]
    [InlineData("cerrada")]
    [InlineData("rechazada")]
    public async Task ApiActualizarEstado_PqrFinalizada_NoPermiteReapertura(string estado)
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        db.PQRs.Add(new PqrEntity { IdPqr = 1, IdPaciente = 1, Asunto = "Uno", Descripcion = "Descripción válida", Estado = estado, FechaCreacion = DateTime.Now });
        await db.SaveChangesAsync();
        var result = await CrearControlador(db).ApiActualizarEstado(1, new ActualizarEstadoPqrRequest { Estado = "en_proceso" });
        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(estado, (await db.PQRs.FindAsync(1))!.Estado);
    }

    [Theory]
    [InlineData("resuelta")]
    [InlineData("cerrada")]
    [InlineData("rechazada")]
    public async Task ApiResponderPqr_PqrFinalizada_NoPermiteRespuesta(string estado)
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        db.PQRs.Add(new PqrEntity { IdPqr = 1, IdPaciente = 1, Asunto = "Uno", Descripcion = "Descripción válida", Estado = estado, FechaCreacion = DateTime.Now });
        await db.SaveChangesAsync();
        var result = await CrearControlador(db).ApiResponderPqr(1, new ResponderPqrRequest { Respuesta = "Respuesta de prueba" });
        Assert.IsType<ConflictObjectResult>(result);
        Assert.Null((await db.PQRs.FindAsync(1))!.Respuesta);
    }

    [Fact]
    public async Task ApiActualizarEstado_NoPermiteRegresarARecibida()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        db.PQRs.Add(new PqrEntity { IdPqr = 1, IdPaciente = 1, Asunto = "Uno", Descripcion = "Descripción válida", Estado = "en_proceso", FechaCreacion = DateTime.Now });
        await db.SaveChangesAsync();
        var result = await CrearControlador(db).ApiActualizarEstado(1, new ActualizarEstadoPqrRequest { Estado = "recibida" });
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task ApiResponderPqr_PermiteRespuestaAdicionalYRegistraHistorial()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, 10));
        db.PQRs.Add(new PqrEntity { IdPqr = 1, IdPaciente = 1, Asunto = "Uno", Descripcion = "Descripción válida", Estado = "en_proceso", Respuesta = "Respuesta original", FechaCreacion = DateTime.Now });
        await db.SaveChangesAsync();

        var result = await CrearControlador(db).ApiResponderPqr(1, new ResponderPqrRequest { Respuesta = "Respuesta nueva" });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Respuesta nueva", (await db.PQRs.FindAsync(1))!.Respuesta);
        var evento = Assert.Single(await db.PqrHistorial.ToListAsync());
        Assert.Equal(1, evento.IdPqr);
        Assert.Equal("respuesta", evento.TipoEvento);
        Assert.Equal("en_proceso", evento.EstadoAnterior);
        Assert.Equal("en_proceso", evento.EstadoNuevo);
        Assert.Equal("Respuesta nueva", evento.Respuesta);
    }

    private static IFormFile CrearArchivo(long longitud, string contentType, string nombre)
        => new FakeFormFile(longitud, contentType, nombre);

    private sealed class FakeFormFile : IFormFile
    {
        public FakeFormFile(long longitud, string contentType, string nombre)
        {
            Length = longitud;
            ContentType = contentType;
            FileName = nombre;
        }

        public string ContentType { get; }
        public string ContentDisposition => string.Empty;
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public long Length { get; }
        public string Name => "evidencia";
        public string FileName { get; }
        public void CopyTo(Stream target) { }
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Stream OpenReadStream() => new MemoryStream();
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SmileTrack.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "smiletrack-tests-wwwroot");
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
