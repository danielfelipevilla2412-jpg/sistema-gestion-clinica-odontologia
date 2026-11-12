using System;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Controllers;

public class GestionPacientesControllerTests
{
    [Fact]
    public async Task BuscarPacientes_DevuelvePacienteActivoYContratoCamelCase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var paciente = new Paciente
        {
            Nombres = "Andrea",
            Apellidos = "Gómez",
            TipoDocumento = "CC",
            Documento = "10345678",
            FechaNacimiento = new DateTime(1990, 1, 1),
            Estado = "ACTIVO",
            Alergias = "Penicilina"
        };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();

        var auditoria = new AuditoriaHistoriaClinicaService(db);
        var controller = new GestionPacientesController(
            db,
            NullLogger<GestionPacientesController>.Instance,
            new PacienteFusionService(db, auditoria));

        var result = Assert.IsType<JsonResult>(await controller.BuscarPacientes(
            search: null,
            estado: "activo",
            alergias: null,
            cita: null,
            historial: null,
            editar: null,
            page: 1,
            pageSize: 20));

        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(
            result.Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root = payload.RootElement;
        var item = root.GetProperty("items")[0];

        Assert.Equal(1, root.GetProperty("total").GetInt32());
        Assert.Equal(paciente.IdPaciente, item.GetProperty("id").GetInt32());
        Assert.Equal("Andrea Gómez", item.GetProperty("name").GetString());
        Assert.Equal("CC 10345678", item.GetProperty("doc").GetString());
        Assert.Equal("ACTIVO", item.GetProperty("estado").GetString());
        Assert.Equal("Penicilina", item.GetProperty("allergies")[0].GetString());
    }
}