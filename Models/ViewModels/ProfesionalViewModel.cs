using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.ViewModels
{
    public class ProfesionalViewModel
    {
        public int? IdProfesional { get; set; }

        public int? IdUsuario { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombres { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los apellidos son obligatorios")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Los apellidos deben tener entre 2 y 100 caracteres")]
        public string Apellidos { get; set; } = string.Empty;

        [Required(ErrorMessage = "El registro médico es obligatorio")]
        [StringLength(50, ErrorMessage = "El registro médico no puede superar los 50 caracteres")]
        [RegularExpression(@"^[A-Za-z0-9\-]{3,50}$", ErrorMessage = "El registro médico solo puede contener letras, números y guiones, y tener entre 3 y 50 caracteres")]
        public string RegistroMedico { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "La categoría no puede superar los 100 caracteres")]
        public string? Categoria { get; set; }

        [Phone(ErrorMessage = "Teléfono inválido")]
        [StringLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres")]
        public string? Telefono { get; set; }

        public int? IdEspecialidad { get; set; }

        [StringLength(255, ErrorMessage = "La descripción no puede superar los 255 caracteres")]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El estado es obligatorio")]
        [StringLength(15)]
        public string Estado { get; set; } = "activo";

        public string? ReturnUrl { get; set; }
    }
}
