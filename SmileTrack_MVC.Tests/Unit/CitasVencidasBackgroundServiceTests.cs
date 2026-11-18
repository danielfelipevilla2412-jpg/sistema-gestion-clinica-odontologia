using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.GestionDeCitas;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

/// <summary>
/// Pruebas del <see cref="CitasVencidasBackgroundService"/>. Se invoca por reflexión su método
/// privado <c>CerrarCitasVencidasAsync(corte, ct)</c> evitando ExecuteAsync (bucle infinito).
/// El servicio solo resuelve AppDbContext desde el scope.
///
/// LIMITACIÓN CONOCIDA: el cierre de citas vencidas se materializa con
/// <c>ExecuteUpdateAsync</c>, y el proveedor InMemory NO soporta ExecuteUpdate/ExecuteUpdateAsync
/// (lanza InvalidOperationException). El servicio captura esa excepción y la registra, por lo que
/// la transición real de estado ("No asistio") NO puede observarse con InMemory. Por eso las
/// pruebas se centran en: (a) que las citas que NO deben cerrarse no se modifican y
/// (b) que el servicio no propaga el fallo del proveedor (contrato del try/catch del bucle).
/// </summary>
public sealed class CitasVencidasBackgroundServiceTests
{
    private sealed class Grabador<T> : ILogger<T>
    {
        public readonly List<string> Mensajes = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Mensajes.Add(logLevel + ": " + formatter(state, exception));
    }

    private static string NuevaDb() => Guid.NewGuid().ToString();

    private static ServiceProvider CrearProvider(string dbName)
    {
        var services = new ServiceCollection();
        // Sin ConfigureWarnings: mantener la MISMA configuración en seed, provider y verificación
        // para que todos compartan el mismo store InMemory.
        services.AddDbContext<AppDbContext>(options => options
            .UseInMemoryDatabase(dbName));
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

    private static async Task InvocarCerrarCitasVencidasAsync(
        CitasVencidasBackgroundService servicio, DateTime corte, CancellationToken ct)
    {
        var metodo = typeof(CitasVencidasBackgroundService).GetMethod(
            "CerrarCitasVencidasAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(metodo);

        var tarea = (Task)metodo!.Invoke(servicio, new object[] { corte, ct })!;
        await tarea;
    }

    private static void SembrarBase(AppDbContext db)
    {
        db.Pacientes.Add(new Paciente { IdPaciente = 1, Nombres = "Juan", Apellidos = "Pérez", Estado = "activo" });
        db.Profesionales.Add(new Profesional { IdProfesional = 1, Nombres = "Ana", Apellidos = "Gómez", RegistroMedico = "RM-1", Estado = "activo" });
        db.Consultorios.Add(new Consultorio { IdConsultorio = 1, Nombre = "Consultorio 1", Estado = "disponible" });
        db.EstadosCita.Add(new EstadoCita { IdEstado = 1, NombreEstado = "Programada" });
        db.EstadosCita.Add(new EstadoCita { IdEstado = 9, NombreEstado = "No asistio" });
    }

    [Fact]
    public async Task CerrarCitasVencidasAsync_CitaDeLaJornadaEnCurso_NoLaCierra()
    {
        string dbName = NuevaDb();

        Sembrar(dbName, db =>
        {
            SembrarBase(db);
            db.Citas.Add(new Cita
            {
                IdCita = 200,
                IdPaciente = 1,
                IdProfesional = 1,
                IdConsultorio = 1,
                IdEstado = 1,
                Estado = "Programada",
                FechaHora = ColombiaClock.Today.AddHours(23),
                DuracionMinutos = 60
            });
        });

        using var provider = CrearProvider(dbName);
        var grabador = new Grabador<CitasVencidasBackgroundService>();
        var servicio = new CitasVencidasBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(), grabador);

        // corte = inicio del día: la cita de hoy (>= corte) no debe tocarse.
        await InvocarCerrarCitasVencidasAsync(servicio, ColombiaClock.Today, CancellationToken.None);

        using var dbVerificacion = LeerDb(dbName);
        var cita = await dbVerificacion.Citas.AsNoTracking().FirstAsync(c => c.IdCita == 200);
        Assert.Equal("Programada", cita.Estado);
        Assert.Equal(1, cita.IdEstado);
        Assert.DoesNotContain(grabador.Mensajes, m => m.Contains("Cierre automático de citas vencidas"));
    }

    [Fact]
    public async Task CerrarCitasVencidasAsync_CitaYaFinalizada_NoLaModifica()
    {
        string dbName = NuevaDb();

        Sembrar(dbName, db =>
        {
            SembrarBase(db);
            db.EstadosCita.Add(new EstadoCita { IdEstado = 4, NombreEstado = "Atendida" });
            db.Citas.Add(new Cita
            {
                IdCita = 300,
                IdPaciente = 1,
                IdProfesional = 1,
                IdConsultorio = 1,
                IdEstado = 4,
                Estado = "Atendida",
                FechaHora = ColombiaClock.Today.AddDays(-2).AddHours(9),
                DuracionMinutos = 60
            });
        });

        using var provider = CrearProvider(dbName);
        var servicio = new CitasVencidasBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CitasVencidasBackgroundService>.Instance);

        await InvocarCerrarCitasVencidasAsync(servicio, ColombiaClock.Today, CancellationToken.None);

        using var dbVerificacion = LeerDb(dbName);
        var cita = await dbVerificacion.Citas.AsNoTracking().FirstAsync(c => c.IdCita == 300);
        Assert.Equal("Atendida", cita.Estado);
        Assert.Equal(4, cita.IdEstado);
    }

    [Fact]
    public async Task CerrarCitasVencidasAsync_ConCitaVencidaPendiente_NoPropagaElFalloDelProveedor()
    {
        string dbName = NuevaDb();

        Sembrar(dbName, db =>
        {
            SembrarBase(db);
            db.Citas.Add(new Cita
            {
                IdCita = 100,
                IdPaciente = 1,
                IdProfesional = 1,
                IdConsultorio = 1,
                IdEstado = 1,
                Estado = "Programada",
                FechaHora = ColombiaClock.Today.AddDays(-1).AddHours(10),
                DuracionMinutos = 60
            });
        });

        using var provider = CrearProvider(dbName);
        var grabador = new Grabador<CitasVencidasBackgroundService>();
        var servicio = new CitasVencidasBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(), grabador);

        // El proveedor InMemory no soporta ExecuteUpdateAsync; el servicio debe capturarlo y loguearlo.
        await InvocarCerrarCitasVencidasAsync(servicio, ColombiaClock.Today, CancellationToken.None);

        Assert.Contains(grabador.Mensajes,
            m => m.StartsWith("Error") && m.Contains("No fue posible cerrar las citas vencidas"));
    }
}
