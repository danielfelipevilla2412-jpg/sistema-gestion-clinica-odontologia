using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmileTrack_MVC.Controllers.Api.Facturacion;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Facturacion;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Services.Email;
using SmileTrack_MVC.Services.Facturacion;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class FacturacionApiControllerTests
{
    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(System.Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static FacturacionApiController CrearControlador(
        AppDbContext db,
        int? idUsuario = 1,
        CapturingEmailService? emailService = null)
    {
        var service = new FacturacionService(db);
        var controller = new FacturacionApiController(
            service,
            db,
            NullLogger<FacturacionApiController>.Instance,
            emailService ?? new CapturingEmailService());

        var claims = new List<Claim> { new(ClaimTypes.Role, "Administrador") };
        if (idUsuario.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, idUsuario.Value.ToString()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        controller.ProblemDetailsFactory = new DefaultProblemDetailsFactory(
            Options.Create(new ApiBehaviorOptions()),
            Options.Create(new ProblemDetailsOptions()));

        return controller;
    }

    private static Paciente CrearPaciente(int id) => new()
    {
        IdPaciente = id,
        TipoDocumento = "CC",
        Documento = "1001",
        Nombres = "Paciente",
        Apellidos = "Prueba",
        FechaNacimiento = new System.DateTime(1990, 1, 1),
        Estado = "activo"
    };

    private static CrearFacturaConPagoRequest CrearRequestEfectivo(int idPaciente, decimal monto, string metodo = "efectivo") => new()
    {
        IdPaciente = idPaciente,
        MontoPagado = monto,
        MetodoPago = metodo,
        Detalles = new List<FacturaDetalleRequest>
        {
            new() { Descripcion = "Consulta odontológica", Cantidad = 1, PrecioUnitario = 100000m, Descuento = 0m }
        }
    };

    private static int Contar(IEnumerable datos)
    {
        int total = 0;
        foreach (var _ in datos) total++;
        return total;
    }

    [Fact]
    public async Task Listar_ConFacturas_RetornaOkConDatos()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1));
        db.Facturas.Add(new Factura { IdFactura = 1, NumeroFactura = "FAC-2026-00001", IdPaciente = 1, FechaFactura = System.DateTime.Today, Total = 100000m, Estado = "pendiente" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Listar(null, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        var data = Assert.IsAssignableFrom<IEnumerable>(ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value));
        Assert.Equal(1, Contar(data));
    }

    [Fact]
    public async Task EnviarRecordatoriosVencidas_AgrupaFacturasYOmiteLasNoVencidas()
    {
        await using var db = CrearContext();
        var pacienteConCorreo = CrearPaciente(1);
        pacienteConCorreo.Correo = "paciente@example.com";
        db.Pacientes.AddRange(pacienteConCorreo, CrearPaciente(2));
        db.Facturas.AddRange(
            new Factura
            {
                IdFactura = 1,
                NumeroFactura = "FAC-001",
                IdPaciente = 1,
                FechaFactura = ColombiaClock.Today.AddDays(-16),
                Total = 100000m,
                MontoPagado = 25000m,
                Estado = "pendiente"
            },
            new Factura
            {
                IdFactura = 2,
                NumeroFactura = "FAC-002",
                IdPaciente = 1,
                FechaFactura = ColombiaClock.Today.AddDays(-20),
                Total = 50000m,
                Estado = "pendiente"
            },
            new Factura
            {
                IdFactura = 3,
                NumeroFactura = "FAC-003",
                IdPaciente = 1,
                FechaFactura = ColombiaClock.Today.AddDays(-15),
                Total = 70000m,
                Estado = "pendiente"
            },
            new Factura
            {
                IdFactura = 4,
                NumeroFactura = "FAC-004",
                IdPaciente = 1,
                FechaFactura = ColombiaClock.Today.AddDays(-30),
                Total = 30000m,
                Estado = "pagada"
            },
            new Factura
            {
                IdFactura = 5,
                NumeroFactura = "FAC-005",
                IdPaciente = 2,
                FechaFactura = ColombiaClock.Today.AddDays(-16),
                Total = 40000m,
                Estado = "pendiente"
            });
        await db.SaveChangesAsync();

        var emailService = new CapturingEmailService();
        var controller = CrearControlador(db, emailService: emailService);

        var result = await controller.EnviarRecordatoriosVencidas(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(3, (int)ok.Value!.GetType().GetProperty("facturas")!.GetValue(ok.Value)!);
        Assert.Equal(1, (int)ok.Value.GetType().GetProperty("enviados")!.GetValue(ok.Value)!);
        Assert.Equal(1, (int)ok.Value.GetType().GetProperty("fallidos")!.GetValue(ok.Value)!);
        Assert.Equal("paciente@example.com", emailService.UltimoDestinatario);
        Assert.Equal(
            new[] { "FAC-002", "FAC-001" },
            emailService.UltimasFacturas.Select(f => f.NumeroFactura).ToArray());
        Assert.Equal(5, await db.Facturas.CountAsync());
    }

    [Fact]
    public async Task Obtener_FacturaInexistente_RetornaNotFound()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Obtener(999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Obtener_FacturaExistente_RetornaOkConData()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1));
        db.Facturas.Add(new Factura { IdFactura = 5, NumeroFactura = "FAC-2026-00002", IdPaciente = 1, FechaFactura = System.DateTime.Today, Total = 80000m, Estado = "pendiente" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Obtener(5, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        Assert.NotNull(ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value));
    }

    [Fact]
    public async Task CrearConPago_ModelStateInvalido_RetornaValidationProblem()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);
        controller.ModelState.AddModelError("MontoPagado", "El monto es obligatorio.");

        var result = await controller.CrearConPago(CrearRequestEfectivo(1, 0m), CancellationToken.None);

        var objeto = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(400, objeto.StatusCode);
    }

    [Fact]
    public async Task CrearConPago_SinUsuarioIdentificado_RetornaUnauthorized()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db, idUsuario: null);

        var result = await controller.CrearConPago(CrearRequestEfectivo(1, 100000m), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task CrearConPago_MetodoNoEfectivo_RetornaBadRequestYNoPersiste()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1));
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.CrearConPago(CrearRequestEfectivo(1, 100000m, "tarjeta"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await db.Facturas.CountAsync());
    }

    [Fact]
    public async Task CrearConPago_DatosValidos_RetornaCreatedYPersisteFacturaYPago()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1));
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.CrearConPago(CrearRequestEfectivo(1, 100000m), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.Equal(1, await db.Facturas.CountAsync());
        Assert.Equal(1, await db.Pagos.CountAsync());
    }

    [Fact]
    public async Task CrearDesdeCita_CitaAtendida_RetornaCreatedYPersisteFactura()
    {
        await using var db = CrearContext();
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "Operador",
            Apellidos = "Prueba",
            Correo = "operador@test.local",
            Contrasena = "hash",
            IdRol = 1,
            Estado = "activo"
        });
        db.Pacientes.Add(CrearPaciente(1));
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 125000m });
        db.Citas.Add(new Cita
        {
            IdCita = 1,
            IdPaciente = 1,
            IdServicio = 1,
            FechaHora = System.DateTime.Today,
            Estado = "atendida"
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.CrearDesdeCita(1, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.True((bool)created.Value!.GetType().GetProperty("success")!.GetValue(created.Value)!);
        Assert.Equal(1, await db.Facturas.CountAsync());
    }

    [Fact]
    public async Task Pago_FacturaInexistente_RetornaNotFound()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Pago(999, new RegistrarPagoRequest { MontoPagado = 100m, MetodoPago = "efectivo" }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Pagos_FacturaInexistente_RetornaNotFound()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Pagos(999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Eliminar_FacturaPendienteSinPagos_RetornaOkYElimina()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1));
        db.Facturas.Add(new Factura { IdFactura = 1, NumeroFactura = "FAC-2026-00003", IdPaciente = 1, FechaFactura = System.DateTime.Today, Total = 50000m, MontoPagado = 0m, Estado = "pendiente" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Eliminar(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, await db.Facturas.CountAsync());
    }

    private sealed class CapturingEmailService : IEmailService
    {
        public string? UltimoDestinatario { get; private set; }
        public IReadOnlyList<FacturaRecordatorioEmail> UltimasFacturas { get; private set; } = [];

        public Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendCitaNotificacionAsync(
            string recipientEmail,
            string nombrePaciente,
            System.DateTime fechaCita,
            string profesional,
            string servicio,
            string nuevoEstado,
            CancellationToken cancellationToken = default,
            string? motivoCancelacion = null,
            string? mensajePersonalizado = null)
            => Task.CompletedTask;

        public Task SendFacturaRecordatorioAsync(
            string recipientEmail,
            string nombrePaciente,
            IReadOnlyList<FacturaRecordatorioEmail> facturas,
            CancellationToken cancellationToken = default)
        {
            UltimoDestinatario = recipientEmail;
            UltimasFacturas = facturas;
            return Task.CompletedTask;
        }

        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(
            string correoDestino,
            CancellationToken cancellationToken = default)
            => Task.FromResult((true, "OK"));
    }
}
