using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.GestionDeCitas;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

/// <summary>
/// Pruebas del <see cref="CitasEnCursoBackgroundService"/>. Para no tocar producción se invoca
/// por reflexión su método privado <c>IniciarCitasProgramadasAsync</c> (nunca ExecuteAsync, que
/// entra en bucle infinito) construyendo antes un <see cref="IServiceScopeFactory"/> real a partir
/// de un <see cref="ServiceProvider"/> con AppDbContext (InMemory) y el CitaService real.
/// Nota: seed, ServiceProvider y verificación deben usar EXACTAMENTE la misma configuración de
/// opciones de EF para compartir el mismo store InMemory.
/// </summary>
public sealed class CitasEnCursoBackgroundServiceTests
{
    private static string NuevaDb() => Guid.NewGuid().ToString();

    private static ServiceProvider CrearProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options
            .UseInMemoryDatabase(dbName));

        // ICitaService real: resuelve el AppDbContext scoped del mismo scope que el servicio en segundo plano.
        services.AddScoped<ICitaService>(sp => new CitaService(
            sp.GetRequiredService<AppDbContext>(),
            NullLogger<CitaService>.Instance));

        return services.BuildServiceProvider();
    }

    private static void Sembrar(string dbName, Action<AppDbContext> configurar)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        using var db = new AppDbContext(options);
        configurar(db);
        db.SaveChanges();
    }

    private static AppDbContext LeerDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        return new AppDbContext(options);
    }

    private static async Task InvocarIniciarCitasProgramadasAsync(
        CitasEnCursoBackgroundService servicio, CancellationToken ct)
    {
        var metodo = typeof(CitasEnCursoBackgroundService).GetMethod(
            "IniciarCitasProgramadasAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(metodo);

        var tarea = (Task)metodo!.Invoke(servicio, new object[] { ct })!;
        await tarea;
    }

    /// <summary>Fecha de cita dentro de la jornada de hoy y ya vencida (maneja el borde de medianoche).</summary>
    private static DateTime FechaHoyYaVencida(DateTime ahora)
    {
        var inicioDia = ahora.Date;
        var cincoMinutosAntes = ahora.AddMinutes(-5);
        return cincoMinutosAntes < inicioDia ? inicioDia : cincoMinutosAntes;
    }

    private static void SembrarCatalogosBase(AppDbContext db)
    {
        db.Pacientes.Add(new Paciente { IdPaciente = 1, Nombres = "Juan", Apellidos = "Pérez", Estado = "activo" });
        db.Profesionales.Add(new Profesional { IdProfesional = 1, Nombres = "Ana", Apellidos = "Gómez", RegistroMedico = "RM-1", Estado = "activo" });
        db.Consultorios.Add(new Consultorio { IdConsultorio = 1, Nombre = "Consultorio 1", Estado = "disponible" });
    }

    [Fact]
    public async Task IniciarCitasProgramadasAsync_CitaDeHoyYaVencida_LaPasaAEnProceso()
    {
        string dbName = NuevaDb();
        DateTime ahora = ColombiaClock.Now;

        Sembrar(dbName, db =>
        {
            SembrarCatalogosBase(db);
            db.EstadosCita.Add(new EstadoCita { IdEstado = 1, NombreEstado = "Programada" });
            db.EstadosCita.Add(new EstadoCita { IdEstado = 5, NombreEstado = "En consulta" });
            db.Citas.Add(new Cita
            {
                IdCita = 10,
                IdPaciente = 1,
                IdProfesional = 1,
                IdConsultorio = 1,
                IdEstado = 1,
                Estado = "Programada",
                FechaHora = FechaHoyYaVencida(ahora),
                DuracionMinutos = 60
            });
        });

        using var provider = CrearProvider(dbName);
        var servicio = new CitasEnCursoBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CitasEnCursoBackgroundService>.Instance);

        await InvocarIniciarCitasProgramadasAsync(servicio, CancellationToken.None);

        using var dbVerificacion = LeerDb(dbName);
        var cita = await dbVerificacion.Citas.AsNoTracking().FirstAsync(c => c.IdCita == 10);
        Assert.Equal("En consulta", cita.Estado);
        Assert.Equal(5, cita.IdEstado);

        var historial = await dbVerificacion.CitasHistorialEstado.AsNoTracking()
            .Where(h => h.IdCita == 10).ToListAsync();
        Assert.Contains(historial, h => h.EstadoTexto == "En consulta");

        var asistencias = await dbVerificacion.AsistenciasProcedimiento.AsNoTracking()
            .Where(a => a.IdCita == 10).ToListAsync();
        Assert.Single(asistencias);
    }

    [Fact]
    public async Task IniciarCitasProgramadasAsync_CitaFuturaDeHoy_NoLaInicia()
    {
        string dbName = NuevaDb();
        DateTime ahora = ColombiaClock.Now;

        Sembrar(dbName, db =>
        {
            SembrarCatalogosBase(db);
            db.EstadosCita.Add(new EstadoCita { IdEstado = 1, NombreEstado = "Programada" });
            db.EstadosCita.Add(new EstadoCita { IdEstado = 5, NombreEstado = "En consulta" });
            db.Citas.Add(new Cita
            {
                IdCita = 20,
                IdPaciente = 1,
                IdProfesional = 1,
                IdConsultorio = 1,
                IdEstado = 1,
                Estado = "Programada",
                FechaHora = ahora.AddMinutes(30),
                DuracionMinutos = 60
            });
        });

        using var provider = CrearProvider(dbName);
        var servicio = new CitasEnCursoBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CitasEnCursoBackgroundService>.Instance);

        await InvocarIniciarCitasProgramadasAsync(servicio, CancellationToken.None);

        using var dbVerificacion = LeerDb(dbName);
        var cita = await dbVerificacion.Citas.AsNoTracking().FirstAsync(c => c.IdCita == 20);
        Assert.Equal("Programada", cita.Estado);
        Assert.Equal(1, cita.IdEstado);
    }

    [Fact]
    public async Task IniciarCitasProgramadasAsync_CitaEnEstadoNoIniciable_NoLaToca()
    {
        string dbName = NuevaDb();
        DateTime ahora = ColombiaClock.Now;

        Sembrar(dbName, db =>
        {
            SembrarCatalogosBase(db);
            db.EstadosCita.Add(new EstadoCita { IdEstado = 4, NombreEstado = "Atendida" });
            db.Citas.Add(new Cita
            {
                IdCita = 30,
                IdPaciente = 1,
                IdProfesional = 1,
                IdConsultorio = 1,
                IdEstado = 4,
                Estado = "Atendida",
                FechaHora = FechaHoyYaVencida(ahora),
                DuracionMinutos = 60
            });
        });

        using var provider = CrearProvider(dbName);
        var servicio = new CitasEnCursoBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CitasEnCursoBackgroundService>.Instance);

        await InvocarIniciarCitasProgramadasAsync(servicio, CancellationToken.None);

        using var dbVerificacion = LeerDb(dbName);
        var cita = await dbVerificacion.Citas.AsNoTracking().FirstAsync(c => c.IdCita == 30);
        Assert.Equal("Atendida", cita.Estado);
        Assert.Equal(4, cita.IdEstado);
    }
}
