using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.CentroDeAyuda;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.CentroDeAyuda;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class CentroDeAyudaServiceTests
{
    [Fact]
    public async Task ObtenerTicketsAsync_FiltraPorEstado()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(UsuarioBase(1, "Uno", "User"));
        context.TicketsSoporte.AddRange(
            Ticket(1, "ST-1", "abierto", new DateTime(2026, 1, 1)),
            Ticket(2, "ST-2", "cerrado", new DateTime(2026, 1, 2)));
        await context.SaveChangesAsync();

        var (items, total) = await CrearServicio(context)
            .ObtenerTicketsAsync(1, 20, null, "abierto", null, default);

        Assert.Equal(1, total);
        var item = Assert.Single(items);
        Assert.Equal(1, item.IdTicket);
        Assert.Equal("abierto", item.Estado);
    }

    [Fact]
    public async Task ObtenerTicketsAsync_OrdenaDescendenteYPagina()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(UsuarioBase(1, "Uno", "User"));
        context.TicketsSoporte.AddRange(
            Ticket(1, "ST-1", "abierto", new DateTime(2026, 1, 1)),
            Ticket(2, "ST-2", "abierto", new DateTime(2026, 1, 3)),
            Ticket(3, "ST-3", "abierto", new DateTime(2026, 1, 2)));
        await context.SaveChangesAsync();

        var (items, total) = await CrearServicio(context)
            .ObtenerTicketsAsync(1, 2, null, null, null, default);

        Assert.Equal(3, total);
        Assert.Equal(2, items.Count);
        Assert.Equal(2, items[0].IdTicket);
        Assert.Equal(3, items[1].IdTicket);
    }

    [Fact]
    public async Task ObtenerTicketAsync_TicketInexistente_RetornaNull()
    {
        await using var context = CrearContext();

        var resultado = await CrearServicio(context).ObtenerTicketAsync(999, default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task CrearTicketAsync_CategoriaInvalida_RetornaErrorDeValidacion()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(UsuarioBase(1, "Uno", "User"));
        await context.SaveChangesAsync();

        var (data, error) = await CrearServicio(context).CrearTicketAsync(new CentroAyudaTicketRequest
        {
            Asunto = "Asunto válido",
            Categoria = "inventada",
            ModuloAfectado = "citas",
            Severidad = "baja",
            Descripcion = "Descripción suficientemente larga"
        }, usuarioId: 1, default);

        Assert.Null(data);
        Assert.Contains("Categoría inválida", error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await context.TicketsSoporte.ToListAsync());
    }

    [Fact]
    public async Task CrearTicketAsync_UsuarioInactivo_RetornaError()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(UsuarioBase(1, "Uno", "User", estado: "inactivo"));
        await context.SaveChangesAsync();

        var (data, error) = await CrearServicio(context).CrearTicketAsync(new CentroAyudaTicketRequest
        {
            Asunto = "Asunto válido",
            Categoria = "consulta",
            ModuloAfectado = "citas",
            Severidad = "baja",
            Descripcion = "Descripción suficientemente larga"
        }, usuarioId: 1, default);

        Assert.Null(data);
        Assert.Contains("inactivo", error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await context.TicketsSoporte.ToListAsync());
    }

    [Fact]
    public async Task CrearTicketAsync_DatosValidos_CreaTicketAbiertoConReferencia()
    {
        await using var context = CrearContext();
        context.Usuarios.Add(UsuarioBase(1, "Uno", "User"));
        await context.SaveChangesAsync();

        var (data, error) = await CrearServicio(context).CrearTicketAsync(new CentroAyudaTicketRequest
        {
            Asunto = "No puedo agendar una cita",
            Categoria = "incidente",
            ModuloAfectado = "citas",
            Severidad = "alta",
            Descripcion = "Descripción suficientemente larga del problema"
        }, usuarioId: 1, default);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("abierto", data!.Estado);
        Assert.StartsWith("ST-", data.Referencia);
        Assert.Equal(1, data.IdUsuario);
        Assert.Equal("incidente", data.Categoria);
        Assert.Equal(1, await context.TicketsSoporte.CountAsync());
    }

    [Fact]
    public async Task ActualizarTicketAsync_EstadoInvalido_Retorna422()
    {
        await using var context = CrearContext();
        context.TicketsSoporte.Add(Ticket(1, "ST-1", "abierto", new DateTime(2026, 1, 1)));
        await context.SaveChangesAsync();

        var (data, error, status) = await CrearServicio(context).ActualizarTicketAsync(1, new CentroAyudaTicketUpdateRequest
        {
            Asunto = "Asunto actualizado",
            Categoria = "consulta",
            ModuloAfectado = "citas",
            Severidad = "baja",
            Descripcion = "Descripción suficientemente larga",
            Estado = "estado_inexistente"
        }, usuarioId: 1, default);

        Assert.Null(data);
        Assert.NotNull(error);
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task ActualizarTicketAsync_TicketInexistente_Retorna404()
    {
        await using var context = CrearContext();

        var (data, error, status) = await CrearServicio(context).ActualizarTicketAsync(999, new CentroAyudaTicketUpdateRequest
        {
            Asunto = "Asunto actualizado",
            Categoria = "consulta",
            ModuloAfectado = "citas",
            Severidad = "baja",
            Descripcion = "Descripción suficientemente larga",
            Estado = "en_proceso"
        }, usuarioId: 1, default);

        Assert.Null(data);
        Assert.Contains("no encontrado", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ActualizarTicketAsync_ConRespuesta_AsignaAtendidoPorYFechaRespuesta()
    {
        await using var context = CrearContext();
        context.Usuarios.AddRange(UsuarioBase(1, "Uno", "User"), UsuarioBase(7, "Siete", "Atiende"));
        context.TicketsSoporte.Add(Ticket(1, "ST-1", "abierto", new DateTime(2026, 1, 1)));
        await context.SaveChangesAsync();

        var (data, error, status) = await CrearServicio(context).ActualizarTicketAsync(1, new CentroAyudaTicketUpdateRequest
        {
            Asunto = "Asunto actualizado",
            Categoria = "consulta",
            ModuloAfectado = "citas",
            Severidad = "media",
            Descripcion = "Descripción suficientemente larga",
            Estado = "en_proceso",
            Respuesta = "Estamos revisando el caso"
        }, usuarioId: 7, default);

        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.NotNull(data);
        Assert.Equal("en_proceso", data!.Estado);
        Assert.Equal("Estamos revisando el caso", data.Respuesta);
        Assert.Equal(7, data.AtendidoPor);
        Assert.NotNull(data.FechaRespuesta);
    }

    [Fact]
    public async Task CambiarEstadoAsync_EstadoInvalido_Retorna422()
    {
        await using var context = CrearContext();
        context.TicketsSoporte.Add(Ticket(1, "ST-1", "abierto", new DateTime(2026, 1, 1)));
        await context.SaveChangesAsync();

        var (data, error, status) = await CrearServicio(context).CambiarEstadoAsync(1, "invalido", usuarioId: 1, default);

        Assert.Null(data);
        Assert.Contains("Estado inválido", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task CambiarEstadoAsync_TicketInexistente_Retorna404()
    {
        await using var context = CrearContext();

        var (data, error, status) = await CrearServicio(context).CambiarEstadoAsync(999, "resuelto", usuarioId: 1, default);

        Assert.Null(data);
        Assert.Contains("no encontrado", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CambiarEstadoAsync_Resuelto_MarcaRespuestaYAtendidoPor()
    {
        await using var context = CrearContext();
        context.Usuarios.AddRange(UsuarioBase(1, "Uno", "User"), UsuarioBase(7, "Siete", "Atiende"));
        context.TicketsSoporte.Add(Ticket(1, "ST-1", "abierto", new DateTime(2026, 1, 1)));
        await context.SaveChangesAsync();

        var (data, error, status) = await CrearServicio(context).CambiarEstadoAsync(1, "resuelto", usuarioId: 7, default);

        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.NotNull(data);
        Assert.Equal("resuelto", data!.Estado);
        Assert.Equal(7, data.AtendidoPor);
        Assert.NotNull(data.FechaRespuesta);
    }

    [Fact]
    public async Task EliminarTicketAsync_Inexistente_Retorna404()
    {
        await using var context = CrearContext();

        var (success, error, status) = await CrearServicio(context).EliminarTicketAsync(999, default);

        Assert.False(success);
        Assert.Contains("no encontrado", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task EliminarTicketAsync_Existente_EliminaYRetorna204()
    {
        await using var context = CrearContext();
        context.TicketsSoporte.Add(Ticket(1, "ST-1", "abierto", new DateTime(2026, 1, 1)));
        await context.SaveChangesAsync();

        var (success, error, status) = await CrearServicio(context).EliminarTicketAsync(1, default);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(204, status);
        Assert.Empty(await context.TicketsSoporte.ToListAsync());
    }

    private static TicketSoporte Ticket(int id, string referencia, string estado, DateTime fechaCreacion) => new()
    {
        IdTicket = id,
        Referencia = referencia,
        IdUsuario = 1,
        Asunto = "Asunto de prueba",
        Categoria = "consulta",
        ModuloAfectado = "citas",
        Severidad = "baja",
        Descripcion = "Descripción de prueba",
        Estado = estado,
        FechaCreacion = fechaCreacion
    };

    private static Usuario UsuarioBase(int id, string nombre, string apellidos, string estado = "activo") => new()
    {
        IdUsuario = id,
        Nombre = nombre,
        Apellidos = apellidos,
        Correo = $"{nombre.ToLowerInvariant()}@u.com",
        Contrasena = "hash",
        IdRol = 1,
        Estado = estado
    };

    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CentroDeAyudaService CrearServicio(AppDbContext context)
        => new(context, new FakeWebHostEnvironment(), NullLogger<CentroDeAyudaService>.Instance);

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SmileTrack.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = System.IO.Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
