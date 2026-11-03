using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SmileTrack_MVC.Models.Api.Pacientes;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class PacienteApiValidationTests
{
    [Fact]
    public void Create_AcceptsAlphanumericPassport()
    {
        var dto = ValidPatient();
        dto.TipoDocumento = "PAS";
        dto.Documento = "A1234567";

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void Create_RejectsDocumentTypesOutsideDatabaseCheck()
    {
        var dto = ValidPatient();
        dto.TipoDocumento = "PA";
        dto.Documento = "12345678";

        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void Create_RequiresResponsibleContactForMinor()
    {
        var dto = ValidPatient();
        dto.TipoDocumento = "TI";
        dto.FechaNacimiento = DateTime.Today.AddYears(-15);

        Assert.NotEmpty(Validate(dto));

        dto.ContactoEmergencia = "Responsable";
        dto.TelefonoEmergencia = "3001234567";

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void Update_RejectsInvalidDatabaseValues()
    {
        var dto = new PacienteApiUpdateDto
        {
            Genero = "Masculino",
            GrupoSanguineo = "ABO"
        };

        Assert.NotEmpty(Validate(dto));
    }

    private static PacienteApiCreateDto ValidPatient() => new()
    {
        TipoDocumento = "CC",
        Documento = "12345678",
        Nombres = "Ana",
        Apellidos = "Pérez",
        FechaNacimiento = new DateTime(2000, 1, 1),
        Telefono = "3001234567",
        Correo = "ana@example.com"
    };

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
