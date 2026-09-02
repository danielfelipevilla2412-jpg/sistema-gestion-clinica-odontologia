using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Profesional> Profesionales => Set<Profesional>();
    public DbSet<Especialidad> Especialidades => Set<Especialidad>();
    public DbSet<Profesional_Especialidad> ProfesionalEspecialidades => Set<Profesional_Especialidad>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Consultorio> Consultorios => Set<Consultorio>();
    public DbSet<EstadoCita> EstadosCita => Set<EstadoCita>();
    public DbSet<Cita> Citas => Set<Cita>();
    public DbSet<HistoriaClinica> HistoriasClinicas => Set<HistoriaClinica>();

    // Yeray - Agregado DbSet para tabla Registro_Odontograma
    // Permite trazabilidad real por diente en lugar de JSON en ObservacionesGenerales
    public DbSet<RegistroOdontograma> RegistrosOdontograma => Set<RegistroOdontograma>();

    // Yeray - Agregados DbSet para Nota_Clinica y Control_Postoperatorio
    // Reemplazan los JSON "notasClinicas" y "controlesPostoperatorios" que vivían
    // dentro de Historia_Clinica.observaciones_generales por tablas reales,
    // consultables con SQL (mismo criterio que ya se aplicó a Registro_Odontograma).
    public DbSet<NotaClinica> NotasClinicas => Set<NotaClinica>();
    public DbSet<ControlPostoperatorio> ControlesPostoperatorios => Set<ControlPostoperatorio>();

    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<AuditoriaRecuperacion> AuditoriasRecuperacion => Set<AuditoriaRecuperacion>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<DetalleFactura> DetallesFactura => Set<DetalleFactura>();
    public DbSet<CodigoRecuperacion> CodigosRecuperacion => Set<CodigoRecuperacion>();
    public DbSet<PqrEntity> PQRs => Set<PqrEntity>();
    public DbSet<TicketSoporte> TicketsSoporte => Set<TicketSoporte>();
    public DbSet<Inventario> Inventarios => Set<Inventario>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<ConfiguracionGeneral> ConfiguracionesGenerales => Set<ConfiguracionGeneral>();

    // Yeray - DbSet para Documento_Clinico.
    // Antes la vista st-aux-08-documentos-clinicos devolvía Array.Empty<object>() porque
    // no existía ninguna tabla de documentos. Ahora cada archivo subido (radiografía,
    // PDF, consentimiento, etc.) se persiste como fila real consultable con SQL.
    public DbSet<DocumentoClinico> DocumentosClinicos => Set<DocumentoClinico>();

    // ── Disponibilidad y servicios de Profesional (P-01 / U-06) ──────────────
    // Estas cuatro tablas existían en el script SQL pero carecían de entidades,
    // DbSets y lógica de negocio. Se agregan aquí para que EF Core pueda
    // consultarlas y para exponer los endpoints de disponibilidad por profesional.
    public DbSet<HorarioProfesional>  HorariosProfesional  => Set<HorarioProfesional>();
    public DbSet<AusenciaProfesional> AusenciasProfesional => Set<AusenciaProfesional>();
    public DbSet<BloqueoProfesional>  BloqueosProfesional  => Set<BloqueoProfesional>();
    public DbSet<ProfesionalServicio> ProfesionalServicios => Set<ProfesionalServicio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuario", tableBuilder => tableBuilder.UseSqlOutputClause(false));
            entity.HasKey(u => u.IdUsuario);
            entity.Property(u => u.IdUsuario).HasColumnName("id_usuario");
            entity.Property(u => u.CreadoPor).HasColumnName("creado_por");
            entity.Property(u => u.Nombre).HasColumnName("nombre");
            entity.Property(u => u.Apellidos).HasColumnName("apellidos");
            entity.Property(u => u.Correo).HasColumnName("correo");
            entity.Property(u => u.Contrasena).HasColumnName("contrasena");
            entity.Property(u => u.IdRol).HasColumnName("id_rol");
            entity.Property(u => u.Estado).HasColumnName("estado");
            entity.Property(u => u.FechaNacimiento).HasColumnName("fecha_nacimiento");
            entity.Property(u => u.FechaCreacion).HasColumnName("fecha_creacion");
            entity.Property(u => u.UltimoLogin).HasColumnName("ultimo_login");
            entity.Property(u => u.CodigoRecuperacion).HasColumnName("codigo_recuperacion");
            entity.Property(u => u.FechaExpiracionCodigo).HasColumnName("fecha_expiracion_codigo");

            entity.HasOne(u => u.Rol)
                  .WithMany()
                  .HasForeignKey(u => u.IdRol);
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.ToTable("Rol");
            entity.HasKey(r => r.IdRol);
            entity.Property(r => r.IdRol).HasColumnName("id_rol");
            entity.Property(r => r.NombreRol).HasColumnName("nombre_rol");
            entity.Property(r => r.Descripcion).HasColumnName("descripcion");
        });

        modelBuilder.Entity<Paciente>(entity =>
        {
            entity.ToTable("Paciente");
            entity.HasKey(p => p.IdPaciente);
            entity.Property(p => p.IdPaciente).HasColumnName("id_paciente");
            entity.Property(p => p.IdUsuario).HasColumnName("id_usuario");
            entity.Property(p => p.TipoDocumento).HasColumnName("tipo_documento");
            entity.Property(p => p.Documento).HasColumnName("documento");
            entity.Property(p => p.Nombres).HasColumnName("nombres");
            entity.Property(p => p.Apellidos).HasColumnName("apellidos");
            entity.Property(p => p.FechaNacimiento).HasColumnName("fecha_nacimiento");
            entity.Property(p => p.Genero).HasColumnName("genero");
            entity.Property(p => p.Telefono).HasColumnName("telefono");
            entity.Property(p => p.Correo).HasColumnName("correo");
            entity.Property(p => p.Direccion).HasColumnName("direccion");
            entity.Property(p => p.Ciudad).HasColumnName("ciudad");
            entity.Property(p => p.GrupoSanguineo).HasColumnName("grupo_sanguineo");
            entity.Property(p => p.Alergias).HasColumnName("alergias");
            entity.Property(p => p.AntecedentesMedicos).HasColumnName("antecedentes_medicos");
            entity.Property(p => p.ContactoEmergencia).HasColumnName("contacto_emergencia");
            entity.Property(p => p.TelefonoEmergencia).HasColumnName("telefono_emergencia");
            entity.Property(p => p.FechaRegistro).HasColumnName("fecha_registro");
            entity.Property(p => p.Estado).HasColumnName("estado");
            entity.Property(p => p.ArchivoAdjunto).HasColumnName("archivo_adjunto");
        });

        modelBuilder.Entity<Profesional>(entity =>
        {
            entity.ToTable("Profesional");
            entity.HasKey(p => p.IdProfesional);
            entity.Property(p => p.IdProfesional).HasColumnName("id_profesional");
            entity.Property(p => p.IdUsuario).HasColumnName("id_usuario");
            entity.Property(p => p.Nombres).HasColumnName("nombres");
            entity.Property(p => p.Apellidos).HasColumnName("apellidos");
            entity.Property(p => p.RegistroMedico).HasColumnName("registro_medico");
            entity.Property(p => p.Descripcion).HasColumnName("descripcion");
            entity.Property(p => p.Categoria).HasColumnName("categoria");
            entity.Property(p => p.Telefono).HasColumnName("telefono");
            entity.Property(p => p.Estado).HasColumnName("estado");
            entity.Property(p => p.FechaIngreso).HasColumnName("fecha_ingreso");

            entity.HasOne(p => p.Usuario)
                  .WithMany()
                  .HasForeignKey(p => p.IdUsuario)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(p => p.Especialidades)
                  .WithOne(pe => pe.Profesional)
                  .HasForeignKey(pe => pe.IdProfesional)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Especialidad>(entity =>
        {
            entity.ToTable("Especialidad");
            entity.HasKey(e => e.IdEspecialidad);
            entity.Property(e => e.IdEspecialidad).HasColumnName("id_especialidad");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
        });

        modelBuilder.Entity<Profesional_Especialidad>(entity =>
        {
            entity.ToTable("Profesional_Especialidad");
            entity.HasKey(pe => new { pe.IdProfesional, pe.IdEspecialidad });
            entity.Property(pe => pe.IdProfesional).HasColumnName("id_profesional");
            entity.Property(pe => pe.IdEspecialidad).HasColumnName("id_especialidad");
            entity.Property(pe => pe.Principal).HasColumnName("principal");

            entity.HasOne(pe => pe.Profesional)
                  .WithMany(p => p.Especialidades)
                  .HasForeignKey(pe => pe.IdProfesional)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pe => pe.Especialidad)
                  .WithMany()
                  .HasForeignKey(pe => pe.IdEspecialidad)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Servicio>(entity =>
        {
            entity.ToTable("Servicio");
            entity.HasKey(s => s.IdServicio);
            entity.Property(s => s.IdServicio).HasColumnName("id_servicio");
            entity.Property(s => s.Nombre).HasColumnName("nombre");
            entity.Property(s => s.Descripcion).HasColumnName("descripcion");
            entity.Property(s => s.Precio).HasColumnName("precio").HasPrecision(12, 2);
            entity.Property(s => s.Estado).HasColumnName("estado");
        });

        modelBuilder.Entity<Consultorio>(entity =>
        {
            entity.ToTable("Consultorio");
            entity.HasKey(c => c.IdConsultorio);
            entity.Property(c => c.IdConsultorio).HasColumnName("id_consultorio");
            entity.Property(c => c.Nombre).HasColumnName("nombre");
            entity.Property(c => c.Ubicacion).HasColumnName("ubicacion");
            entity.Property(c => c.Tipo).HasColumnName("tipo");
            entity.Property(c => c.NombreEstado).HasColumnName("nombre_estado");
            entity.Property(c => c.Capacidad).HasColumnName("capacidad");
            entity.Property(c => c.Estado).HasColumnName("estado");
        });

        modelBuilder.Entity<EstadoCita>(entity =>
        {
            entity.ToTable("Estado_Cita");
            entity.HasKey(e => e.IdEstado);
            entity.Property(e => e.IdEstado).HasColumnName("id_estado");
            entity.Property(e => e.NombreEstado).HasColumnName("nombre_estado");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
        });

        modelBuilder.Entity<Cita>(entity =>
        {
            entity.ToTable("Cita");
            entity.HasKey(c => c.IdCita);
            entity.Property(c => c.IdCita).HasColumnName("id_cita");
            entity.Property(c => c.IdPaciente).HasColumnName("id_paciente");
            entity.Property(c => c.IdProfesional).HasColumnName("id_profesional");
            entity.Property(c => c.IdServicio).HasColumnName("id_servicio");
            entity.Property(c => c.IdConsultorio).HasColumnName("id_consultorio");
            entity.Property(c => c.IdEstado).HasColumnName("id_estado");
            entity.Property(c => c.FechaHora).HasColumnName("fecha_hora");
            entity.Property(c => c.Estado).HasColumnName("estado");
            entity.Property(c => c.Notas).HasColumnName("notas");

            entity.HasOne(c => c.Paciente)
                  .WithMany()
                  .HasForeignKey(c => c.IdPaciente)
                  .OnDelete(DeleteBehavior.NoAction);
                  

            entity.HasOne(c => c.Profesional)
                  .WithMany()
                  .HasForeignKey(c => c.IdProfesional)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.Servicio)
                  .WithMany()
                  .HasForeignKey(c => c.IdServicio)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.Consultorio)
                  .WithMany()
                  .HasForeignKey(c => c.IdConsultorio)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.EstadoCita)
                  .WithMany()
                  .HasForeignKey(c => c.IdEstado)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(c => new { c.IdProfesional, c.FechaHora })
                  .HasDatabaseName("IX_Cita_Profesional_Fecha")
                  .IsClustered(false);

            entity.HasIndex(c => new { c.IdPaciente, c.FechaHora })
                  .HasDatabaseName("IX_Cita_Paciente_Fecha")
                  .IsClustered(false);

            entity.HasIndex(c => new { c.IdConsultorio, c.FechaHora })
                  .HasDatabaseName("IX_Cita_Consultorio_Fecha")
                  .IsClustered(false);

            entity.HasIndex(c => new { c.Estado, c.FechaHora })
                  .HasDatabaseName("IX_Cita_Estado_Fecha")
                  .IsClustered(false);
        });

        modelBuilder.Entity<HistoriaClinica>(entity =>
        {
            entity.ToTable("Historia_Clinica");
            entity.HasKey(h => h.IdHistoria);
            entity.Property(h => h.IdHistoria).HasColumnName("id_historia");
            entity.Property(h => h.IdPaciente).HasColumnName("id_paciente");
            entity.Property(h => h.FechaApertura).HasColumnName("fecha_apertura");
            entity.Property(h => h.ObservacionesGenerales).HasColumnName("observaciones_generales");
            entity.Property(h => h.Activa).HasColumnName("activa");

            entity.HasOne(h => h.Paciente)
                  .WithOne()
                  .HasForeignKey<HistoriaClinica>(h => h.IdPaciente)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Yeray - Configuración Fluent API para Registro_Odontograma
        // Tabla nueva que reemplaza el JSON en Historia_Clinica.observaciones_generales
        // con registros estructurados por diente con trazabilidad real
        modelBuilder.Entity<RegistroOdontograma>(entity =>
        {
            entity.ToTable("Registro_Odontograma");
            entity.HasKey(r => r.IdRegistro);
            entity.Property(r => r.IdRegistro).HasColumnName("id_registro");
            entity.Property(r => r.IdHistoria).HasColumnName("id_historia");
            entity.Property(r => r.NumeroFdi).HasColumnName("numero_fdi").HasMaxLength(5);
            entity.Property(r => r.NombrePieza).HasColumnName("nombre_pieza").HasMaxLength(150);
            entity.Property(r => r.Estado).HasColumnName("estado").HasMaxLength(50);
            entity.Property(r => r.Observacion).HasColumnName("observacion");
            entity.Property(r => r.FechaRegistro).HasColumnName("fecha_registro");
            entity.Property(r => r.IdProfesional).HasColumnName("id_profesional");
            entity.Property(r => r.IdCita).HasColumnName("id_cita");

            // Yeray - Relación: una HC tiene muchos registros (CASCADE al borrar HC)
            entity.HasOne(r => r.HistoriaClinica)
                  .WithMany()
                  .HasForeignKey(r => r.IdHistoria)
                  .OnDelete(DeleteBehavior.Cascade);

            // Yeray - Relación: profesional SET NULL al eliminar (no pierde el registro)
            entity.HasOne(r => r.Profesional)
                  .WithMany()
                  .HasForeignKey(r => r.IdProfesional)
                  .OnDelete(DeleteBehavior.SetNull);

            // Yeray - Relación: cita SET NULL al eliminar (registro queda huérfano pero no se borra)
            entity.HasOne(r => r.Cita)
                  .WithMany()
                  .HasForeignKey(r => r.IdCita)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Yeray - Configuración Fluent API para Nota_Clinica
        // Reemplaza el arreglo JSON "notasClinicas" (mismo criterio que Registro_Odontograma)
        modelBuilder.Entity<NotaClinica>(entity =>
        {
            entity.ToTable("Nota_Clinica");
            entity.HasKey(n => n.IdNota);
            entity.Property(n => n.IdNota).HasColumnName("id_nota");
            entity.Property(n => n.IdHistoria).HasColumnName("id_historia");
            entity.Property(n => n.IdProfesional).HasColumnName("id_profesional");
            entity.Property(n => n.Fecha).HasColumnName("fecha");
            entity.Property(n => n.Diagnostico).HasColumnName("diagnostico");
            entity.Property(n => n.Procedimiento).HasColumnName("procedimiento");
            entity.Property(n => n.ProximaCita).HasColumnName("proxima_cita").HasMaxLength(50);
            entity.Property(n => n.Estado).HasColumnName("estado").HasMaxLength(20);

            entity.HasOne(n => n.HistoriaClinica)
                  .WithMany()
                  .HasForeignKey(n => n.IdHistoria)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.Profesional)
                  .WithMany()
                  .HasForeignKey(n => n.IdProfesional)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Yeray - Configuración Fluent API para Control_Postoperatorio
        // Reemplaza el objeto JSON "controlesPostoperatorios" indexado por citaId como texto
        modelBuilder.Entity<ControlPostoperatorio>(entity =>
        {
            entity.ToTable("Control_Postoperatorio");
            entity.HasKey(c => c.IdControl);
            entity.Property(c => c.IdControl).HasColumnName("id_control");
            entity.Property(c => c.IdCita).HasColumnName("id_cita");
            entity.Property(c => c.Status).HasColumnName("status").HasMaxLength(20);
            entity.Property(c => c.InstruccionesJson).HasColumnName("instrucciones_json");
            entity.Property(c => c.Observaciones).HasColumnName("observaciones");
            entity.Property(c => c.FechaRegistro).HasColumnName("fecha_registro");

            // Yeray - Una cita tiene, como máximo, un control postoperatorio (1 a 1)
            entity.HasIndex(c => c.IdCita).IsUnique();

            entity.HasOne(c => c.Cita)
                  .WithMany()
                  .HasForeignKey(c => c.IdCita)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Auditoria>(entity =>
        {
            entity.ToTable("Auditoria");
            entity.HasKey(a => a.IdAuditoria);
            entity.Property(a => a.IdAuditoria).HasColumnName("id_auditoria");
            entity.Property(a => a.IdUsuario).HasColumnName("id_usuario");
            entity.Property(a => a.TablaAfectada).HasColumnName("tabla_afectada");
            entity.Property(a => a.IdRegistro).HasColumnName("id_registro");
            entity.Property(a => a.Accion).HasColumnName("accion");
            entity.Property(a => a.IpOrigen).HasColumnName("ip_origen");
            entity.Property(a => a.DatosAnteriores).HasColumnName("datos_anteriores");
            entity.Property(a => a.DatosNuevos).HasColumnName("datos_nuevos");
            entity.Property(a => a.Descripcion).HasColumnName("descripcion");
            entity.Property(a => a.Fecha).HasColumnName("fecha");

            entity.HasOne(a => a.Usuario)
                  .WithMany()
                  .HasForeignKey(a => a.IdUsuario)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Factura>(entity =>
        {
            entity.ToTable("Factura");
            entity.HasKey(f => f.IdFactura);
            entity.Property(f => f.IdFactura).HasColumnName("id_factura");
            entity.Property(f => f.NumeroFactura).HasColumnName("numero_factura");
            entity.Property(f => f.FechaFactura).HasColumnName("fecha_factura");
            entity.Property(f => f.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
            entity.Property(f => f.Total).HasColumnName("total").HasPrecision(12, 2);
            entity.Property(f => f.Estado).HasColumnName("estado");
            entity.Property(f => f.IdPaciente).HasColumnName("id_paciente");
            entity.Property(f => f.Notas).HasColumnName("notas");
            entity.Property(f => f.GeneradaPor).HasColumnName("generada_por");
            entity.Property(f => f.MontoPagado).HasColumnName("monto_pagado").HasPrecision(12, 2);
            entity.Property(f => f.FechaPago).HasColumnName("fecha_pago");
            entity.HasOne(f => f.Paciente)
                  .WithMany()
                  .HasForeignKey(f => f.IdPaciente)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.GeneradaPorUsuario)
                  .WithMany()
                  .HasForeignKey(f => f.GeneradaPor)
                  .OnDelete(DeleteBehavior.Restrict);
                  entity.HasMany(f => f.Detalles)
                 .WithOne(d => d.Factura)
                  .HasForeignKey(d => d.IdFactura)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DetalleFactura>(entity =>
        {
            entity.ToTable("Detalle_Factura");
            entity.HasKey(d => d.IdDetalle);
            entity.Property(d => d.IdDetalle).HasColumnName("id_detalle");
            entity.Property(d => d.IdFactura).HasColumnName("id_factura");
            entity.Property(d => d.IdServicio).HasColumnName("id_servicio");
            entity.Property(d => d.Descripcion).HasColumnName("descripcion");
            entity.Property(d => d.Cantidad).HasColumnName("cantidad");
            entity.Property(d => d.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
            entity.Property(d => d.SubtotalLinea).HasColumnName("subtotal_linea").HasPrecision(12, 2);

            entity.HasOne(d => d.Servicio)
                  .WithMany()
                  .HasForeignKey(d => d.IdServicio)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PqrEntity>(entity =>
        {
            entity.ToTable("PQR");
            entity.HasKey(p => p.IdPqr);
            entity.Property(p => p.IdPqr).HasColumnName("id_pqr");
            entity.Property(p => p.IdPaciente).HasColumnName("id_paciente");
            entity.Property(p => p.IdUsuario).HasColumnName("id_usuario");
            entity.Property(p => p.Tipo).HasColumnName("tipo");
            entity.Property(p => p.Asunto).HasColumnName("asunto");
            entity.Property(p => p.Descripcion).HasColumnName("descripcion");
            entity.Property(p => p.Estado).HasColumnName("estado");
            entity.Property(p => p.Prioridad).HasColumnName("prioridad");
            entity.Property(p => p.FechaCreacion).HasColumnName("fecha_creacion");
            entity.Property(p => p.FechaRespuesta).HasColumnName("fecha_respuesta");
            entity.Property(p => p.Respuesta).HasColumnName("respuesta");
            entity.Property(p => p.AtendidaPor).HasColumnName("atendida_por");
            entity.Property(p => p.EvidenciaAdjunto).HasColumnName("evidencia_adjunto");

            entity.HasOne(p => p.Paciente)
                  .WithMany()
                  .HasForeignKey(p => p.IdPaciente)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Usuario)
                  .WithMany()
                  .HasForeignKey(p => p.IdUsuario)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(p => p.AtendidaPorUsuario)
                  .WithMany()
                  .HasForeignKey(p => p.AtendidaPor)
                  .OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<TicketSoporte>(entity =>
        {
            entity.ToTable("Ticket_Soporte");
            entity.HasKey(t => t.IdTicket);
            entity.Property(t => t.IdTicket).HasColumnName("id_ticket");
            entity.Property(t => t.Referencia).HasColumnName("referencia").HasMaxLength(20);
            entity.Property(t => t.IdUsuario).HasColumnName("id_usuario");
            entity.Property(t => t.Asunto).HasColumnName("asunto");
            entity.Property(t => t.Categoria).HasColumnName("categoria");
            entity.Property(t => t.ModuloAfectado).HasColumnName("modulo_afectado");
            entity.Property(t => t.Severidad).HasColumnName("severidad");
            entity.Property(t => t.Descripcion).HasColumnName("descripcion");
            entity.Property(t => t.CapturaPantalla).HasColumnName("captura_pantalla");
            entity.Property(t => t.Estado).HasColumnName("estado");
            entity.Property(t => t.FechaCreacion).HasColumnName("fecha_creacion");
            entity.Property(t => t.FechaRespuesta).HasColumnName("fecha_respuesta");
            entity.Property(t => t.Respuesta).HasColumnName("respuesta");
            entity.Property(t => t.AtendidoPor).HasColumnName("atendido_por");

            entity.HasOne(t => t.Usuario)
                  .WithMany()
                  .HasForeignKey(t => t.IdUsuario)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(t => t.AtendidoPorUsuario)
                  .WithMany()
                  .HasForeignKey(t => t.AtendidoPor)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Inventario>(entity =>
        {
            entity.ToTable("Inventario");
            entity.HasKey(i => i.IdItem);
            entity.Property(i => i.IdItem).HasColumnName("id_item");
            entity.Property(i => i.Codigo).HasColumnName("codigo");
            entity.Property(i => i.Nombre).HasColumnName("nombre");
            entity.Property(i => i.Categoria).HasColumnName("categoria");
            entity.Property(i => i.StockActual).HasColumnName("stock_actual");
            entity.Property(i => i.StockMinimo).HasColumnName("stock_minimo");
            entity.Property(i => i.UnidadMedida).HasColumnName("unidad_medida");
            entity.Property(i => i.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
            entity.Property(i => i.FechaVencimiento).HasColumnName("fecha_vencimiento");
            entity.Property(i => i.Estado).HasColumnName("estado");
        });

        modelBuilder.Entity<Equipo>(entity =>
        {
            entity.ToTable("Equipo");
            entity.HasKey(e => e.IdEquipo);
            entity.Property(e => e.IdEquipo).HasColumnName("id_equipo");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
            entity.Property(e => e.Modelo).HasColumnName("modelo");
            entity.Property(e => e.Serie).HasColumnName("serie");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.UltimoMantenimiento).HasColumnName("ultimo_mantenimiento");
            entity.Property(e => e.ProximoMantenimiento).HasColumnName("proximo_mantenimiento");
            entity.Property(e => e.Ubicacion).HasColumnName("ubicacion");
        });

        // Yeray - Configuración Fluent API para Documento_Clinico.
        // Tabla nueva que resuelve la vista st-aux-08 que antes devolvía lista vacía.
        // Relaciones: HC → CASCADE delete (un documento sin HC no tiene sentido);
        //             Usuario (SubidoPor) → SET NULL (el documento queda aunque el
        //             usuario sea eliminado).
        // Índice IX_DC_Historia agiliza el GET de documentos por historia clínica.
        modelBuilder.Entity<DocumentoClinico>(entity =>
        {
            entity.ToTable("Documento_Clinico");
            entity.HasKey(d => d.IdDocumento);
            entity.Property(d => d.IdDocumento).HasColumnName("id_documento");
            entity.Property(d => d.IdHistoria).HasColumnName("id_historia");
            entity.Property(d => d.SubidoPor).HasColumnName("subido_por");
            entity.Property(d => d.Tipo).HasColumnName("tipo").HasMaxLength(100);
            entity.Property(d => d.NombreOriginal).HasColumnName("nombre_original").HasMaxLength(255);
            entity.Property(d => d.RutaRelativa).HasColumnName("ruta_relativa").HasMaxLength(500);
            entity.Property(d => d.ContentType).HasColumnName("content_type").HasMaxLength(100);
            entity.Property(d => d.TamanoBytes).HasColumnName("tamano_bytes");
            entity.Property(d => d.FechaSubida).HasColumnName("fecha_subida");
            entity.Property(d => d.Observacion).HasColumnName("observacion").HasMaxLength(500);

            // Índice para listar documentos de una HC sin full-scan
            entity.HasIndex(d => d.IdHistoria).HasDatabaseName("IX_DC_Historia");

            // Historia clínica → CASCADE: sin HC el documento no tiene sentido
            entity.HasOne(d => d.HistoriaClinica)
                  .WithMany()
                  .HasForeignKey(d => d.IdHistoria)
                  .OnDelete(DeleteBehavior.Cascade);

            // Usuario que subió → SET NULL: el documento sigue existiendo aunque el
            // usuario sea eliminado
            entity.HasOne(d => d.SubidoPorUsuario)
                  .WithMany()
                  .HasForeignKey(d => d.SubidoPor)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ConfiguracionGeneral>(entity =>
        {
            entity.ToTable("Configuracion_General");
            entity.HasKey(c => c.IdConfiguracion);
            entity.Property(c => c.IdConfiguracion).HasColumnName("id_configuracion");
            entity.Property(c => c.Clave).HasColumnName("clave");
            entity.Property(c => c.Valor).HasColumnName("valor");
            entity.Property(c => c.Descripcion).HasColumnName("descripcion");
            entity.Property(c => c.Modulo).HasColumnName("modulo");
        });

        // ── HorarioProfesional (P-01 / U-06) ─────────────────────────────────
        modelBuilder.Entity<HorarioProfesional>(entity =>
        {
            entity.ToTable("Horario_Profesional");
            entity.HasKey(h => h.IdHorario);
            entity.Property(h => h.IdHorario).HasColumnName("id_horario");
            entity.Property(h => h.IdProfesional).HasColumnName("id_profesional");
            entity.Property(h => h.DiaSemana).HasColumnName("dia_semana").HasMaxLength(12);
            entity.Property(h => h.HoraInicio).HasColumnName("hora_inicio");
            entity.Property(h => h.HoraFin).HasColumnName("hora_fin");
            entity.Property(h => h.Activo).HasColumnName("activo");

            entity.HasOne(h => h.Profesional)
                  .WithMany()
                  .HasForeignKey(h => h.IdProfesional)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── AusenciaProfesional (P-01 / U-06) ────────────────────────────────
        modelBuilder.Entity<AusenciaProfesional>(entity =>
        {
            entity.ToTable("Ausencia_Profesional");
            entity.HasKey(a => a.IdAusencia);
            entity.Property(a => a.IdAusencia).HasColumnName("id_ausencia");
            entity.Property(a => a.IdProfesional).HasColumnName("id_profesional");
            entity.Property(a => a.Tipo).HasColumnName("tipo").HasMaxLength(15);
            entity.Property(a => a.FechaInicio).HasColumnName("fecha_inicio");
            entity.Property(a => a.FechaFin).HasColumnName("fecha_fin");
            entity.Property(a => a.Duracion).HasColumnName("duracion");
            entity.Property(a => a.Observaciones).HasColumnName("observaciones");
            entity.Property(a => a.AprobadoPor).HasColumnName("aprobado_por");

            entity.HasOne(a => a.Profesional)
                  .WithMany()
                  .HasForeignKey(a => a.IdProfesional)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── BloqueoProfesional (P-01 / U-06) ─────────────────────────────────
        modelBuilder.Entity<BloqueoProfesional>(entity =>
        {
            entity.ToTable("Bloqueo_Profesional");
            entity.HasKey(b => b.IdBloqueo);
            entity.Property(b => b.IdBloqueo).HasColumnName("id_bloqueo");
            entity.Property(b => b.IdProfesional).HasColumnName("id_profesional");
            entity.Property(b => b.FechaInicio).HasColumnName("fecha_inicio");
            entity.Property(b => b.FechaFin).HasColumnName("fecha_fin");
            entity.Property(b => b.Motivo).HasColumnName("motivo").HasMaxLength(150);
            entity.Property(b => b.AprobadoPor).HasColumnName("aprobado_por");

            entity.HasOne(b => b.Profesional)
                  .WithMany()
                  .HasForeignKey(b => b.IdProfesional)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ProfesionalServicio (P-01 / U-06) ────────────────────────────────
        // La tabla tiene PK compuesta (id_profesional, id_servicio).
        // Las columnas precio_personalizado y activo se agregan via ALTER TABLE
        // en el script SQL de forma idempotente.
        modelBuilder.Entity<ProfesionalServicio>(entity =>
        {
            entity.ToTable("Profesional_Servicio");
            entity.HasKey(ps => new { ps.IdProfesional, ps.IdServicio });
            entity.Property(ps => ps.IdProfesional).HasColumnName("id_profesional");
            entity.Property(ps => ps.IdServicio).HasColumnName("id_servicio");
            entity.Property(ps => ps.PrecioPersonalizado)
                  .HasColumnName("precio_personalizado")
                  .HasPrecision(12, 2);
            entity.Property(ps => ps.Activo).HasColumnName("activo");

            entity.HasOne(ps => ps.Profesional)
                  .WithMany()
                  .HasForeignKey(ps => ps.IdProfesional)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ps => ps.Servicio)
                  .WithMany()
                  .HasForeignKey(ps => ps.IdServicio)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}