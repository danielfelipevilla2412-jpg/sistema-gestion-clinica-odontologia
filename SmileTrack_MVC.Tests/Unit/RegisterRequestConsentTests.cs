using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SmileTrack_MVC.Models.ViewModels;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class RegisterRequestConsentTests
{
    // Yeray: Asegura que el servidor rechace el alta sin consentimiento y acepte el valor explícito true.
    [Fact]
    public void AceptaTratamientoDatos_DebeSerVerdadero()
    {
        var request = new RegisterRequest();
        var context = new ValidationContext(request)
        {
            MemberName = nameof(RegisterRequest.AceptaTratamientoDatos)
        };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateProperty(request.AceptaTratamientoDatos, context, results));

        request.AceptaTratamientoDatos = true;
        results.Clear();
        Assert.True(Validator.TryValidateProperty(request.AceptaTratamientoDatos, context, results));
    }
}
