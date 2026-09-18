using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class FuncionalidadesAvanzadasTests
{
    private static AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new AppDbContext(options);

        // Seed data
        db.Pacientes.Add(new Paciente { IdPaciente = 1, Nombres = "Carlos", Apellidos = "Mendoza", Estado = "activo" });
        db.Pacientes.Add(new Paciente { IdPaciente = 2, Nombres = "María", Apellidos = "López", Estado = "activo" });

        db.Profesionales.Add(new Profesional { IdProfesional = 1, Nombres = "Dr. Pedro", Apellidos = "Ramírez", Estado = "activo" });
        db.Profesionales.Add(new Profesional { IdProfesional = 2, Nombres = "Dra. Sofía", Apellidos = "Vargas", Estado = "activo" });

        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Limpieza Odontológica", Precio = 100000m, DuracionMinutos = 30, Estado = "activo" });
        db.Consultorios.Add(new Consultorio { IdConsultorio = 1, Nombre = "Consultorio 101", Estado = "disponible" });

        db.EstadosCita.Add(new EstadoCita { IdEstado = 1, NombreEstado = "Programada" });
        db.EstadosCita.Add(new EstadoCita { IdEstado = 2, NombreEstado = "Confirmada" });
        db.EstadosCita.Add(new EstadoCita { IdEstado = 3, NombreEstado = "Cancelada" });
        db.EstadosCita.Add(new EstadoCita { IdEstado = 4, NombreEstado = "Atendida" });

        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task RegistrarAusenciaConReasignacionAsync_ReasignaCitaAProfesionalDisponible()
    {
        var db = CreateInMemoryDb($"db_reasignar_{Guid.NewGuid()}");
        var profService = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var citaService = new CitaService(db, NullLogger<CitaService>.Instance);

        var fechaCita = DateTime.Today.AddDays(3).AddHours(10);

        // Cita con Dr. Pedro (ID=1)
        var citaReq = new CitaApiRequest
        {
            IdPaciente = 1,
            IdProfesional = 1,
            IdServicio = 1,
            IdConsultorio = 1,
            FechaHora = fechaCita,
            Estado = "Programada"
        };
        var citaCreada = await citaService.CrearAsync(citaReq);

        // Se registra ausencia para Dr. Pedro (ID=1)
        var inicioAusencia = fechaCita.AddHours(-1);
        var finAusencia = fechaCita.AddHours(2);

        var resultado = await profService.RegistrarAusenciaConReasignacionAsync(1, inicioAusencia, finAusencia, "Incapacidad médica");

        Assert.Equal(1, resultado.TotalCitasAfectadas);
        Assert.Equal(1, resultado.TotalReasignadas);
        Assert.Equal(0, resultado.TotalCanceladas);

        // La cita ahora pertenece a Dra. Sofía (ID=2)
        var citaActualizada = await db.Citas.FirstOrDefaultAsync(c => c.IdCita == citaCreada.IdCita);
        Assert.NotNull(citaActualizada);
        Assert.Equal(2, citaActualizada.IdProfesional);

        // Notificación generada al paciente
        var notif = await db.Notificaciones.FirstOrDefaultAsync(n => n.IdPaciente == 1 && n.Tipo == "cambio_profesional");
        Assert.NotNull(notif);
        Assert.Contains("Sofía Vargas", notif.Contenido);
    }

    [Fact]
    public async Task CalcularComisionesAsync_CalculaHonorariosCorrectamente()
    {
        var db = CreateInMemoryDb($"db_comisiones_{Guid.NewGuid()}");
        var profService = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);

        // Crear 3 citas atendidas para Dr. Pedro (100,000 COP c/u = 300,000 COP)
        db.Citas.Add(new Cita { IdPaciente = 1, IdProfesional = 1, IdServicio = 1, FechaHora = DateTime.Today.AddDays(-2), Estado = "Atendida" });
        db.Citas.Add(new Cita { IdPaciente = 2, IdProfesional = 1, IdServicio = 1, FechaHora = DateTime.Today.AddDays(-1), Estado = "Atendida" });
        db.Citas.Add(new Cita { IdPaciente = 1, IdProfesional = 1, IdServicio = 1, FechaHora = DateTime.Today.AddDays(-1).AddHours(2), Estado = "Atendida" });
        await db.SaveChangesAsync();

        var reporte = await profService.CalcularComisionesAsync(1, DateTime.Today.AddDays(-10), DateTime.Today, porcentajeComision: 40);

        Assert.NotNull(reporte);
        Assert.Equal(3, reporte.TotalCitasAtendidas);
        Assert.Equal(300000m, reporte.MontoTotalFacturado);
        Assert.Equal(40m, reporte.PorcentajeComision);
        Assert.Equal(120000m, reporte.MontoTotalHonorarios); // 40% de 300,000 = 120,000
    }

    [Fact]
    public async Task CancelarAsync_NotificaAPacientesEnListaDeEspera()
    {
        var db = CreateInMemoryDb($"db_espera_{Guid.NewGuid()}");
        var citaService = new CitaService(db, NullLogger<CitaService>.Instance);

        var fechaCita = DateTime.Today.AddDays(2).AddHours(14);

        // Paciente 2 está en lista de espera para la misma fecha/hora y servicio
        db.ListaEsperaCitas.Add(new ListaEsperaCita
        {
            IdPaciente = 2,
            IdServicio = 1,
            FechaDeseadaInicio = fechaCita.AddHours(-1),
            FechaDeseadaFin = fechaCita.AddHours(1),
            Estado = "pendiente"
        });
        await db.SaveChangesAsync();

        // Paciente 1 crea una cita
        var citaReq = new CitaApiRequest
        {
            IdPaciente = 1,
            IdProfesional = 1,
            IdServicio = 1,
            IdConsultorio = 1,
            FechaHora = fechaCita,
            Estado = "Programada"
        };
        var cita = await citaService.CrearAsync(citaReq);

        // Paciente 1 cancela su cita
        bool cancelado = await citaService.CancelarAsync(cita.IdCita);

        Assert.True(cancelado);

        // Paciente 2 en lista de espera debe haber sido notificado
        var espera = await db.ListaEsperaCitas.FirstOrDefaultAsync(l => l.IdPaciente == 2);
        Assert.NotNull(espera);
        Assert.Equal("notificado", espera.Estado);

        var notifEspera = await db.Notificaciones.FirstOrDefaultAsync(n => n.IdPaciente == 2 && n.Tipo == "lista_espera_liberada");
        Assert.NotNull(notifEspera);
        Assert.Contains("Se ha liberado un turno", notifEspera.Contenido);
    }
}
