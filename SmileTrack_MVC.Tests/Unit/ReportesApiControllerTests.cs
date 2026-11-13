using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Reportes;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ReportesApiControllerTests
{
    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ReportesApiController CrearControlador(AppDbContext db)
    {
        var service = new ReportesApiService(db, NullLogger<ReportesApiService>.Instance);
        return new ReportesApiController(service);
    }

    private static int Contar(IEnumerable datos)
    {
        int total = 0;
        foreach (var _ in datos) total++;
        return total;
    }

    [Fact]
    public async Task GetResumen_RangoValido_RetornaOkConDatos()
    {
        await using var db = CrearContext();
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 50000m, Estado = "activo" });
        db.Pacientes.Add(new Paciente { IdPaciente = 1, TipoDocumento = "CC", Documento = "1001", Nombres = "A", Apellidos = "A", FechaNacimiento = new DateTime(1990, 1, 1), FechaRegistro = new DateTime(2026, 1, 10), Estado = "activo" });
        db.Citas.AddRange(
            new Cita { IdCita = 1, IdPaciente = 1, IdServicio = 1, FechaHora = new DateTime(2026, 1, 5, 10, 0, 0), Estado = "Atendida" },
            new Cita { IdCita = 2, IdPaciente = 1, IdServicio = 1, FechaHora = new DateTime(2026, 1, 6, 10, 0, 0), Estado = "Cancelada" });
        db.Facturas.Add(new Factura { IdFactura = 1, NumeroFactura = "FAC-2026-00001", IdPaciente = 1, FechaFactura = new DateTime(2026, 1, 5), Estado = "pagada", Total = 50000m });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.GetResumen(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        var data = Assert.IsType<ReporteResumenApiDto>(ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value));
        Assert.Equal(2, data.TotalCitas);
        Assert.Equal(1, data.CitasAtendidas);
        Assert.Equal(1, data.CitasCanceladas);
        Assert.Equal(50000m, data.IngresosPagados);
    }

    [Fact]
    public async Task GetResumen_RangoInvalido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.GetResumen(new DateTime(2026, 1, 10), new DateTime(2026, 1, 5), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetBitacora_ConDatos_RetornaOkConPaginacion()
    {
        await using var db = CrearContext();
        db.Usuarios.Add(new Usuario { IdUsuario = 1, Nombre = "Ana", Apellidos = "Zapata", Correo = "ana@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" });
        db.Auditorias.AddRange(
            new Auditoria { IdAuditoria = 1, IdUsuario = 1, TablaAfectada = "Usuario", Accion = "INSERT", Fecha = new DateTime(2026, 1, 10), Descripcion = "inserción" },
            new Auditoria { IdAuditoria = 2, IdUsuario = 1, TablaAfectada = "Usuario", Accion = "UPDATE", Fecha = new DateTime(2026, 1, 11), Descripcion = "actualización" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.GetBitacora(null, null, null, null, 1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        var datos = Assert.IsAssignableFrom<IEnumerable>(ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value));
        Assert.Equal(2, Contar(datos));
        var pagination = ok.Value!.GetType().GetProperty("pagination")!.GetValue(ok.Value)!;
        Assert.Equal(2, (int)pagination.GetType().GetProperty("TotalCount")!.GetValue(pagination)!);
    }

    [Fact]
    public async Task GetBitacora_SinDatos_RetornaOkConListaVacia()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.GetBitacora(null, null, null, null, 1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var datos = Assert.IsAssignableFrom<IEnumerable>(ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value));
        Assert.Equal(0, Contar(datos));
        var pagination = ok.Value!.GetType().GetProperty("pagination")!.GetValue(ok.Value)!;
        Assert.Equal(0, (int)pagination.GetType().GetProperty("TotalCount")!.GetValue(pagination)!);
    }
}
