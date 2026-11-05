using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Facturacion;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.Facturacion;
using Xunit;

namespace SmileTrack_MVC.Tests.Services;

public sealed class FacturacionServiceTests
{
    [Fact]
    public async Task CrearDesdeCita_CreaFacturaConOrigenYPrecioHistorico()
    {
        await using var context = CrearContext();
        AgregarDatosBase(context, "atendida");
        await context.SaveChangesAsync();

        var factura = await new FacturacionService(context).CrearDesdeCitaAsync(1, 99, default);

        Assert.Equal(1, factura.IdCita);
        Assert.Equal(1, factura.IdPaciente);
        Assert.Equal(125000m, factura.Total);
        Assert.Single(factura.Detalles);
        Assert.Equal(125000m, factura.Detalles[0].PrecioUnitario);
    }

    [Fact]
    public async Task CrearDesdeCita_RechazaCitaNoAtendida()
    {
        await using var context = CrearContext();
        AgregarDatosBase(context, "confirmada");
        await context.SaveChangesAsync();

        var service = new FacturacionService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CrearDesdeCitaAsync(1, 99, default));
    }

    [Fact]
    public async Task CrearDesdeCita_EvitaFacturaActivaDuplicada()
    {
        await using var context = CrearContext();
        AgregarDatosBase(context, "completada");
        await context.SaveChangesAsync();
        var service = new FacturacionService(context);

        await service.CrearDesdeCitaAsync(1, 99, default);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CrearDesdeCitaAsync(1, 99, default));
        Assert.Contains("factura activa", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegistrarPago_CreaHistorialYActualizaEstado()
    {
        await using var context = CrearContext();
        AgregarDatosBase(context, "atendida");
        await context.SaveChangesAsync();
        var service = new FacturacionService(context);
        var factura = await service.CrearDesdeCitaAsync(1, 99, default);

        var parcial = await service.RegistrarPagoAsync(factura.Id, new() { MontoPagado = 50000, MetodoPago = "efectivo" }, 99, default);
        var pagada = await service.RegistrarPagoAsync(factura.Id, new() { MontoPagado = 75000, MetodoPago = "efectivo" }, 99, default);

        Assert.Equal("parcial", parcial!.Estado);
        Assert.Equal("pagada", pagada!.Estado);
        Assert.Equal(2, pagada.Pagos.Count);
        Assert.Equal(125000m, pagada.MontoPagado);
    }

    [Fact]
    public async Task CrearConPago_GuardaFacturaProfesionalDetalleYPago()
    {
        await using var context = CrearContext();
        AgregarDatosBase(context, "atendida");
        context.Profesionales.Add(new Profesional
        {
            IdProfesional = 1,
            Nombres = "Profesional",
            Apellidos = "Prueba",
            RegistroMedico = "RM-1",
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var factura = await new FacturacionService(context).CrearConPagoAsync(new CrearFacturaConPagoRequest
        {
            IdPaciente = 1,
            IdProfesional = 1,
            MontoPagado = 50000m,
            MetodoPago = "efectivo",
            Detalles = new()
            {
                new() { IdServicio = 1, Descripcion = "Consulta", Cantidad = 1, PrecioUnitario = 125000m }
            }
        }, 99, default);

        Assert.Equal(1, factura.IdProfesional);
        Assert.Equal(50000m, factura.MontoPagado);
        Assert.Equal(75000m, factura.Pendiente);
        Assert.Equal("parcial", factura.Estado);
        Assert.Single(factura.Detalles);
        Assert.Single(factura.Pagos);
        Assert.Equal(50000m, factura.Pagos[0].Monto);
    }

    private static void AgregarDatosBase(AppDbContext context, string estado)
    {
        context.Usuarios.Add(new Usuario { IdUsuario = 99, Nombre = "Operador", Apellidos = "Prueba", Correo = "operador@test.local", Contrasena = "hash", IdRol = 1, Estado = "activo" });
        context.Pacientes.Add(new Paciente { IdPaciente = 1, Documento = "1001", Nombres = "Paciente", Apellidos = "Prueba", FechaNacimiento = DateTime.Today.AddYears(-30), FechaRegistro = DateTime.Today, Estado = "activo" });
        context.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 125000m, Estado = "activo" });
        context.Citas.Add(new Cita { IdCita = 1, IdPaciente = 1, IdServicio = 1, FechaHora = DateTime.Now.AddDays(-1), Estado = estado });
    }

    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}