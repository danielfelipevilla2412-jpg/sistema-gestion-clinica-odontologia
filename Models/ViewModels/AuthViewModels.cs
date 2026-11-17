using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.ViewModels
{
    public static class AuthPasswordPolicy
    {
        public const string Pattern =
            @"^(?=.{8,100}$)(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9\s]).+$";
        public const string ErrorMessage =
            "La contraseña debe tener entre 8 y 100 caracteres, una mayúscula, un número y un símbolo.";
    }

    public class LoginRequest
    {
        private string _correo = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Correo no válido")]
        [StringLength(150)]
        public string Correo
        {
            get => _correo;
            set => _correo = value?.Trim() ?? string.Empty;
        }

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 100 caracteres")]
        [RegularExpression(AuthPasswordPolicy.Pattern, ErrorMessage = AuthPasswordPolicy.ErrorMessage)]
        [DataType(DataType.Password)]
        public string Contrasena { get; set; } = string.Empty;

        public string? Rol { get; set; }

        public bool Recordar { get; set; }
    }

    public class RegisterRequest
{
        private string _correo = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio")]
    [EmailAddress(ErrorMessage = "Correo no válido")]
    [StringLength(150)]
        public string Correo
        {
            get => _correo;
            set => _correo = value?.Trim() ?? string.Empty;
        }

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    [DataType(DataType.Password)]
    public string Contrasena { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Los apellidos deben tener entre 2 y 100 caracteres")]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma tu contraseña")]
    [Compare("Contrasena", ErrorMessage = "Las contraseñas deben coincidir")]
    [DataType(DataType.Password)]
    public string? ConfirmarContrasena { get; set; }

    [Required(ErrorMessage = "El tipo de documento es obligatorio")]
    [StringLength(5)]
    public string? TipoDocumento { get; set; }

    [Required(ErrorMessage = "El número de documento es obligatorio")]
    [StringLength(20)]
    public string? NumeroDocumento { get; set; }
    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria")]
    [DataType(DataType.Date)]
    public DateTime? FechaNacimiento { get; set; }
    [Required(ErrorMessage = "El teléfono es obligatorio")]
    [StringLength(20)]
    public string? Telefono { get; set; }

    // Yeray: Exige aceptación explícita en servidor para que el registro no dependa solo del checkbox del navegador.
    [Range(typeof(bool), "true", "true", ErrorMessage = "Debes autorizar el tratamiento de datos para crear tu cuenta.")]
    public bool AceptaTratamientoDatos { get; set; }
}

    public class RecoverPasswordRequest
    {
        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        public string Correo { get; set; } = string.Empty;
    }

    public class VerifyRecoveryCodeRequest
    {
        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El código es obligatorio")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "El código debe tener 6 dígitos")]
        public string Codigo { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string TokenTemporal { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        public string? Correo { get; set; }

        [RegularExpression(@"^\d{6}$", ErrorMessage = "El código debe tener 6 dígitos")]
        public string? Codigo { get; set; }

        [Required(ErrorMessage = "La nueva contraseña es obligatoria")]
        [StringLength(100, MinimumLength = 8)]
        [RegularExpression(AuthPasswordPolicy.Pattern, ErrorMessage = AuthPasswordPolicy.ErrorMessage)]
        public string NuevaContrasena { get; set; } = string.Empty;

        [Required(ErrorMessage = "La confirmación de contraseña es obligatoria")]
        [Compare("NuevaContrasena", ErrorMessage = "Las contraseñas deben coincidir")]
        public string ConfirmarContrasena { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "La contraseña actual es obligatoria")]
        [DataType(DataType.Password)]
        public string ContrasenaActual { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es obligatoria")]
        [StringLength(100, MinimumLength = 8)]
        [RegularExpression(AuthPasswordPolicy.Pattern, ErrorMessage = AuthPasswordPolicy.ErrorMessage)]
        [DataType(DataType.Password)]
        public string NuevaContrasena { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirma tu nueva contraseña")]
        [Compare("NuevaContrasena", ErrorMessage = "Las contraseñas deben coincidir")]
        [DataType(DataType.Password)]
        public string ConfirmarContrasena { get; set; } = string.Empty;

    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public string? RecoveryToken { get; set; }
        public object? User { get; set; }
    }
}
