using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ReportesApiServiceTests
{
    [Fact]
    public async Task ObtenerResumenAsync_CalculaKpisEIngresosDelRango()
    {
        await using var context = CrearContext();
        context.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 50000m, Estado = "activo" });
        context.Pacientes.AddRange(
            new Paciente { IdPaciente = 1, Nombres = "A", Apellidos = "A", FechaNacimiento = new DateTime(1990, 1, 1), FechaRegistro = new DateTime(2026, 1, 10), Estado = "activo" },
            new Paciente { IdPaciente = 2, Nombres = "B", Apellidos = "B", FechaNacimiento = new DateTime(1991, 1, 1), FechaRegistro = new DateTime(2025, 1, 10), Estado = "activo" });
        context.Citas.AddRange(
            new Cita { IdCita = 1, IdPaciente = 1, IdServicio = 1, FechaHora = new DateTime(2026, 1, 5, 10, 0, 0), Estado = "Atendida" },
            new Cita { IdCita = 2, IdPaciente = 1, IdServicio = 1, FechaHora = new DateTime(2026, 1, 6, 10, 0, 0), Estado = "Cancelada" },
            new Cita { IdCita = 3, IdPaciente = 1, IdServicio = 1, FechaHora = new DateTime(2026, 1, 7, 10, 0, 0), Estado = "No asistio" });
        context.Facturas.AddRange(
            new Factura { IdFactura = 1, IdPaciente = 1, FechaFactura = new DateTime(2026, 1, 5), Estado = "pagada", Total = 50000m },
            new Factura { IdFactura = 2, IdPaciente = 1, FechaFactura = new DateTime(2026, 1, 6), Estado = "pendiente", Total = 30000m });
        await context.SaveChangesAsync();

        var resultado = await CrearServicio(context)
            .ObtenerResumenAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.Equal(3, resultado.TotalCitas);
        Assert.Equal(1, resultado.CitasAtendidas);
        Assert.Equal(1, resultado.CitasCanceladas);
        Assert.Equal(1, resultado.CitasNoAsistieron);
        Assert.Equal(50000m, resultado.IngresosPagados);
        Assert.Equal(30000m, resultado.IngresosPendientes);
        Assert.Equal(2, resultado.TotalPacientes);
        Assert.Equal(1, resultado.PacientesNuevos);
        Assert.Equal(Math.Round(100d / 3, 2), resultado.TasaAsistencia);
        Assert.Equal(3, resultado.CitasPorDia.Count);
        var servicio = Assert.Single(resultado.Servicios);
        Assert.Equal(3, servicio.CantidadCitas);
        Assert.Equal(50000m, servicio.PrecioReferencial);
    }

    [Fact]
    public async Task ObtenerResumenAsync_RangoInvalido_LanzaArgumentException()
    {
        await using var context = CrearContext();

        var service = CrearServicio(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ObtenerResumenAsync(new DateTime(2026, 1, 10), new DateTime(2026, 1, 5)));
    }

    [Fact]
    public async Task ObtenerResumenAsync_SinCitas_RetornaTasaCeroYSeriesVacias()
    {
        await using var context = CrearContext();

        var resultado = await CrearServicio(context)
            .ObtenerResumenAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        Assert.Equal(0, resultado.TotalCitas);
        Assert.Equal(0, resultado.TasaAsistencia);
        Assert.Equal(0m, resultado.IngresosPagados);
        Assert.Equal(0m, resultado.IngresosPendientes);
        Assert.Empty(resultado.CitasPorDia);
        Assert.Empty(resultado.Servicios);
    }

    [Fact]
    public async Task ObtenerBitacoraAsync_FiltraPorUsuarioAccionYFecha()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(new Usuario
        {
            IdUsuario = 1,
            Nombre = "Ana",
            Apellidos = "Zapata",
            Correo = "ana@u.com",
            Contrasena = "h",
            IdRol = 1,
            Estado = "activo"
        });
        context.Auditorias.AddRange(
            new Auditoria { IdAuditoria = 1, IdUsuario = 1, TablaAfectada = "Usuario", Accion = "INSERT", Fecha = new DateTime(2026, 1, 10), Descripcion = "inserción" },
            new Auditoria { IdAuditoria = 2, IdUsuario = 1, TablaAfectada = "Usuario", Accion = "UPDATE", Fecha = new DateTime(2026, 1, 11), Descripcion = "actualización" },
            new Auditoria { IdAuditoria = 3, IdUsuario = null, TablaAfectada = "Sistema", Accion = "INSERT", Fecha = new DateTime(2026, 1, 12) });
        await context.SaveChangesAsync();

        var resultado = await CrearServicio(context)
            .ObtenerBitacoraAsync("Ana", "INSERT", new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), 1, 20);

        Assert.True(resultado.Success);
        Assert.Equal(1, resultado.TotalCount);
        Assert.Equal(1, resultado.Items.Single().IdAuditoria);
        Assert.Equal("Ana Zapata", resultado.Items.Single().Usuario);
    }

    [Fact]
    public async Task ObtenerBitacoraAsync_UsuarioNulo_SeMuestraComoSistema()
    {
        await using var context = CrearContext();
        context.Auditorias.Add(new Auditoria
        {
            IdAuditoria = 1,
            IdUsuario = null,
            TablaAfectada = "Sistema",
            Accion = "INSERT",
            Fecha = new DateTime(2026, 1, 12)
        });
        await context.SaveChangesAsync();

        var resultado = await CrearServicio(context)
            .ObtenerBitacoraAsync(null, null, null, null, 1, 20);

        Assert.True(resultado.Success);
        Assert.Equal("Sistema", resultado.Items.Single().Usuario);
    }

    [Fact]
    public async Task ObtenerBitacoraAsync_AplicaPaginacionYOrdenDescendente()
    {
        await using var context = CrearContext();
        context.Auditorias.AddRange(
            new Auditoria { IdAuditoria = 1, TablaAfectada = "A", Accion = "INSERT", Fecha = new DateTime(2026, 1, 1) },
            new Auditoria { IdAuditoria = 2, TablaAfectada = "B", Accion = "INSERT", Fecha = new DateTime(2026, 1, 3) },
            new Auditoria { IdAuditoria = 3, TablaAfectada = "C", Accion = "INSERT", Fecha = new DateTime(2026, 1, 2) });
        await context.SaveChangesAsync();

        var resultado = await CrearServicio(context)
            .ObtenerBitacoraAsync(null, null, null, null, 1, 2);

        Assert.Equal(3, resultado.TotalCount);
        Assert.Equal(2, resultado.Items.Count);
        Assert.Equal(2, resultado.Items[0].IdAuditoria);
        Assert.Equal(3, resultado.Items[1].IdAuditoria);
    }

    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ReportesApiService CrearServicio(AppDbContext context)
        => new(context, NullLogger<ReportesApiService>.Instance);
}
