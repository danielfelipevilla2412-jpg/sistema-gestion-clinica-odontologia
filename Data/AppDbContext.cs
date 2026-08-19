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
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<AuditoriaRecuperacion> AuditoriasRecuperacion => Set<AuditoriaRecuperacion>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<CodigoRecuperacion> CodigosRecuperacion => Set<CodigoRecuperacion>();
    public DbSet<PqrEntity> PQRs => Set<PqrEntity>();
    public DbSet<Inventario> Inventarios => Set<Inventario>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<ConfiguracionGeneral> ConfiguracionesGenerales => Set<ConfiguracionGeneral>();

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

            entity.HasOne(f => f.Paciente)
                  .WithMany()
                  .HasForeignKey(f => f.IdPaciente)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.GeneradaPorUsuario)
                  .WithMany()
                  .HasForeignKey(f => f.GeneradaPor)
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
    }
}