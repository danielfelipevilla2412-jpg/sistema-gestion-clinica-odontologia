using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Facturacion;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.Facturacion;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class FacturacionServicePaymentTests
{
    [Fact]
    public async Task CrearConPagoAsync_PersistsPartialPaymentAndOutstandingBalance()
    {
        await using var db = CreateDb();
        var patient = new Paciente
        {
            TipoDocumento = "CC",
            Documento = Guid.NewGuid().ToString("N")[..10],
            Nombres = "Paciente",
            Apellidos = "Prueba",
            FechaNacimiento = DateTime.Today,
            Estado = "activo"
        };
        db.Pacientes.Add(patient);
        await db.SaveChangesAsync();

        var service = new FacturacionService(db);
        var result = await service.CrearConPagoAsync(new CrearFacturaConPagoRequest
        {
            IdPaciente = patient.IdPaciente,
            MontoPagado = 25000m,
            MetodoPago = "efectivo",
            Detalles = new List<FacturaDetalleRequest>
            {
                new() { Descripcion = "Consulta odontológica", Cantidad = 1, PrecioUnitario = 60000m, Descuento = 5000m }
            }
        }, 7, CancellationToken.None);

        Assert.Equal(60000m, result.Subtotal);
        Assert.Equal(5000m, result.Descuento);
        Assert.Equal(55000m, result.Total);
        Assert.Equal(25000m, result.MontoPagado);
        Assert.Equal(30000m, result.Pendiente);
        Assert.Equal("parcial", result.Estado);
        var detail = Assert.Single(result.Detalles);
        Assert.Equal(5000m, detail.Descuento);
        Assert.Equal(55000m, detail.Subtotal);
        var payment = Assert.Single(result.Pagos);
        Assert.Equal(25000m, payment.Monto);
        Assert.Equal("efectivo", payment.MetodoPago);
        Assert.Equal(1, await db.Facturas.CountAsync());
        Assert.Equal(1, await db.PagosFacturas.CountAsync());
    }

    [Fact]
    public async Task CrearConPagoAsync_RejectsAbonoAboveTotalWithoutCreatingInvoice()
    {
        await using var db = CreateDb();
        var patient = new Paciente
        {
            TipoDocumento = "CC",
            Documento = Guid.NewGuid().ToString("N")[..10],
            Nombres = "Paciente",
            Apellidos = "Prueba",
            FechaNacimiento = DateTime.Today,
            Estado = "activo"
        };
        db.Pacientes.Add(patient);
        await db.SaveChangesAsync();

        var service = new FacturacionService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CrearConPagoAsync(new CrearFacturaConPagoRequest
        {
            IdPaciente = patient.IdPaciente,
            MontoPagado = 65000m,
            MetodoPago = "efectivo",
            Detalles = new List<FacturaDetalleRequest>
            {
                new() { Descripcion = "Consulta odontológica", Cantidad = 1, PrecioUnitario = 60000m }
            }
        }, 7, CancellationToken.None));

        Assert.Empty(await db.Facturas.ToListAsync());
        Assert.Empty(await db.PagosFacturas.ToListAsync());
    }

    [Fact]
    public async Task RegistrarPagoAsync_StoresCashMovementAndUpdatesInvoiceBalance()
    {
        await using var db = CreateDb();
        var invoice = await CreateInvoiceAsync(db, "FAC-2026-00020", 100000m, 25000m, "parcial");

        var service = new FacturacionService(db);
        var result = await service.RegistrarPagoAsync(
            invoice.IdFactura,
            new RegistrarPagoRequest { MontoPagado = 30000m, MetodoPago = "efectivo" },
            7,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(55000m, result.MontoPagado);
        Assert.Equal(45000m, result.Pendiente);
        Assert.Equal("parcial", result.Estado);
        var payment = Assert.Single(result.Pagos);
        Assert.Equal(30000m, payment.Monto);
        Assert.Equal("efectivo", payment.MetodoPago);
        Assert.Equal("aprobado", payment.Estado);
        Assert.StartsWith("CAJ-", payment.Referencia);

        var completed = await service.RegistrarPagoAsync(
            invoice.IdFactura,
            new RegistrarPagoRequest { MontoPagado = 45000m, MetodoPago = "efectivo" },
            7,
            CancellationToken.None);

        Assert.NotNull(completed);
        Assert.Equal(100000m, completed.MontoPagado);
        Assert.Equal(0m, completed.Pendiente);
        Assert.Equal("pagada", completed.Estado);
        Assert.Equal(2, completed.Pagos.Count);
    }

    [Fact]
    public async Task RegistrarPagoAsync_RejectsElectronicMethodUntilGatewayIsConfigured()
    {
        await using var db = CreateDb();
        var invoice = await CreateInvoiceAsync(db, "FAC-2026-00021", 100000m, 0m, "pendiente");

        var service = new FacturacionService(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegistrarPagoAsync(
            invoice.IdFactura,
            new RegistrarPagoRequest { MontoPagado = 100000m, MetodoPago = "tarjeta_credito" },
            7,
            CancellationToken.None));

        Assert.Contains("pasarela de pago configurada", error.Message);
        Assert.Empty(await db.PagosFacturas.ToListAsync());
    }

    [Fact]
    public async Task RegistrarPagoAsync_RejectsAmountAboveOutstandingBalance()
    {
        await using var db = CreateDb();
        var invoice = await CreateInvoiceAsync(db, "FAC-2026-00022", 100000m, 80000m, "parcial");

        var service = new FacturacionService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegistrarPagoAsync(
            invoice.IdFactura,
            new RegistrarPagoRequest { MontoPagado = 25000m, MetodoPago = "efectivo" },
            7,
            CancellationToken.None));

        Assert.Empty(await db.PagosFacturas.ToListAsync());
    }

    [Fact]
    public async Task ListarAndObtenerAsync_ReturnPersistedBalanceAndPaymentHistory()
    {
        await using var db = CreateDb();
        var invoice = await CreateInvoiceAsync(db, "FAC-2026-00023", 150000m, 0m, "pendiente");
        db.DetallesFactura.Add(new DetalleFactura
        {
            IdFactura = invoice.IdFactura,
            Descripcion = "Control de ortodoncia",
            Cantidad = 1,
            PrecioUnitario = 150000m,
            SubtotalLinea = 150000m
        });
        await db.SaveChangesAsync();
        var service = new FacturacionService(db);

        await service.RegistrarPagoAsync(
            invoice.IdFactura,
            new RegistrarPagoRequest { MontoPagado = 50000m, MetodoPago = "efectivo" },
            7,
            CancellationToken.None);

        var listed = Assert.Single(await service.ListarAsync(null, null, null, null, null, null, CancellationToken.None));
        var details = await service.ObtenerAsync(invoice.IdFactura, CancellationToken.None);

        Assert.Equal(150000m, listed.Total);
        Assert.Equal("Control de ortodoncia", listed.Servicios);
        Assert.Equal(50000m, listed.MontoPagado);
        Assert.Equal(100000m, listed.Pendiente);
        Assert.Equal("parcial", listed.Estado);
        Assert.NotNull(details);
        var payment = Assert.Single(details.Pagos);
        Assert.Equal(50000m, payment.Monto);
        Assert.Equal("efectivo", payment.MetodoPago);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Factura> CreateInvoiceAsync(
        AppDbContext db,
        string invoiceNumber,
        decimal total,
        decimal amountPaid,
        string status)
    {
        var patient = new Paciente
        {
            TipoDocumento = "CC",
            Documento = Guid.NewGuid().ToString("N")[..10],
            Nombres = "Paciente",
            Apellidos = "Prueba",
            FechaNacimiento = DateTime.Today,
            Estado = "activo"
        };
        db.Pacientes.Add(patient);
        await db.SaveChangesAsync();

        var invoice = new Factura
        {
            NumeroFactura = invoiceNumber,
            IdPaciente = patient.IdPaciente,
            Total = total,
            MontoPagado = amountPaid,
            Estado = status
        };
        db.Facturas.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }
}