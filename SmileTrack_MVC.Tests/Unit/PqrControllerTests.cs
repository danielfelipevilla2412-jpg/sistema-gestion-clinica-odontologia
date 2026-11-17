using System;
using System.Collections;
using System.IO;
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

    private static PqrController CrearControlador(AppDbContext db, int idUsuario = 10)
    {
        var controller = new PqrController(db, NullLogger<PqrController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()),
                    new Claim(ClaimTypes.Role, "Paciente")
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

        var result = await controller.ApiListarPqrs(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        var pqrs = Assert.IsAssignableFrom<IEnumerable>(ok.Value!.GetType().GetProperty("pqrs")!.GetValue(ok.Value));
        Assert.Equal(2, Contar(pqrs));
        var stats = ok.Value!.GetType().GetProperty("stats")!.GetValue(ok.Value)!;
        Assert.Equal(2, (int)stats.GetType().GetProperty("total")!.GetValue(stats)!);
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
