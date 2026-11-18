using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class PacienteAlergiaSyncTests
{
    [Fact]
    public async Task SynchronizeAsync_UpdatesStructuredAllergiesWithoutDeletingHistory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"patient_allergies_{Guid.NewGuid()}")
            .Options;

        await using var db = new AppDbContext(options);
        var paciente = new Paciente
        {
            TipoDocumento = "CC",
            Documento = "12345678",
            Nombres = "Ana",
            Apellidos = "Pérez",
            FechaNacimiento = new DateTime(2000, 1, 1),
            Estado = "activo"
        };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();

        await PacienteAlergiaSync.SynchronizeAsync(db, paciente, "Penicilina, Látex");
        await db.SaveChangesAsync();
        await PacienteAlergiaSync.SynchronizeAsync(db, paciente, "Látex");
        await db.SaveChangesAsync();

        var allergies = await db.AlergiasPaciente
            .OrderBy(a => a.Sustancia)
            .ToListAsync();

        Assert.Equal(2, allergies.Count);
        Assert.False(allergies.Single(a => a.Sustancia == "Penicilina").Activa);
        Assert.True(allergies.Single(a => a.Sustancia == "Látex").Activa);
        Assert.Equal("Látex", paciente.Alergias);
    }
}
