using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

// ─────────────────────────────────────────────────────────────────────────────
// Yeray - Nueva entidad DocumentoClinico (2025)
//
// MOTIVO: la vista st-aux-08-documentos-clinicos existía pero devolvía un arreglo
// vacío porque no había ninguna tabla que respaldara los archivos clínicos
// (radiografías, PDFs, consentimientos, exámenes de laboratorio, etc.).
// Esta entidad resuelve eso: cada archivo subido queda como una fila real en
// la tabla Documento_Clinico, con referencia a la historia clínica, tipo,
// nombre original, ruta física en disco y quién la subió.
//
// RELACIONES:
//   - Documento_Clinico → Historia_Clinica  (CASCADE delete)
//   - Documento_Clinico → Usuario           (SET NULL si el usuario se elimina)
//
// ALMACENAMIENTO:
//   Los archivos físicos se guardan en wwwroot/uploads/documentos-clinicos/<idHistoria>/.
//   El campo RutaRelativa guarda la ruta relativa desde wwwroot,
//   p.ej. "uploads/documentos-clinicos/3/radiografia_20250831.pdf".
//   Así se puede construir la URL de descarga con IWebHostEnvironment.WebRootPath.
//
// TIPOS PERMITIDOS (validados en el controlador):
//   image/jpeg, image/png, application/pdf  — máximo 10 MB por archivo.
// ─────────────────────────────────────────────────────────────────────────────

[Table("Documento_Clinico")]
public class DocumentoClinico
{
    // ── Clave primaria ────────────────────────────────────────────────────────
    [Key]
    [Column("id_documento")]
    public int IdDocumento { get; set; }

    // ── FK a Historia_Clinica ─────────────────────────────────────────────────
    /// <summary>
    /// Historia clínica a la que pertenece el documento.
    /// CASCADE: al borrar la HC, se borran también sus documentos.
    /// </summary>
    [Required]
    [Column("id_historia")]
    public int IdHistoria { get; set; }

    // ── FK a Usuario (quién lo subió) ─────────────────────────────────────────
    /// <summary>
    /// Usuario (auxiliar/recepcionista/profesional) que subió el archivo.
    /// SET NULL si el usuario es eliminado: el documento queda pero sin autor.
    /// </summary>
    [Column("subido_por")]
    public int? SubidoPor { get; set; }

    // ── Metadatos del archivo ─────────────────────────────────────────────────

    /// <summary>
    /// Tipo de documento seleccionado por el usuario al subir el archivo.
    /// Valores típicos: "Radiografía Panorámica", "Fotografía Intraoral",
    /// "Consentimiento Informado", "Examen de Coagulación", etc.
    /// </summary>
    [Required]
    [Column("tipo")]
    [StringLength(100)]
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Nombre original del archivo tal como vino del navegador.</summary>
    [Required]
    [Column("nombre_original")]
    [StringLength(255)]
    public string NombreOriginal { get; set; } = string.Empty;

    /// <summary>
    /// Ruta relativa desde wwwroot donde se guardó el archivo físico.
    /// Ejemplo: "uploads/documentos-clinicos/5/radiografia_20250831_142310.pdf"
    /// </summary>
    [Required]
    [Column("ruta_relativa")]
    [StringLength(500)]
    public string RutaRelativa { get; set; } = string.Empty;

    /// <summary>Content-type real del archivo (image/jpeg, image/png, application/pdf).</summary>
    [Required]
    [Column("content_type")]
    [StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Tamaño del archivo en bytes (para mostrar en UI sin stat del disco).</summary>
    [Column("tamano_bytes")]
    public long TamanoBytes { get; set; }

    /// <summary>Fecha y hora en que se subió el documento (UTC).</summary>
    [Required]
    [Column("fecha_subida")]
    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;

    /// <summary>Observación libre sobre el documento (opcional).</summary>
    [Column("observacion")]
    [StringLength(500)]
    public string? Observacion { get; set; }

    // ── Navegación ────────────────────────────────────────────────────────────
    public HistoriaClinica? HistoriaClinica { get; set; }
    public Usuario?         SubidoPorUsuario { get; set; }
}
