-- SmileTrack deployment schema: excludes user, patient, clinical, audit, recovery, payment, PQR, commercial, and CIE-10 seed records.
-- Only core role and status catalogs are seeded. No login account is created.
USE [master]
GO
/****** Object:  Database [SmileTrackDB]    Script Date: 12/11/2026 8:06:34 p. m. ******/
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'SmileTrackDB')
BEGIN
    CREATE DATABASE [SmileTrackDB];
END
GO
ALTER DATABASE [SmileTrackDB] SET COMPATIBILITY_LEVEL = 160
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [SmileTrackDB].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [SmileTrackDB] SET ANSI_NULL_DEFAULT OFF
GO
ALTER DATABASE [SmileTrackDB] SET ANSI_NULLS OFF
GO
ALTER DATABASE [SmileTrackDB] SET ANSI_PADDING OFF
GO
ALTER DATABASE [SmileTrackDB] SET ANSI_WARNINGS OFF
GO
ALTER DATABASE [SmileTrackDB] SET ARITHABORT OFF
GO
ALTER DATABASE [SmileTrackDB] SET AUTO_CLOSE ON
GO
ALTER DATABASE [SmileTrackDB] SET AUTO_SHRINK OFF
GO
ALTER DATABASE [SmileTrackDB] SET AUTO_UPDATE_STATISTICS ON
GO
ALTER DATABASE [SmileTrackDB] SET CURSOR_CLOSE_ON_COMMIT OFF
GO
ALTER DATABASE [SmileTrackDB] SET CURSOR_DEFAULT  GLOBAL
GO
ALTER DATABASE [SmileTrackDB] SET CONCAT_NULL_YIELDS_NULL OFF
GO
ALTER DATABASE [SmileTrackDB] SET NUMERIC_ROUNDABORT OFF
GO
ALTER DATABASE [SmileTrackDB] SET QUOTED_IDENTIFIER OFF
GO
ALTER DATABASE [SmileTrackDB] SET RECURSIVE_TRIGGERS OFF
GO
ALTER DATABASE [SmileTrackDB] SET  DISABLE_BROKER
GO
ALTER DATABASE [SmileTrackDB] SET AUTO_UPDATE_STATISTICS_ASYNC OFF
GO
ALTER DATABASE [SmileTrackDB] SET DATE_CORRELATION_OPTIMIZATION OFF
GO
ALTER DATABASE [SmileTrackDB] SET TRUSTWORTHY OFF
GO
ALTER DATABASE [SmileTrackDB] SET ALLOW_SNAPSHOT_ISOLATION OFF
GO
ALTER DATABASE [SmileTrackDB] SET PARAMETERIZATION SIMPLE
GO
ALTER DATABASE [SmileTrackDB] SET READ_COMMITTED_SNAPSHOT ON
GO
ALTER DATABASE [SmileTrackDB] SET HONOR_BROKER_PRIORITY OFF
GO
ALTER DATABASE [SmileTrackDB] SET RECOVERY FULL
GO
ALTER DATABASE [SmileTrackDB] SET  MULTI_USER
GO
ALTER DATABASE [SmileTrackDB] SET PAGE_VERIFY CHECKSUM
GO
ALTER DATABASE [SmileTrackDB] SET DB_CHAINING OFF
GO
ALTER DATABASE [SmileTrackDB] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF )
GO
ALTER DATABASE [SmileTrackDB] SET TARGET_RECOVERY_TIME = 60 SECONDS
GO
ALTER DATABASE [SmileTrackDB] SET DELAYED_DURABILITY = DISABLED
GO
ALTER DATABASE [SmileTrackDB] SET ACCELERATED_DATABASE_RECOVERY = OFF
GO
ALTER DATABASE [SmileTrackDB] SET QUERY_STORE = ON
GO
ALTER DATABASE [SmileTrackDB] SET QUERY_STORE (OPERATION_MODE = READ_WRITE, CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30), DATA_FLUSH_INTERVAL_SECONDS = 900, INTERVAL_LENGTH_MINUTES = 60, MAX_STORAGE_SIZE_MB = 1000, QUERY_CAPTURE_MODE = AUTO, SIZE_BASED_CLEANUP_MODE = AUTO, MAX_PLANS_PER_QUERY = 200, WAIT_STATS_CAPTURE_MODE = ON)
GO
USE [SmileTrackDB]
GO
/****** Object:  Table [dbo].[Consultorio]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Consultorio](
	[id_consultorio] [int] IDENTITY(1,1) NOT NULL,
	[nombre] [nvarchar](100) NOT NULL,
	[ubicacion] [nvarchar](150) NULL,
	[tipo] [nvarchar](50) NULL,
	[nombre_estado] [nvarchar](50) NULL,
	[capacidad] [int] NULL,
	[estado] [nvarchar](15) NOT NULL,
 CONSTRAINT [PK_Consultorio] PRIMARY KEY CLUSTERED
(
	[id_consultorio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Especialidad]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Especialidad](
	[id_especialidad] [int] IDENTITY(1,1) NOT NULL,
	[nombre] [nvarchar](100) NOT NULL,
	[descripcion] [nvarchar](255) NULL,
 CONSTRAINT [PK_Especialidad] PRIMARY KEY CLUSTERED
(
	[id_especialidad] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Estado_Cita]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Estado_Cita](
	[id_estado] [int] IDENTITY(1,1) NOT NULL,
	[nombre_estado] [nvarchar](50) NOT NULL,
	[descripcion] [nvarchar](150) NULL,
 CONSTRAINT [PK_Estado_Cita] PRIMARY KEY CLUSTERED
(
	[id_estado] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Servicio]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Servicio](
	[id_servicio] [int] IDENTITY(1,1) NOT NULL,
	[nombre] [nvarchar](150) NOT NULL,
	[descripcion] [nvarchar](500) NULL,
	[precio] [decimal](12, 2) NOT NULL,
	[estado] [nvarchar](10) NOT NULL,
	[categoria] [varchar](50) NOT NULL,
	[duracion_minutos] [int] NOT NULL,
	[id_especialidad] [int] NULL,
 CONSTRAINT [PK_Servicio] PRIMARY KEY CLUSTERED
(
	[id_servicio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Usuario]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Usuario](
	[id_usuario] [int] IDENTITY(1,1) NOT NULL,
	[creado_por] [int] NULL,
	[nombre] [nvarchar](100) NOT NULL,
	[apellidos] [nvarchar](100) NOT NULL,
	[correo] [nvarchar](150) NOT NULL,
	[contrasena] [nvarchar](255) NOT NULL,
	[id_rol] [int] NOT NULL,
	[estado] [nvarchar](10) NOT NULL,
	[fecha_nacimiento] [datetime2](7) NULL,
	[fecha_creacion] [datetime2](7) NOT NULL,
	[ultimo_login] [datetime2](7) NULL,
	[codigo_recuperacion] [nvarchar](10) NULL,
	[fecha_expiracion_codigo] [datetime2](7) NULL,
	[intentos_fallidos] [int] NOT NULL,
	[bloqueado_hasta] [datetime2](7) NULL,
	[ultimo_logout] [datetime] NULL,
	[version_consentimiento] [nvarchar](10) NULL,
	[fecha_consentimiento] [datetime2](7) NULL,
	[ip_consentimiento] [nvarchar](45) NULL,
 CONSTRAINT [PK_Usuario] PRIMARY KEY CLUSTERED
(
	[id_usuario] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Paciente]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Paciente](
	[id_paciente] [int] IDENTITY(1,1) NOT NULL,
	[id_usuario] [int] NULL,
	[tipo_documento] [nvarchar](5) NOT NULL,
	[documento] [nvarchar](20) NOT NULL,
	[nombres] [nvarchar](100) NOT NULL,
	[apellidos] [nvarchar](100) NOT NULL,
	[fecha_nacimiento] [datetime2](7) NOT NULL,
	[genero] [nvarchar](5) NULL,
	[telefono] [nvarchar](20) NULL,
	[correo] [nvarchar](150) NULL,
	[direccion] [nvarchar](255) NULL,
	[ciudad] [nvarchar](100) NULL,
	[grupo_sanguineo] [nvarchar](5) NULL,
	[alergias] [nvarchar](max) NULL,
	[antecedentes_medicos] [nvarchar](max) NULL,
	[contacto_emergencia] [nvarchar](100) NULL,
	[telefono_emergencia] [nvarchar](20) NULL,
	[fecha_registro] [datetime2](7) NOT NULL,
	[estado] [nvarchar](10) NOT NULL,
	[archivo_adjunto] [nvarchar](255) NULL,
	[medicamentos] [varchar](max) NULL,
	[estado_civil] [varchar](20) NULL,
	[departamento] [varchar](100) NULL,
	[parentesco_emergencia] [varchar](50) NULL,
	[eps_aseguradora] [varchar](150) NULL,
	[numero_poliza] [varchar](50) NULL,
	[tipo_afiliacion] [varchar](20) NULL,
	[medicamentos_actuales] [varchar](max) NULL,
	[antecedentes_familiares] [varchar](max) NULL,
	[id_profesional_asignado] [int] NULL,
 CONSTRAINT [PK_Paciente] PRIMARY KEY CLUSTERED
(
	[id_paciente] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Profesional]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Profesional](
	[id_profesional] [int] IDENTITY(1,1) NOT NULL,
	[id_usuario] [int] NULL,
	[nombres] [nvarchar](100) NOT NULL,
	[apellidos] [nvarchar](100) NOT NULL,
	[registro_medico] [nvarchar](50) NOT NULL,
	[descripcion] [nvarchar](255) NULL,
	[categoria] [nvarchar](100) NULL,
	[telefono] [nvarchar](20) NULL,
	[estado] [nvarchar](15) NOT NULL,
	[fecha_ingreso] [datetime2](7) NULL,
	[credenciales_aprobadas] [bit] NOT NULL,
 CONSTRAINT [PK_Profesional] PRIMARY KEY CLUSTERED
(
	[id_profesional] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Cita]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Cita](
	[id_cita] [int] IDENTITY(1,1) NOT NULL,
	[id_paciente] [int] NOT NULL,
	[id_profesional] [int] NULL,
	[id_servicio] [int] NULL,
	[fecha_hora] [datetime2](7) NOT NULL,
	[estado] [nvarchar](30) NOT NULL,
	[notas] [nvarchar](max) NULL,
	[fecha] [date] NULL,
	[hora_inicio] [time](7) NULL,
	[hora_fin] [time](7) NULL,
	[motivo_consulta] [varchar](max) NULL,
	[notas_previas] [varchar](max) NULL,
	[tipo_cita] [varchar](20) NULL,
	[id_consultorio] [int] NULL,
	[id_estado] [int] NULL,
	[fecha_creacion] [datetime] NULL,
	[creado_por] [int] NULL,
	[archivo_adjunto] [varchar](255) NULL,
	[duracion_minutos] [int] NOT NULL,
	[motivo_cancelacion] [varchar](500) NULL,
 CONSTRAINT [PK_Cita] PRIMARY KEY CLUSTERED
(
	[id_cita] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Profesional_Especialidad]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Profesional_Especialidad](
	[id_profesional] [int] NOT NULL,
	[id_especialidad] [int] NOT NULL,
	[principal] [bit] NOT NULL,
 CONSTRAINT [PK_Profesional_Especialidad] PRIMARY KEY CLUSTERED
(
	[id_profesional] ASC,
	[id_especialidad] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [dbo].[vw_Citas_Dashboard]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vw_Citas_Dashboard]
AS
SELECT
    -- Cita
    c.id_cita,
    c.fecha_hora,
    c.estado,
    c.duracion_minutos,
    c.notas,
    c.motivo_consulta,
    c.notas_previas,
    c.tipo_cita,
    c.fecha_creacion,

    -- Paciente
    pac.id_paciente,
    pac.nombres + ' ' + pac.apellidos AS nombre_paciente,
    pac.documento,
    pac.tipo_documento,
    pac.telefono AS telefono_paciente,
    pac.correo AS correo_paciente,
    pac.genero,
    pac.fecha_nacimiento,
    DATEDIFF(YEAR, pac.fecha_nacimiento, GETDATE()) AS edad_paciente,

    -- Profesional
    prof.id_profesional,
    prof.nombres + ' ' + prof.apellidos AS nombre_profesional,
    prof.registro_medico,
    prof.estado AS estado_profesional,
    prof.telefono AS telefono_profesional,

    -- Especialidad principal del profesional
    (SELECT TOP 1 e.nombre
     FROM Profesional_Especialidad pe
     INNER JOIN Especialidad e ON pe.id_especialidad = e.id_especialidad
     WHERE pe.id_profesional = prof.id_profesional AND pe.principal = 1
    ) AS especialidad_profesional,

    -- Servicio
    s.id_servicio,
    s.nombre AS nombre_servicio,
    s.descripcion AS descripcion_servicio,
    s.precio AS precio_servicio,
    s.duracion_minutos AS duracion_servicio,
    s.categoria AS categoria_servicio,

    -- Consultorio
    cons.id_consultorio,
    cons.nombre AS nombre_consultorio,
    cons.ubicacion AS ubicacion_consultorio,
    cons.tipo AS tipo_consultorio,
    cons.estado AS estado_consultorio,

    -- Estado catálogo
    ec.id_estado AS id_estado_catalogo,
    ec.nombre_estado AS nombre_estado_catalogo,
    ec.descripcion AS descripcion_estado,

    -- Usuario que creó
    u.id_usuario AS creado_por_id,
    u.nombre + ' ' + u.apellidos AS creado_por_nombre,
    u.correo AS correo_creador,

    -- Indicadores calculados
    CASE
        WHEN c.fecha_hora < GETDATE() AND c.estado IN ('Agendada', 'Confirmada', 'Programada')
        THEN 1 ELSE 0
    END AS es_atrasada,

    CASE
        WHEN c.fecha_hora BETWEEN GETDATE() AND DATEADD(HOUR, 24, GETDATE())
        AND c.estado IN ('Agendada', 'Confirmada', 'Programada')
        THEN 1 ELSE 0
    END AS es_proxima_24h,

    DATEDIFF(MINUTE, GETDATE(), c.fecha_hora) AS minutos_hasta_cita

FROM Cita c
INNER JOIN Paciente pac ON c.id_paciente = pac.id_paciente
LEFT JOIN Profesional prof ON c.id_profesional = prof.id_profesional
LEFT JOIN Servicio s ON c.id_servicio = s.id_servicio
LEFT JOIN Consultorio cons ON c.id_consultorio = cons.id_consultorio
LEFT JOIN Estado_Cita ec ON c.id_estado = ec.id_estado
LEFT JOIN Usuario u ON c.creado_por = u.id_usuario;
GO
/****** Object:  Table [dbo].[Horario_Profesional]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Horario_Profesional](
	[id_horario] [int] IDENTITY(1,1) NOT NULL,
	[id_profesional] [int] NOT NULL,
	[dia_semana] [varchar](12) NOT NULL,
	[hora_inicio] [time](7) NOT NULL,
	[hora_fin] [time](7) NOT NULL,
	[activo] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_horario] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Bloqueo_Profesional]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Bloqueo_Profesional](
	[id_bloqueo] [int] IDENTITY(1,1) NOT NULL,
	[id_profesional] [int] NOT NULL,
	[fecha_inicio] [datetime] NOT NULL,
	[fecha_fin] [datetime] NOT NULL,
	[motivo] [varchar](150) NULL,
	[aprobado_por] [int] NULL,
PRIMARY KEY CLUSTERED
(
	[id_bloqueo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Ausencia_Profesional]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Ausencia_Profesional](
	[id_ausencia] [int] IDENTITY(1,1) NOT NULL,
	[id_profesional] [int] NOT NULL,
	[tipo] [varchar](15) NOT NULL,
	[fecha_inicio] [date] NOT NULL,
	[fecha_fin] [date] NOT NULL,
	[duracion] [int] NULL,
	[observaciones] [varchar](max) NULL,
	[aprobado_por] [int] NULL,
PRIMARY KEY CLUSTERED
(
	[id_ausencia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Profesional_Servicio]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Profesional_Servicio](
	[id_profesional] [int] NOT NULL,
	[id_servicio] [int] NOT NULL,
	[precio_personalizado] [decimal](12, 2) NULL,
	[activo] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_profesional] ASC,
	[id_servicio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Rol]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Rol](
	[id_rol] [int] IDENTITY(1,1) NOT NULL,
	[nombre_rol] [nvarchar](50) NOT NULL,
	[descripcion] [nvarchar](200) NULL,
 CONSTRAINT [PK_Rol] PRIMARY KEY CLUSTERED
(
	[id_rol] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [dbo].[vw_Profesionales_Completo]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vw_Profesionales_Completo]
AS
SELECT
    -- Profesional
    p.id_profesional,
    p.nombres,
    p.apellidos,
    p.nombres + ' ' + p.apellidos AS nombre_completo,
    p.registro_medico,
    p.categoria,
    p.telefono,
    p.descripcion,
    p.estado,
    p.fecha_ingreso,

    -- Usuario asociado
    u.id_usuario,
    u.correo,
    u.estado AS estado_usuario,
    u.fecha_creacion AS fecha_creacion_usuario,
    u.ultimo_login,
    u.intentos_fallidos,

    -- Rol
    r.id_rol,
    r.nombre_rol,

    -- Especialidad principal
    (SELECT TOP 1 e.id_especialidad
     FROM Profesional_Especialidad pe
     INNER JOIN Especialidad e ON pe.id_especialidad = e.id_especialidad
     WHERE pe.id_profesional = p.id_profesional AND pe.principal = 1
    ) AS id_especialidad_principal,

    (SELECT TOP 1 e.nombre
     FROM Profesional_Especialidad pe
     INNER JOIN Especialidad e ON pe.id_especialidad = e.id_especialidad
     WHERE pe.id_profesional = p.id_profesional AND pe.principal = 1
    ) AS nombre_especialidad_principal,

    -- Contadores de disponibilidad
    (SELECT COUNT(*)
     FROM Horario_Profesional hp
     WHERE hp.id_profesional = p.id_profesional AND hp.activo = 1
    ) AS total_horarios_activos,

    (SELECT COUNT(*)
     FROM Ausencia_Profesional ap
     WHERE ap.id_profesional = p.id_profesional
       AND ap.fecha_fin >= CAST(GETDATE() AS DATE)
    ) AS total_ausencias_vigentes,

    (SELECT COUNT(*)
     FROM Bloqueo_Profesional bp
     WHERE bp.id_profesional = p.id_profesional
       AND bp.fecha_fin > GETDATE()
    ) AS total_bloqueos_activos,

    -- Contadores de citas
    (SELECT COUNT(*)
     FROM Cita c
     WHERE c.id_profesional = p.id_profesional
       AND c.fecha_hora >= CAST(GETDATE() AS DATE)
       AND c.estado NOT IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado')
    ) AS total_citas_pendientes,

    (SELECT COUNT(*)
     FROM Cita c
     WHERE c.id_profesional = p.id_profesional
       AND YEAR(c.fecha_hora) = YEAR(GETDATE())
       AND MONTH(c.fecha_hora) = MONTH(GETDATE())
    ) AS total_citas_mes_actual,

    -- Servicios que ofrece
    (SELECT COUNT(*)
     FROM Profesional_Servicio ps
     WHERE ps.id_profesional = p.id_profesional AND ps.activo = 1
    ) AS total_servicios_activos,

    -- Indicadores de disponibilidad
    CASE
        WHEN p.estado = 'activo'
         AND NOT EXISTS (
             SELECT 1 FROM Ausencia_Profesional ap
             WHERE ap.id_profesional = p.id_profesional
               AND CAST(GETDATE() AS DATE) BETWEEN ap.fecha_inicio AND ap.fecha_fin
         )
         AND NOT EXISTS (
             SELECT 1 FROM Bloqueo_Profesional bp
             WHERE bp.id_profesional = p.id_profesional
               AND GETDATE() BETWEEN bp.fecha_inicio AND bp.fecha_fin
         )
        THEN 1 ELSE 0
    END AS disponible_ahora

FROM Profesional p
LEFT JOIN Usuario u ON p.id_usuario = u.id_usuario
LEFT JOIN Rol r ON u.id_rol = r.id_rol;
GO
/****** Object:  View [dbo].[vw_Disponibilidad_Profesional]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vw_Disponibilidad_Profesional]
AS
SELECT
    -- Profesional
    p.id_profesional,
    p.nombres + ' ' + p.apellidos AS nombre_profesional,
    p.estado AS estado_profesional,
    p.registro_medico,

    -- Horarios semanales
    hp.id_horario,
    hp.dia_semana,
    hp.hora_inicio,
    hp.hora_fin,
    hp.activo AS horario_activo,

    -- Tiene ausencia hoy
    CASE WHEN EXISTS (
        SELECT 1 FROM Ausencia_Profesional ap
        WHERE ap.id_profesional = p.id_profesional
          AND CAST(GETDATE() AS DATE) BETWEEN ap.fecha_inicio AND ap.fecha_fin
    ) THEN 1 ELSE 0 END AS tiene_ausencia_hoy,

    -- Tipo de ausencia actual
    (SELECT TOP 1 ap.tipo
     FROM Ausencia_Profesional ap
     WHERE ap.id_profesional = p.id_profesional
       AND CAST(GETDATE() AS DATE) BETWEEN ap.fecha_inicio AND ap.fecha_fin
    ) AS tipo_ausencia_actual,

    -- Tiene bloqueo ahora
    CASE WHEN EXISTS (
        SELECT 1 FROM Bloqueo_Profesional bp
        WHERE bp.id_profesional = p.id_profesional
          AND GETDATE() BETWEEN bp.fecha_inicio AND bp.fecha_fin
    ) THEN 1 ELSE 0 END AS tiene_bloqueo_ahora,

    -- Motivo del bloqueo actual
    (SELECT TOP 1 bp.motivo
     FROM Bloqueo_Profesional bp
     WHERE bp.id_profesional = p.id_profesional
       AND GETDATE() BETWEEN bp.fecha_inicio AND bp.fecha_fin
    ) AS motivo_bloqueo_actual,

    -- Próxima cita
    (SELECT MIN(c.fecha_hora)
     FROM Cita c
     WHERE c.id_profesional = p.id_profesional
       AND c.fecha_hora > GETDATE()
       AND c.estado NOT IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado')
    ) AS proxima_cita,

    -- Última cita atendida
    (SELECT MAX(c.fecha_hora)
     FROM Cita c
     WHERE c.id_profesional = p.id_profesional
       AND c.estado IN ('Atendida', 'Completada')
    ) AS ultima_cita_atendida

FROM Profesional p
LEFT JOIN Horario_Profesional hp ON p.id_profesional = hp.id_profesional
WHERE p.estado IN ('activo', 'vacaciones');
GO
/****** Object:  View [dbo].[vw_Citas_Conflictos]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vw_Citas_Conflictos]
AS
WITH CitasConFin AS (
    SELECT
        c.id_cita,
        c.id_profesional,
        c.id_paciente,
        c.id_consultorio,
        c.fecha_hora AS inicio,
        DATEADD(MINUTE, ISNULL(c.duracion_minutos, 60), c.fecha_hora) AS fin,
        c.estado
    FROM Cita c
    WHERE c.estado NOT IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado')
      AND c.fecha_hora >= CAST(GETDATE() AS DATE)
)
SELECT
    c1.id_cita AS cita1_id,
    c2.id_cita AS cita2_id,

    -- Datos del conflicto
    CASE
        WHEN c1.id_profesional = c2.id_profesional AND c1.id_profesional IS NOT NULL
        THEN 'Profesional'
        WHEN c1.id_paciente = c2.id_paciente THEN 'Paciente'
        WHEN c1.id_consultorio = c2.id_consultorio AND c1.id_consultorio IS NOT NULL
        THEN 'Consultorio'
    END AS tipo_conflicto,

    -- Profesional
    c1.id_profesional,
    p.nombres + ' ' + p.apellidos AS nombre_profesional,

    -- Paciente
    c1.id_paciente,
    pac.nombres + ' ' + pac.apellidos AS nombre_paciente,

    -- Consultorio
    c1.id_consultorio,
    cons.nombre AS nombre_consultorio,

    -- Fechas de las citas
    c1.inicio AS cita1_inicio,
    c1.fin AS cita1_fin,
    c2.inicio AS cita2_inicio,
    c2.fin AS cita2_fin,

    -- Minutos de solapamiento
    DATEDIFF(
        MINUTE,
        CASE WHEN c1.inicio > c2.inicio THEN c1.inicio ELSE c2.inicio END,
        CASE WHEN c1.fin < c2.fin THEN c1.fin ELSE c2.fin END
    ) AS minutos_solapamiento,

    -- Estados
    c1.estado AS cita1_estado,
    c2.estado AS cita2_estado

FROM CitasConFin c1
INNER JOIN CitasConFin c2
    ON c1.id_cita < c2.id_cita
   AND (
       (c1.id_profesional = c2.id_profesional AND c1.id_profesional IS NOT NULL)
       OR c1.id_paciente = c2.id_paciente
       OR (c1.id_consultorio = c2.id_consultorio AND c1.id_consultorio IS NOT NULL)
   )
   AND c1.inicio < c2.fin
   AND c2.inicio < c1.fin
LEFT JOIN Profesional p ON c1.id_profesional = p.id_profesional
LEFT JOIN Paciente pac ON c1.id_paciente = pac.id_paciente
LEFT JOIN Consultorio cons ON c1.id_consultorio = cons.id_consultorio;
GO
/****** Object:  View [dbo].[vw_KPI_Citas_Resumen]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vw_KPI_Citas_Resumen]
AS
SELECT
    YEAR(c.fecha_hora) AS anio,
    MONTH(c.fecha_hora) AS mes,
    CAST(DATEFROMPARTS(YEAR(c.fecha_hora), MONTH(c.fecha_hora), 1) AS DATE) AS primer_dia_mes,

    -- Totales generales
    COUNT(*) AS total_citas,
    COUNT(DISTINCT c.id_paciente) AS total_pacientes_unicos,
    COUNT(DISTINCT c.id_profesional) AS total_profesionales_activos,
    COUNT(DISTINCT c.id_consultorio) AS total_consultorios_usados,

    -- Por Estado normalizado
    SUM(CASE
        WHEN c.estado IN ('Agendada', 'Confirmada', 'Programada', 'agendada', 'confirmada', 'programada')
        THEN 1 ELSE 0
    END) AS total_programadas,

    SUM(CASE
        WHEN c.estado IN ('Atendida', 'Completada', 'atendida', 'completada')
        THEN 1 ELSE 0
    END) AS total_atendidas,

    SUM(CASE
        WHEN c.estado IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado')
        THEN 1 ELSE 0
    END) AS total_canceladas,

    SUM(CASE
        WHEN c.estado IN ('No Asistió', 'no asistio', 'inasistencia')
        THEN 1 ELSE 0
    END) AS total_inasistencias,

    -- Tasas calculadas
    CAST(
        SUM(CASE WHEN c.estado IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado') THEN 1 ELSE 0 END) * 100.0
        / NULLIF(COUNT(*), 0)
    AS DECIMAL(5,2)) AS tasa_cancelacion,

    CAST(
        SUM(CASE WHEN c.estado IN ('Atendida', 'Completada', 'atendida', 'completada') THEN 1 ELSE 0 END) * 100.0
        / NULLIF(COUNT(*), 0)
    AS DECIMAL(5,2)) AS tasa_asistencia,

    CAST(
        SUM(CASE WHEN c.estado IN ('No Asistió', 'no asistio', 'inasistencia') THEN 1 ELSE 0 END) * 100.0
        / NULLIF(COUNT(*), 0)
    AS DECIMAL(5,2)) AS tasa_inasistencia,

    -- Duraciones
    AVG(ISNULL(c.duracion_minutos, 60)) AS duracion_promedio_minutos,
    SUM(ISNULL(c.duracion_minutos, 60)) AS duracion_total_minutos,

    -- Servicio más demandado
    (SELECT TOP 1 s.nombre
     FROM Cita c2
     INNER JOIN Servicio s ON c2.id_servicio = s.id_servicio
     WHERE YEAR(c2.fecha_hora) = YEAR(c.fecha_hora)
       AND MONTH(c2.fecha_hora) = MONTH(c.fecha_hora)
     GROUP BY s.nombre
     ORDER BY COUNT(*) DESC
    ) AS servicio_mas_demandado,

    -- Profesional más activo
    (SELECT TOP 1 p.nombres + ' ' + p.apellidos
     FROM Cita c2
     INNER JOIN Profesional p ON c2.id_profesional = p.id_profesional
     WHERE YEAR(c2.fecha_hora) = YEAR(c.fecha_hora)
       AND MONTH(c2.fecha_hora) = MONTH(c.fecha_hora)
     GROUP BY p.id_profesional, p.nombres, p.apellidos
     ORDER BY COUNT(*) DESC
    ) AS profesional_mas_activo

FROM Cita c
GROUP BY YEAR(c.fecha_hora), MONTH(c.fecha_hora);
GO
/****** Object:  Table [dbo].[Alergia_Paciente]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Alergia_Paciente](
	[id_alergia] [int] IDENTITY(1,1) NOT NULL,
	[id_paciente] [int] NOT NULL,
	[sustancia] [varchar](150) NOT NULL,
	[tipo] [varchar](15) NOT NULL,
	[severidad] [varchar](10) NOT NULL,
	[reaccion] [varchar](300) NULL,
	[fecha_registro] [datetime2](7) NOT NULL,
	[activa] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_alergia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Asistencia_Procedimiento]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Asistencia_Procedimiento](
	[id_asistencia] [int] IDENTITY(1,1) NOT NULL,
	[id_cita] [int] NOT NULL,
	[minutos] [int] NOT NULL,
	[inicio] [datetime2](7) NOT NULL,
	[limpieza] [bit] NOT NULL,
	[esterilizacion] [bit] NOT NULL,
	[equipos] [bit] NOT NULL,
	[actualizado_por] [int] NULL,
	[actualizado_en] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_asistencia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Auditoria]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Auditoria](
	[id_auditoria] [int] IDENTITY(1,1) NOT NULL,
	[id_usuario] [int] NULL,
	[tabla_afectada] [varchar](100) NOT NULL,
	[id_registro] [int] NULL,
	[accion] [varchar](45) NOT NULL,
	[ip_origen] [varchar](45) NULL,
	[datos_anteriores] [varchar](max) NULL,
	[datos_nuevos] [varchar](max) NULL,
	[descripcion] [varchar](255) NULL,
	[fecha] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_auditoria] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AuditoriaRecuperacion]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AuditoriaRecuperacion](
	[id_auditoria] [int] IDENTITY(1,1) NOT NULL,
	[id_usuario] [int] NULL,
	[correo_solicitado] [varchar](150) NOT NULL,
	[accion] [varchar](30) NOT NULL,
	[ip_origen] [varchar](45) NULL,
	[fecha] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_auditoria] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Cita_Consultorio_Historial]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Cita_Consultorio_Historial](
	[id_historial] [int] IDENTITY(1,1) NOT NULL,
	[id_cita] [int] NOT NULL,
	[id_consultorio_anterior] [int] NULL,
	[id_consultorio_nuevo] [int] NULL,
	[id_usuario] [int] NULL,
	[fecha_cambio] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_historial] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Cita_Historial_Estado]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Cita_Historial_Estado](
	[id_historial] [int] IDENTITY(1,1) NOT NULL,
	[id_cita] [int] NOT NULL,
	[id_estado] [int] NULL,
	[estado_texto] [varchar](50) NOT NULL,
	[id_usuario] [int] NULL,
	[motivo] [varchar](500) NULL,
	[fecha_cambio] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_historial] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CodigoRecuperacion]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CodigoRecuperacion](
	[id_codigo] [int] IDENTITY(1,1) NOT NULL,
	[id_usuario] [int] NOT NULL,
	[codigo_hash] [varchar](255) NOT NULL,
	[fecha_creacion] [datetime] NOT NULL,
	[fecha_expiracion] [datetime] NOT NULL,
	[intentos_fallidos] [int] NOT NULL,
	[usado] [bit] NOT NULL,
	[ip_origen] [varchar](45) NULL,
PRIMARY KEY CLUSTERED
(
	[id_codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Configuracion_General]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Configuracion_General](
	[id_configuracion] [int] IDENTITY(1,1) NOT NULL,
	[clave] [varchar](100) NOT NULL,
	[valor] [varchar](max) NOT NULL,
	[descripcion] [varchar](255) NULL,
	[modulo] [varchar](50) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_configuracion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Consulta_Clinica]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Consulta_Clinica](
	[id_consulta_clinica] [int] IDENTITY(1,1) NOT NULL,
	[id_historia] [int] NOT NULL,
	[id_cita] [int] NULL,
	[id_nota] [int] NULL,
	[id_proxima_cita] [int] NULL,
	[id_profesional] [int] NULL,
	[fecha_consulta] [datetime2](7) NULL,
	[motivo_consulta] [nvarchar](max) NULL,
	[enfermedad_actual] [nvarchar](max) NULL,
	[antecedentes_medicos] [nvarchar](max) NULL,
	[habitos] [nvarchar](max) NULL,
	[alergias] [nvarchar](max) NULL,
	[hallazgos] [nvarchar](max) NULL,
	[odontograma_observaciones] [nvarchar](max) NULL,
	[examenes_complementarios] [nvarchar](max) NULL,
	[evolucion_clinica] [nvarchar](max) NULL,
	[prescripcion] [nvarchar](max) NULL,
	[origen] [nvarchar](30) NOT NULL,
	[fecha_registro] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Consulta_Clinica] PRIMARY KEY CLUSTERED
(
	[id_consulta_clinica] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Consulta_Clinica_Diagnostico]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Consulta_Clinica_Diagnostico](
	[id_diagnostico] [int] IDENTITY(1,1) NOT NULL,
	[id_consulta_clinica] [int] NOT NULL,
	[codigo_cie10] [nvarchar](10) NULL,
	[descripcion] [nvarchar](500) NOT NULL,
	[es_principal] [bit] NOT NULL,
 CONSTRAINT [PK_Consulta_Clinica_Diagnostico] PRIMARY KEY CLUSTERED
(
	[id_diagnostico] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Consulta_Clinica_Sintoma]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Consulta_Clinica_Sintoma](
	[id_sintoma] [int] IDENTITY(1,1) NOT NULL,
	[id_consulta_clinica] [int] NOT NULL,
	[descripcion] [nvarchar](250) NOT NULL,
	[orden] [int] NOT NULL,
 CONSTRAINT [PK_Consulta_Clinica_Sintoma] PRIMARY KEY CLUSTERED
(
	[id_sintoma] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Consultorio_Estado_Operativo]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Consultorio_Estado_Operativo](
	[id_estado_operativo] [int] IDENTITY(1,1) NOT NULL,
	[id_consultorio] [int] NOT NULL,
	[checklist_json] [nvarchar](max) NOT NULL,
	[observaciones] [nvarchar](2000) NULL,
	[actualizado_por] [int] NULL,
	[actualizado_en] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_estado_operativo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Consultorio_Historial]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Consultorio_Historial](
	[id_historial] [int] IDENTITY(1,1) NOT NULL,
	[id_consultorio] [int] NOT NULL,
	[estado] [varchar](20) NOT NULL,
	[id_usuario] [int] NULL,
	[motivo] [varchar](500) NULL,
	[fecha_cambio] [datetime2](7) NOT NULL,
	[id_estado_anterior] [tinyint] NULL,
	[id_estado_nuevo] [tinyint] NULL,
PRIMARY KEY CLUSTERED
(
	[id_historial] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Control_Postoperatorio]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Control_Postoperatorio](
	[id_control] [int] IDENTITY(1,1) NOT NULL,
	[id_cita] [int] NOT NULL,
	[status] [varchar](20) NOT NULL,
	[instrucciones_json] [varchar](max) NULL,
	[observaciones] [varchar](max) NULL,
	[fecha_registro] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_control] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Credencial_Profesional]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Credencial_Profesional](
	[id_credencial] [int] IDENTITY(1,1) NOT NULL,
	[id_profesional] [int] NOT NULL,
	[subido_por] [int] NULL,
	[tipo] [varchar](50) NOT NULL,
	[nombre_original] [varchar](255) NOT NULL,
	[ruta_privada] [varchar](500) NOT NULL,
	[content_type] [varchar](100) NOT NULL,
	[tamano_bytes] [bigint] NOT NULL,
	[estado_revision] [varchar](15) NOT NULL,
	[observacion_revision] [varchar](500) NULL,
	[fecha_subida] [datetime2](7) NOT NULL,
	[revisado_por] [int] NULL,
	[fecha_revision] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED
(
	[id_credencial] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Detalle_Factura]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Detalle_Factura](
	[id_detalle] [int] IDENTITY(1,1) NOT NULL,
	[id_factura] [int] NOT NULL,
	[id_servicio] [int] NULL,
	[descripcion] [varchar](200) NOT NULL,
	[cantidad] [int] NOT NULL,
	[precio_unitario] [decimal](12, 2) NOT NULL,
	[subtotal_linea] [decimal](12, 2) NOT NULL,
	[descuento] [decimal](12, 2) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_detalle] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Diagnostico_CIE10]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Diagnostico_CIE10](
	[codigo] [nvarchar](10) NOT NULL,
	[descripcion] [nvarchar](500) NOT NULL,
	[version_catalogo] [nvarchar](50) NOT NULL,
	[fuente] [nvarchar](500) NOT NULL,
 CONSTRAINT [PK_Diagnostico_CIE10] PRIMARY KEY CLUSTERED
(
	[codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Documento_Clinico]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Documento_Clinico](
	[id_documento] [int] IDENTITY(1,1) NOT NULL,
	[id_historia] [int] NOT NULL,
	[subido_por] [int] NULL,
	[tipo] [varchar](100) NOT NULL,
	[nombre_original] [varchar](255) NOT NULL,
	[ruta_relativa] [varchar](500) NOT NULL,
	[content_type] [varchar](100) NOT NULL,
	[tamano_bytes] [bigint] NOT NULL,
	[fecha_subida] [datetime2](7) NOT NULL,
	[observacion] [varchar](500) NULL,
	[fecha_documento] [date] NULL,
PRIMARY KEY CLUSTERED
(
	[id_documento] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Equipo]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Equipo](
	[id_equipo] [int] IDENTITY(1,1) NOT NULL,
	[nombre] [varchar](150) NOT NULL,
	[modelo] [varchar](100) NULL,
	[serie] [varchar](100) NULL,
	[status] [varchar](20) NOT NULL,
	[ultimo_mantenimiento] [date] NULL,
	[proximo_mantenimiento] [date] NULL,
	[ubicacion] [varchar](150) NULL,
PRIMARY KEY CLUSTERED
(
	[id_equipo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Estado_Consultorio]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Estado_Consultorio](
	[id_estado_consultorio] [tinyint] NOT NULL,
	[codigo] [varchar](30) NOT NULL,
	[nombre] [varchar](50) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_estado_consultorio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Factura]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Factura](
	[id_factura] [int] IDENTITY(1,1) NOT NULL,
	[numero_factura] [varchar](20) NOT NULL,
	[fecha_factura] [date] NOT NULL,
	[subtotal] [decimal](12, 2) NOT NULL,
	[total] [decimal](12, 2) NOT NULL,
	[estado] [varchar](10) NOT NULL,
	[id_paciente] [int] NOT NULL,
	[notas] [varchar](max) NULL,
	[generada_por] [int] NOT NULL,
	[monto_pagado] [decimal](12, 2) NOT NULL,
	[fecha_pago] [datetime] NULL,
	[motivo_anulacion] [varchar](300) NULL,
	[fecha_anulacion] [datetime] NULL,
	[anulada_por] [int] NULL,
	[id_cita] [int] NULL,
	[id_profesional] [int] NULL,
	[descuento] [decimal](12, 2) NOT NULL,
	[clave_idempotencia] [varchar](36) NULL,
PRIMARY KEY CLUSTERED
(
	[id_factura] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Historia_Clinica]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Historia_Clinica](
	[id_historia] [int] IDENTITY(1,1) NOT NULL,
	[id_paciente] [int] NOT NULL,
	[fecha_apertura] [datetime2](7) NOT NULL,
	[observaciones_generales] [nvarchar](max) NULL,
	[activa] [bit] NOT NULL,
 CONSTRAINT [PK_Historia_Clinica] PRIMARY KEY CLUSTERED
(
	[id_historia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Inventario]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Inventario](
	[id_item] [int] IDENTITY(1,1) NOT NULL,
	[codigo] [varchar](50) NOT NULL,
	[nombre] [varchar](150) NOT NULL,
	[categoria] [varchar](100) NULL,
	[stock_actual] [int] NOT NULL,
	[stock_minimo] [int] NOT NULL,
	[unidad_medida] [varchar](50) NULL,
	[precio_unitario] [decimal](12, 2) NOT NULL,
	[fecha_vencimiento] [date] NULL,
	[estado] [varchar](20) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_item] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Menu]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Menu](
	[id_menu] [int] IDENTITY(1,1) NOT NULL,
	[nombre] [varchar](100) NOT NULL,
	[url] [varchar](200) NOT NULL,
	[icono] [varchar](100) NULL,
	[orden] [int] NOT NULL,
	[id_menu_padre] [int] NULL,
	[modulo] [varchar](50) NULL,
	[activo] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_menu] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Nota_Clinica]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Nota_Clinica](
	[id_nota] [int] IDENTITY(1,1) NOT NULL,
	[id_historia] [int] NOT NULL,
	[id_profesional] [int] NULL,
	[fecha] [datetime] NOT NULL,
	[diagnostico] [varchar](max) NULL,
	[procedimiento] [varchar](max) NULL,
	[proxima_cita] [varchar](50) NULL,
	[estado] [varchar](20) NOT NULL,
	[id_cita] [int] NULL,
PRIMARY KEY CLUSTERED
(
	[id_nota] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Notificacion]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Notificacion](
	[id_notificacion] [int] IDENTITY(1,1) NOT NULL,
	[id_paciente] [int] NULL,
	[id_cita] [int] NULL,
	[tipo] [varchar](30) NOT NULL,
	[titulo] [varchar](200) NOT NULL,
	[contenido] [varchar](max) NOT NULL,
	[canal] [varchar](20) NOT NULL,
	[estado] [varchar](20) NOT NULL,
	[fecha_programada] [datetime2](7) NULL,
	[fecha_envio] [datetime2](7) NULL,
	[fecha_lectura] [datetime2](7) NULL,
	[intentos] [int] NOT NULL,
	[ultimo_error] [varchar](500) NULL,
	[creada_en] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_notificacion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Notificacion_Leida]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Notificacion_Leida](
	[id_notificacion_leida] [int] IDENTITY(1,1) NOT NULL,
	[id_paciente] [int] NOT NULL,
	[id_cita] [int] NOT NULL,
	[fecha_lectura] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_notificacion_leida] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Pago]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Pago](
	[id_pago] [int] IDENTITY(1,1) NOT NULL,
	[id_factura] [int] NOT NULL,
	[monto] [decimal](12, 2) NOT NULL,
	[metodo_pago] [varchar](20) NOT NULL,
	[referencia_pago] [varchar](60) NULL,
	[observaciones] [varchar](300) NULL,
	[fecha_pago] [datetime] NOT NULL,
	[registrado_por] [int] NOT NULL,
	[clave_idempotencia] [varchar](36) NULL,
	[referencia] [varchar](100) NULL,
PRIMARY KEY CLUSTERED
(
	[id_pago] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Pago_Factura]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Pago_Factura](
	[id_pago] [int] IDENTITY(1,1) NOT NULL,
	[id_factura] [int] NOT NULL,
	[monto] [decimal](12, 2) NOT NULL,
	[metodo_pago] [varchar](30) NOT NULL,
	[estado] [varchar](20) NOT NULL,
	[referencia] [varchar](40) NOT NULL,
	[registrado_por] [int] NOT NULL,
	[fecha_utc] [datetime2](7) NOT NULL,
	[observaciones] [varchar](500) NULL,
PRIMARY KEY CLUSTERED
(
	[id_pago] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Plan_Sesion]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Plan_Sesion](
	[id_sesion] [int] IDENTITY(1,1) NOT NULL,
	[id_plan] [int] NOT NULL,
	[fecha_sesion] [datetime2](7) NOT NULL,
	[id_profesional_registro] [int] NOT NULL,
	[id_cita] [int] NULL,
	[id_nota] [int] NULL,
	[registrada_en] [datetime2](7) NOT NULL,
	[anulada] [bit] NOT NULL,
	[motivo_anulacion] [nvarchar](500) NULL,
	[anulada_en] [datetime2](7) NULL,
	[id_profesional_anula] [int] NULL,
 CONSTRAINT [PK_Plan_Sesion] PRIMARY KEY CLUSTERED
(
	[id_sesion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Plan_Tratamiento]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Plan_Tratamiento](
	[id_plan] [int] IDENTITY(1,1) NOT NULL,
	[id_historia] [int] NOT NULL,
	[id_servicio] [int] NOT NULL,
	[id_profesional] [int] NULL,
	[numero_fdi] [nvarchar](5) NULL,
	[estado] [nvarchar](20) NOT NULL,
	[observacion] [nvarchar](1000) NULL,
	[fecha_registro] [datetime2](7) NOT NULL,
	[actualizado_en] [datetime2](7) NOT NULL,
	[sesiones_estimadas] [int] NOT NULL,
 CONSTRAINT [PK_Plan_Tratamiento] PRIMARY KEY CLUSTERED
(
	[id_plan] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PQR]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PQR](
	[id_pqr] [int] IDENTITY(1,1) NOT NULL,
	[id_paciente] [int] NOT NULL,
	[id_usuario] [int] NULL,
	[tipo] [varchar](20) NOT NULL,
	[asunto] [varchar](200) NOT NULL,
	[descripcion] [varchar](max) NOT NULL,
	[estado] [varchar](20) NOT NULL,
	[prioridad] [varchar](10) NOT NULL,
	[fecha_creacion] [datetime] NOT NULL,
	[fecha_respuesta] [datetime] NULL,
	[respuesta] [varchar](max) NULL,
	[atendida_por] [int] NULL,
	[evidencia_adjunto] [varchar](255) NULL,
PRIMARY KEY CLUSTERED
(
	[id_pqr] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Recordatorio_Cita]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Recordatorio_Cita](
	[id_recordatorio] [int] IDENTITY(1,1) NOT NULL,
	[id_cita] [int] NOT NULL,
	[canal] [varchar](20) NOT NULL,
	[estado] [varchar](20) NOT NULL,
	[programado_para] [datetime2](7) NOT NULL,
	[enviado_en] [datetime2](7) NULL,
	[intentos] [int] NOT NULL,
	[ultimo_error] [varchar](500) NULL,
	[creado_en] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_recordatorio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Registro_Odontograma]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Registro_Odontograma](
	[id_registro] [int] IDENTITY(1,1) NOT NULL,
	[id_historia] [int] NOT NULL,
	[numero_fdi] [varchar](5) NOT NULL,
	[nombre_pieza] [varchar](150) NULL,
	[estado] [varchar](50) NOT NULL,
	[observacion] [varchar](max) NULL,
	[fecha_registro] [datetime] NOT NULL,
	[id_profesional] [int] NULL,
	[id_cita] [int] NULL,
PRIMARY KEY CLUSTERED
(
	[id_registro] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Rol_Menu_Permiso]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Rol_Menu_Permiso](
	[id_rol] [int] NOT NULL,
	[id_menu] [int] NOT NULL,
	[puede_ver] [bit] NOT NULL,
	[puede_crear] [bit] NOT NULL,
	[puede_editar] [bit] NOT NULL,
	[puede_eliminar] [bit] NOT NULL,
	[puede_exportar] [bit] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_rol] ASC,
	[id_menu] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Ticket_Soporte]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Ticket_Soporte](
	[id_ticket] [int] IDENTITY(1,1) NOT NULL,
	[referencia] [varchar](20) NOT NULL,
	[id_usuario] [int] NOT NULL,
	[asunto] [varchar](200) NOT NULL,
	[categoria] [varchar](20) NOT NULL,
	[modulo_afectado] [varchar](20) NOT NULL,
	[severidad] [varchar](10) NOT NULL,
	[descripcion] [varchar](max) NOT NULL,
	[captura_pantalla] [varchar](255) NULL,
	[estado] [varchar](20) NOT NULL,
	[fecha_creacion] [datetime] NOT NULL,
	[fecha_respuesta] [datetime] NULL,
	[respuesta] [varchar](max) NULL,
	[atendido_por] [int] NULL,
PRIMARY KEY CLUSTERED
(
	[id_ticket] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UsuariosPreferenciasConsultorio]    Script Date: 12/11/2026 8:06:37 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UsuariosPreferenciasConsultorio](
	[id_preferencia] [int] IDENTITY(1,1) NOT NULL,
	[id_usuario] [int] NOT NULL,
	[id_consultorio] [int] NOT NULL,
	[actualizado_en] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[id_preferencia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

GO
GO
GO

GO

GO

GO

GO

GO

GO

GO

GO

GO

GO

GO

GO

GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO
GO

GO
SET IDENTITY_INSERT [dbo].[Estado_Cita] ON

INSERT [dbo].[Estado_Cita] ([id_estado], [nombre_estado], [descripcion]) VALUES (1, N'Agendada', N'Cita programada y pendiente')
INSERT [dbo].[Estado_Cita] ([id_estado], [nombre_estado], [descripcion]) VALUES (2, N'Confirmada', N'Cita confirmada por paciente o clínica')
INSERT [dbo].[Estado_Cita] ([id_estado], [nombre_estado], [descripcion]) VALUES (3, N'En consulta', N'Paciente en consulta')
INSERT [dbo].[Estado_Cita] ([id_estado], [nombre_estado], [descripcion]) VALUES (4, N'Atendida', N'Cita atendida y finalizada')
INSERT [dbo].[Estado_Cita] ([id_estado], [nombre_estado], [descripcion]) VALUES (5, N'Cancelada', N'Cita cancelada')
INSERT [dbo].[Estado_Cita] ([id_estado], [nombre_estado], [descripcion]) VALUES (6, N'No asistio', N'Paciente no asistió')
INSERT [dbo].[Estado_Cita] ([id_estado], [nombre_estado], [descripcion]) VALUES (7, N'Solicitada', N'Cita solicitada por el paciente pendiente de confirmacion y asignacion')
INSERT [dbo].[Estado_Cita] ([id_estado], [nombre_estado], [descripcion]) VALUES (8, N'Rechazada', N'Solicitud de cita rechazada por la clinica')
SET IDENTITY_INSERT [dbo].[Estado_Cita] OFF
GO
INSERT [dbo].[Estado_Consultorio] ([id_estado_consultorio], [codigo], [nombre]) VALUES (1, N'disponible', N'Disponible')
INSERT [dbo].[Estado_Consultorio] ([id_estado_consultorio], [codigo], [nombre]) VALUES (2, N'ocupado', N'Ocupado')
INSERT [dbo].[Estado_Consultorio] ([id_estado_consultorio], [codigo], [nombre]) VALUES (3, N'mantenimiento', N'En mantenimiento')
INSERT [dbo].[Estado_Consultorio] ([id_estado_consultorio], [codigo], [nombre]) VALUES (4, N'no-disponible', N'No disponible')
INSERT [dbo].[Estado_Consultorio] ([id_estado_consultorio], [codigo], [nombre]) VALUES (5, N'activo', N'Activo')
GO

GO

GO

GO

GO

GO

GO

GO

GO

GO

GO
GO
GO
SET IDENTITY_INSERT [dbo].[Rol] ON

INSERT [dbo].[Rol] ([id_rol], [nombre_rol], [descripcion]) VALUES (1, N'Administrador', N'Acceso total')
INSERT [dbo].[Rol] ([id_rol], [nombre_rol], [descripcion]) VALUES (2, N'Profesional', N'Gestión clínica')
INSERT [dbo].[Rol] ([id_rol], [nombre_rol], [descripcion]) VALUES (3, N'Paciente', N'Paciente de la clínica')
INSERT [dbo].[Rol] ([id_rol], [nombre_rol], [descripcion]) VALUES (4, N'Recepcionista', N'Gestión de citas')
INSERT [dbo].[Rol] ([id_rol], [nombre_rol], [descripcion]) VALUES (5, N'Auxiliar', N'Apoyo clínico')
SET IDENTITY_INSERT [dbo].[Rol] OFF
GO

GO

GO

GO
/****** Object:  Index [IX_AP_Paciente]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_AP_Paciente] ON [dbo].[Alergia_Paciente]
(
	[id_paciente] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_AsistenciaProcedimiento_Cita]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[Asistencia_Procedimiento] ADD  CONSTRAINT [UQ_AsistenciaProcedimiento_Cita] UNIQUE NONCLUSTERED
(
	[id_cita] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AuditoriaRecuperacion_Correo_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_AuditoriaRecuperacion_Correo_Fecha] ON [dbo].[AuditoriaRecuperacion]
(
	[correo_solicitado] ASC,
	[fecha] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AuditoriaRecuperacion_Usuario_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_AuditoriaRecuperacion_Usuario_Fecha] ON [dbo].[AuditoriaRecuperacion]
(
	[id_usuario] ASC,
	[fecha] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AusenciaProfesional_IdProf_Fechas_Covering]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_AusenciaProfesional_IdProf_Fechas_Covering] ON [dbo].[Ausencia_Profesional]
(
	[id_profesional] ASC,
	[fecha_inicio] ASC,
	[fecha_fin] ASC
)
INCLUDE([tipo],[observaciones]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_BloqueoProfesional_IdProf_Fechas_Covering]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_BloqueoProfesional_IdProf_Fechas_Covering] ON [dbo].[Bloqueo_Profesional]
(
	[id_profesional] ASC,
	[fecha_inicio] ASC,
	[fecha_fin] ASC
)
INCLUDE([motivo]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cita_Consultorio_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_Consultorio_Fecha] ON [dbo].[Cita]
(
	[id_consultorio] ASC,
	[fecha_hora] ASC
)
INCLUDE([id_profesional],[id_paciente],[estado])
WHERE ([id_consultorio] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Cita_Estado_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_Estado_Fecha] ON [dbo].[Cita]
(
	[estado] ASC,
	[fecha_hora] ASC
)
INCLUDE([id_paciente],[id_profesional],[id_consultorio]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cita_FechaHora]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_FechaHora] ON [dbo].[Cita]
(
	[fecha_hora] ASC
)
INCLUDE([id_paciente],[id_profesional],[id_consultorio],[estado],[id_servicio]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Cita_FechaHora_Estado_Covering]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_FechaHora_Estado_Covering] ON [dbo].[Cita]
(
	[fecha_hora] ASC,
	[estado] ASC
)
INCLUDE([id_paciente],[id_profesional],[id_servicio],[id_consultorio],[duracion_minutos]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cita_FechaSola_Covering]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_FechaSola_Covering] ON [dbo].[Cita]
(
	[fecha_hora] ASC
)
INCLUDE([id_profesional],[id_paciente],[id_consultorio],[estado]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cita_id_paciente]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_id_paciente] ON [dbo].[Cita]
(
	[id_paciente] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cita_id_profesional]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_id_profesional] ON [dbo].[Cita]
(
	[id_profesional] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cita_id_servicio]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_id_servicio] ON [dbo].[Cita]
(
	[id_servicio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cita_Paciente_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_Paciente_Fecha] ON [dbo].[Cita]
(
	[id_paciente] ASC,
	[fecha_hora] ASC
)
INCLUDE([id_profesional],[id_consultorio],[estado]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cita_Profesional_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_Profesional_Fecha] ON [dbo].[Cita]
(
	[id_profesional] ASC,
	[fecha_hora] ASC
)
INCLUDE([id_paciente],[id_consultorio],[estado])
WHERE ([id_profesional] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cita_Profesional_FechaHora_NoCancel]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Cita_Profesional_FechaHora_NoCancel] ON [dbo].[Cita]
(
	[id_profesional] ASC,
	[fecha_hora] ASC
)
WHERE ([estado]<>'Cancelada' AND [estado]<>'cancelada' AND [estado]<>'Cancelado' AND [estado]<>'cancelado')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_CitaConsultorioHistorial_Cita_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_CitaConsultorioHistorial_Cita_Fecha] ON [dbo].[Cita_Consultorio_Historial]
(
	[id_cita] ASC,
	[fecha_cambio] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_CitaHistorial_Cita_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_CitaHistorial_Cita_Fecha] ON [dbo].[Cita_Historial_Estado]
(
	[id_cita] ASC,
	[fecha_cambio] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_CodigoRecuperacion_Usuario_Usado_Expiracion]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_CodigoRecuperacion_Usuario_Usado_Expiracion] ON [dbo].[CodigoRecuperacion]
(
	[id_usuario] ASC,
	[usado] ASC,
	[fecha_expiracion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Configur__71DCA3DBBE13C66E]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[Configuracion_General] ADD UNIQUE NONCLUSTERED
(
	[clave] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_Consulta_Clinica_Cita]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Consulta_Clinica_Cita] ON [dbo].[Consulta_Clinica]
(
	[id_cita] ASC
)
WHERE ([id_cita] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_Consulta_Clinica_Nota]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Consulta_Clinica_Nota] ON [dbo].[Consulta_Clinica]
(
	[id_nota] ASC
)
WHERE ([id_nota] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Consulta_Clinica_Diagnostico_Consulta]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Consulta_Clinica_Diagnostico_Consulta] ON [dbo].[Consulta_Clinica_Diagnostico]
(
	[id_consulta_clinica] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Consulta_Clinica_Sintoma_Consulta]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Consulta_Clinica_Sintoma_Consulta] ON [dbo].[Consulta_Clinica_Sintoma]
(
	[id_consulta_clinica] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_ConsultorioEstadoOperativo_Consultorio]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[Consultorio_Estado_Operativo] ADD  CONSTRAINT [UQ_ConsultorioEstadoOperativo_Consultorio] UNIQUE NONCLUSTERED
(
	[id_consultorio] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ConsultorioHistorial_Consultorio_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_ConsultorioHistorial_Consultorio_Fecha] ON [dbo].[Consultorio_Historial]
(
	[id_consultorio] ASC,
	[fecha_cambio] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ__Control___6AEC3C08DE406B6D]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[Control_Postoperatorio] ADD UNIQUE NONCLUSTERED
(
	[id_cita] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Credencial_Profesional_Revision]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Credencial_Profesional_Revision] ON [dbo].[Credencial_Profesional]
(
	[id_profesional] ASC,
	[estado_revision] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_DC_Historia]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_DC_Historia] ON [dbo].[Documento_Clinico]
(
	[id_historia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_Especialidad_Nombre]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Especialidad_Nombre] ON [dbo].[Especialidad]
(
	[nombre] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Estado_C__40F9A2062AB69E4C]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[Estado_Consultorio] ADD UNIQUE NONCLUSTERED
(
	[codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Factura__3DC4B2418C951A66]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[Factura] ADD UNIQUE NONCLUSTERED
(
	[numero_factura] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_Factura_Cita_Activa]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Factura_Cita_Activa] ON [dbo].[Factura]
(
	[id_cita] ASC
)
WHERE ([id_cita] IS NOT NULL AND [estado]<>'anulada')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Factura_ClaveIdempotencia]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Factura_ClaveIdempotencia] ON [dbo].[Factura]
(
	[clave_idempotencia] ASC
)
WHERE ([clave_idempotencia] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Historia_Clinica_id_paciente]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Historia_Clinica_id_paciente] ON [dbo].[Historia_Clinica]
(
	[id_paciente] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_HorarioProfesional_IdProf_Activo_Covering]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_HorarioProfesional_IdProf_Activo_Covering] ON [dbo].[Horario_Profesional]
(
	[id_profesional] ASC,
	[activo] ASC
)
INCLUDE([dia_semana],[hora_inicio],[hora_fin]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Inventar__40F9A20681D86D23]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[Inventario] ADD UNIQUE NONCLUSTERED
(
	[codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_NC_Historia]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_NC_Historia] ON [dbo].[Nota_Clinica]
(
	[id_historia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_NC_Cita]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_NC_Cita] ON [dbo].[Nota_Clinica]
(
	[id_cita] ASC
)
WHERE ([id_cita] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Notificacion_Paciente_Estado]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Notificacion_Paciente_Estado] ON [dbo].[Notificacion]
(
	[id_paciente] ASC,
	[estado] ASC,
	[creada_en] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Notificacion_Programada]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Notificacion_Programada] ON [dbo].[Notificacion]
(
	[estado] ASC,
	[fecha_programada] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_NotificacionLeida_Paciente_Cita]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[Notificacion_Leida] ADD  CONSTRAINT [UQ_NotificacionLeida_Paciente_Cita] UNIQUE NONCLUSTERED
(
	[id_paciente] ASC,
	[id_cita] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Paciente_id_usuario]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Paciente_id_usuario] ON [dbo].[Paciente]
(
	[id_usuario] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_Usuario_Correo] ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Usuario_Correo] ON [dbo].[Usuario]
(
	[correo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_Paciente_TipoDocumento_Documento] ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Paciente_TipoDocumento_Documento] ON [dbo].[Paciente]
(
	[tipo_documento] ASC,
	[documento] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Pago_Factura]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Pago_Factura] ON [dbo].[Pago]
(
	[id_factura] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Pago_ClaveIdempotencia]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Pago_ClaveIdempotencia] ON [dbo].[Pago]
(
	[clave_idempotencia] ASC
)
WHERE ([clave_idempotencia] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_PagoFactura_Referencia]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[Pago_Factura] ADD  CONSTRAINT [UQ_PagoFactura_Referencia] UNIQUE NONCLUSTERED
(
	[referencia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_PagoFactura_Factura_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_PagoFactura_Factura_Fecha] ON [dbo].[Pago_Factura]
(
	[id_factura] ASC,
	[fecha_utc] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Plan_Sesion_Plan_Activa]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Plan_Sesion_Plan_Activa] ON [dbo].[Plan_Sesion]
(
	[id_plan] ASC
)
WHERE ([anulada]=(0))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Plan_Tratamiento_Historia]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Plan_Tratamiento_Historia] ON [dbo].[Plan_Tratamiento]
(
	[id_historia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Plan_Tratamiento_Profesional]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Plan_Tratamiento_Profesional] ON [dbo].[Plan_Tratamiento]
(
	[id_profesional] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Profesional_Estado_Covering]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Profesional_Estado_Covering] ON [dbo].[Profesional]
(
	[estado] ASC
)
INCLUDE([nombres],[apellidos],[registro_medico],[id_usuario]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Profesional_id_usuario]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Profesional_id_usuario] ON [dbo].[Profesional]
(
	[id_usuario] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ_Profesional_RegistroMedico]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Profesional_RegistroMedico] ON [dbo].[Profesional]
(
	[registro_medico] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_Profesional_Usuario]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Profesional_Usuario] ON [dbo].[Profesional]
(
	[id_usuario] ASC
)
WHERE ([id_usuario] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Profesional_Especialidad_id_especialidad]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Profesional_Especialidad_id_especialidad] ON [dbo].[Profesional_Especialidad]
(
	[id_especialidad] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ProfesionalEspecialidad_Principal_Covering]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_ProfesionalEspecialidad_Principal_Covering] ON [dbo].[Profesional_Especialidad]
(
	[id_profesional] ASC,
	[principal] ASC
)
INCLUDE([id_especialidad]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_Profesional_Especialidad_Principal]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Profesional_Especialidad_Principal] ON [dbo].[Profesional_Especialidad]
(
	[id_profesional] ASC
)
WHERE ([principal]=(1))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Recordatorio_Estado_Fecha]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Recordatorio_Estado_Fecha] ON [dbo].[Recordatorio_Cita]
(
	[estado] ASC,
	[programado_para] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_RO_Historia]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_RO_Historia] ON [dbo].[Registro_Odontograma]
(
	[id_historia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_RO_Historia_FDI]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_RO_Historia_FDI] ON [dbo].[Registro_Odontograma]
(
	[id_historia] ASC,
	[numero_fdi] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_RO_Profesional]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_RO_Profesional] ON [dbo].[Registro_Odontograma]
(
	[id_profesional] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Servicio_IdEspecialidad]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Servicio_IdEspecialidad] ON [dbo].[Servicio]
(
	[id_especialidad] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Ticket_S__85C4EB33E6DE6C91]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[Ticket_Soporte] ADD UNIQUE NONCLUSTERED
(
	[referencia] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Usuario_id_rol]    Script Date: 12/11/2026 8:06:46 p. m. ******/
CREATE NONCLUSTERED INDEX [IX_Usuario_id_rol] ON [dbo].[Usuario]
(
	[id_rol] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UQ_UsuariosPreferenciasConsultorio_Usuario]    Script Date: 12/11/2026 8:06:46 p. m. ******/
ALTER TABLE [dbo].[UsuariosPreferenciasConsultorio] ADD  CONSTRAINT [UQ_UsuariosPreferenciasConsultorio_Usuario] UNIQUE NONCLUSTERED
(
	[id_usuario] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Alergia_Paciente] ADD  DEFAULT ('otro') FOR [tipo]
GO
ALTER TABLE [dbo].[Alergia_Paciente] ADD  DEFAULT ('leve') FOR [severidad]
GO
ALTER TABLE [dbo].[Alergia_Paciente] ADD  DEFAULT (getutcdate()) FOR [fecha_registro]
GO
ALTER TABLE [dbo].[Alergia_Paciente] ADD  DEFAULT ((1)) FOR [activa]
GO
ALTER TABLE [dbo].[Asistencia_Procedimiento] ADD  DEFAULT ((0)) FOR [minutos]
GO
ALTER TABLE [dbo].[Asistencia_Procedimiento] ADD  DEFAULT ((0)) FOR [limpieza]
GO
ALTER TABLE [dbo].[Asistencia_Procedimiento] ADD  DEFAULT ((0)) FOR [esterilizacion]
GO
ALTER TABLE [dbo].[Asistencia_Procedimiento] ADD  DEFAULT ((0)) FOR [equipos]
GO
ALTER TABLE [dbo].[Asistencia_Procedimiento] ADD  DEFAULT (sysutcdatetime()) FOR [actualizado_en]
GO
ALTER TABLE [dbo].[Auditoria] ADD  DEFAULT (getdate()) FOR [fecha]
GO
ALTER TABLE [dbo].[AuditoriaRecuperacion] ADD  CONSTRAINT [DF_AuditoriaRecuperacion_Fecha]  DEFAULT (getdate()) FOR [fecha]
GO
ALTER TABLE [dbo].[Cita] ADD  CONSTRAINT [DF_Cita_FechaCreacion]  DEFAULT (getdate()) FOR [fecha_creacion]
GO
ALTER TABLE [dbo].[Cita] ADD  CONSTRAINT [DF_Cita_DuracionMinutos]  DEFAULT ((60)) FOR [duracion_minutos]
GO
ALTER TABLE [dbo].[Cita_Consultorio_Historial] ADD  DEFAULT (sysutcdatetime()) FOR [fecha_cambio]
GO
ALTER TABLE [dbo].[Cita_Historial_Estado] ADD  DEFAULT (sysutcdatetime()) FOR [fecha_cambio]
GO
ALTER TABLE [dbo].[CodigoRecuperacion] ADD  CONSTRAINT [DF_CodigoRecuperacion_FechaCreacion]  DEFAULT (getdate()) FOR [fecha_creacion]
GO
ALTER TABLE [dbo].[CodigoRecuperacion] ADD  CONSTRAINT [DF_CodigoRecuperacion_IntentosFallidos]  DEFAULT ((0)) FOR [intentos_fallidos]
GO
ALTER TABLE [dbo].[CodigoRecuperacion] ADD  CONSTRAINT [DF_CodigoRecuperacion_Usado]  DEFAULT ((0)) FOR [usado]
GO
ALTER TABLE [dbo].[Configuracion_General] ADD  DEFAULT ('general') FOR [modulo]
GO
ALTER TABLE [dbo].[Consulta_Clinica] ADD  CONSTRAINT [DF_Consulta_Clinica_origen]  DEFAULT (N'captura') FOR [origen]
GO
ALTER TABLE [dbo].[Consulta_Clinica] ADD  CONSTRAINT [DF_Consulta_Clinica_fecha_registro]  DEFAULT (sysutcdatetime()) FOR [fecha_registro]
GO
ALTER TABLE [dbo].[Consultorio_Estado_Operativo] ADD  DEFAULT (N'[]') FOR [checklist_json]
GO
ALTER TABLE [dbo].[Consultorio_Estado_Operativo] ADD  DEFAULT (sysutcdatetime()) FOR [actualizado_en]
GO
ALTER TABLE [dbo].[Consultorio_Historial] ADD  DEFAULT (sysutcdatetime()) FOR [fecha_cambio]
GO
ALTER TABLE [dbo].[Control_Postoperatorio] ADD  DEFAULT ('stable') FOR [status]
GO
ALTER TABLE [dbo].[Control_Postoperatorio] ADD  DEFAULT (getdate()) FOR [fecha_registro]
GO
ALTER TABLE [dbo].[Credencial_Profesional] ADD  CONSTRAINT [DF_Credencial_Profesional_Estado]  DEFAULT ('pendiente') FOR [estado_revision]
GO
ALTER TABLE [dbo].[Credencial_Profesional] ADD  CONSTRAINT [DF_Credencial_Profesional_Fecha]  DEFAULT (getutcdate()) FOR [fecha_subida]
GO
ALTER TABLE [dbo].[Detalle_Factura] ADD  DEFAULT ((1)) FOR [cantidad]
GO
ALTER TABLE [dbo].[Detalle_Factura] ADD  DEFAULT ((0.00)) FOR [precio_unitario]
GO
ALTER TABLE [dbo].[Detalle_Factura] ADD  DEFAULT ((0.00)) FOR [subtotal_linea]
GO
ALTER TABLE [dbo].[Detalle_Factura] ADD  CONSTRAINT [DF_DetalleFactura_Descuento]  DEFAULT ((0.00)) FOR [descuento]
GO
ALTER TABLE [dbo].[Documento_Clinico] ADD  DEFAULT ((0)) FOR [tamano_bytes]
GO
ALTER TABLE [dbo].[Documento_Clinico] ADD  DEFAULT (getutcdate()) FOR [fecha_subida]
GO
ALTER TABLE [dbo].[Equipo] ADD  DEFAULT ('operativo') FOR [status]
GO
ALTER TABLE [dbo].[Factura] ADD  DEFAULT (CONVERT([date],getdate())) FOR [fecha_factura]
GO
ALTER TABLE [dbo].[Factura] ADD  DEFAULT ((0.00)) FOR [subtotal]
GO
ALTER TABLE [dbo].[Factura] ADD  DEFAULT ((0.00)) FOR [total]
GO
ALTER TABLE [dbo].[Factura] ADD  DEFAULT ('pendiente') FOR [estado]
GO
ALTER TABLE [dbo].[Factura] ADD  DEFAULT ((0.00)) FOR [monto_pagado]
GO
ALTER TABLE [dbo].[Factura] ADD  CONSTRAINT [DF_Factura_Descuento]  DEFAULT ((0.00)) FOR [descuento]
GO
ALTER TABLE [dbo].[Horario_Profesional] ADD  DEFAULT ((1)) FOR [activo]
GO
ALTER TABLE [dbo].[Inventario] ADD  DEFAULT ((0)) FOR [stock_actual]
GO
ALTER TABLE [dbo].[Inventario] ADD  DEFAULT ((0)) FOR [stock_minimo]
GO
ALTER TABLE [dbo].[Inventario] ADD  DEFAULT ((0.00)) FOR [precio_unitario]
GO
ALTER TABLE [dbo].[Inventario] ADD  DEFAULT ('disponible') FOR [estado]
GO
ALTER TABLE [dbo].[Menu] ADD  DEFAULT ((0)) FOR [orden]
GO
ALTER TABLE [dbo].[Menu] ADD  DEFAULT ((1)) FOR [activo]
GO
ALTER TABLE [dbo].[Nota_Clinica] ADD  DEFAULT (getdate()) FOR [fecha]
GO
ALTER TABLE [dbo].[Nota_Clinica] ADD  DEFAULT ('Realizado') FOR [estado]
GO
ALTER TABLE [dbo].[Notificacion] ADD  DEFAULT ('interno') FOR [canal]
GO
ALTER TABLE [dbo].[Notificacion] ADD  DEFAULT ('pendiente') FOR [estado]
GO
ALTER TABLE [dbo].[Notificacion] ADD  DEFAULT ((0)) FOR [intentos]
GO
ALTER TABLE [dbo].[Notificacion] ADD  DEFAULT (sysutcdatetime()) FOR [creada_en]
GO
ALTER TABLE [dbo].[Notificacion_Leida] ADD  DEFAULT (sysutcdatetime()) FOR [fecha_lectura]
GO
ALTER TABLE [dbo].[Pago] ADD  DEFAULT ('efectivo') FOR [metodo_pago]
GO
ALTER TABLE [dbo].[Pago] ADD  DEFAULT (getdate()) FOR [fecha_pago]
GO
ALTER TABLE [dbo].[Plan_Sesion] ADD  CONSTRAINT [DF_Plan_Sesion_fecha]  DEFAULT (sysutcdatetime()) FOR [fecha_sesion]
GO
ALTER TABLE [dbo].[Plan_Sesion] ADD  CONSTRAINT [DF_Plan_Sesion_registrada]  DEFAULT (sysutcdatetime()) FOR [registrada_en]
GO
ALTER TABLE [dbo].[Plan_Sesion] ADD  CONSTRAINT [DF_Plan_Sesion_anulada]  DEFAULT ((0)) FOR [anulada]
GO
ALTER TABLE [dbo].[Plan_Tratamiento] ADD  CONSTRAINT [DF_Plan_Tratamiento_estado]  DEFAULT (N'propuesto') FOR [estado]
GO
ALTER TABLE [dbo].[Plan_Tratamiento] ADD  CONSTRAINT [DF_Plan_Tratamiento_fecha_registro]  DEFAULT (sysutcdatetime()) FOR [fecha_registro]
GO
ALTER TABLE [dbo].[Plan_Tratamiento] ADD  CONSTRAINT [DF_Plan_Tratamiento_actualizado_en]  DEFAULT (sysutcdatetime()) FOR [actualizado_en]
GO
ALTER TABLE [dbo].[Plan_Tratamiento] ADD  CONSTRAINT [DF_Plan_Tratamiento_sesiones_estimadas]  DEFAULT ((0)) FOR [sesiones_estimadas]
GO
ALTER TABLE [dbo].[PQR] ADD  DEFAULT ('recibida') FOR [estado]
GO
ALTER TABLE [dbo].[PQR] ADD  DEFAULT ('media') FOR [prioridad]
GO
ALTER TABLE [dbo].[PQR] ADD  DEFAULT (getdate()) FOR [fecha_creacion]
GO
ALTER TABLE [dbo].[Profesional] ADD  CONSTRAINT [DF_Profesional_CredencialesAprobadas]  DEFAULT ((1)) FOR [credenciales_aprobadas]
GO
ALTER TABLE [dbo].[Profesional_Servicio] ADD  CONSTRAINT [DF_PS_Activo]  DEFAULT ((1)) FOR [activo]
GO
ALTER TABLE [dbo].[Recordatorio_Cita] ADD  DEFAULT ('pendiente') FOR [estado]
GO
ALTER TABLE [dbo].[Recordatorio_Cita] ADD  DEFAULT ((0)) FOR [intentos]
GO
ALTER TABLE [dbo].[Recordatorio_Cita] ADD  DEFAULT (sysutcdatetime()) FOR [creado_en]
GO
ALTER TABLE [dbo].[Registro_Odontograma] ADD  DEFAULT (getdate()) FOR [fecha_registro]
GO
ALTER TABLE [dbo].[Rol_Menu_Permiso] ADD  DEFAULT ((0)) FOR [puede_ver]
GO
ALTER TABLE [dbo].[Rol_Menu_Permiso] ADD  DEFAULT ((0)) FOR [puede_crear]
GO
ALTER TABLE [dbo].[Rol_Menu_Permiso] ADD  DEFAULT ((0)) FOR [puede_editar]
GO
ALTER TABLE [dbo].[Rol_Menu_Permiso] ADD  DEFAULT ((0)) FOR [puede_eliminar]
GO
ALTER TABLE [dbo].[Rol_Menu_Permiso] ADD  DEFAULT ((0)) FOR [puede_exportar]
GO
ALTER TABLE [dbo].[Servicio] ADD  DEFAULT ('general') FOR [categoria]
GO
ALTER TABLE [dbo].[Servicio] ADD  DEFAULT ((30)) FOR [duracion_minutos]
GO
ALTER TABLE [dbo].[Ticket_Soporte] ADD  DEFAULT ('media') FOR [severidad]
GO
ALTER TABLE [dbo].[Ticket_Soporte] ADD  DEFAULT ('abierto') FOR [estado]
GO
ALTER TABLE [dbo].[Ticket_Soporte] ADD  DEFAULT (getdate()) FOR [fecha_creacion]
GO
ALTER TABLE [dbo].[Usuario] ADD  CONSTRAINT [DF_Usuario_IntentosFallidos]  DEFAULT ((0)) FOR [intentos_fallidos]
GO
ALTER TABLE [dbo].[UsuariosPreferenciasConsultorio] ADD  DEFAULT (sysutcdatetime()) FOR [actualizado_en]
GO
ALTER TABLE [dbo].[Alergia_Paciente]  WITH CHECK ADD  CONSTRAINT [FK_AP_Paciente] FOREIGN KEY([id_paciente])
REFERENCES [dbo].[Paciente] ([id_paciente])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Alergia_Paciente] CHECK CONSTRAINT [FK_AP_Paciente]
GO
ALTER TABLE [dbo].[Asistencia_Procedimiento]  WITH CHECK ADD  CONSTRAINT [FK_AsistenciaProcedimiento_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Asistencia_Procedimiento] CHECK CONSTRAINT [FK_AsistenciaProcedimiento_Cita]
GO
ALTER TABLE [dbo].[Asistencia_Procedimiento]  WITH CHECK ADD  CONSTRAINT [FK_AsistenciaProcedimiento_Usuario] FOREIGN KEY([actualizado_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Asistencia_Procedimiento] CHECK CONSTRAINT [FK_AsistenciaProcedimiento_Usuario]
GO
ALTER TABLE [dbo].[Auditoria]  WITH CHECK ADD  CONSTRAINT [FK_Auditoria_Usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[Auditoria] CHECK CONSTRAINT [FK_Auditoria_Usuario]
GO
ALTER TABLE [dbo].[AuditoriaRecuperacion]  WITH CHECK ADD  CONSTRAINT [FK_AuditoriaRecuperacion_Usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[AuditoriaRecuperacion] CHECK CONSTRAINT [FK_AuditoriaRecuperacion_Usuario]
GO
ALTER TABLE [dbo].[Ausencia_Profesional]  WITH CHECK ADD  CONSTRAINT [FK_AP_Profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
GO
ALTER TABLE [dbo].[Ausencia_Profesional] CHECK CONSTRAINT [FK_AP_Profesional]
GO
ALTER TABLE [dbo].[Ausencia_Profesional]  WITH CHECK ADD  CONSTRAINT [FK_AusenciaProfesional_AprobadoPor] FOREIGN KEY([aprobado_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Ausencia_Profesional] CHECK CONSTRAINT [FK_AusenciaProfesional_AprobadoPor]
GO
ALTER TABLE [dbo].[Bloqueo_Profesional]  WITH CHECK ADD  CONSTRAINT [FK_BloqueoProfesional_AprobadoPor] FOREIGN KEY([aprobado_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Bloqueo_Profesional] CHECK CONSTRAINT [FK_BloqueoProfesional_AprobadoPor]
GO
ALTER TABLE [dbo].[Bloqueo_Profesional]  WITH CHECK ADD  CONSTRAINT [FK_BP_Profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
GO
ALTER TABLE [dbo].[Bloqueo_Profesional] CHECK CONSTRAINT [FK_BP_Profesional]
GO
ALTER TABLE [dbo].[Cita]  WITH CHECK ADD  CONSTRAINT [FK_Cita_Consultorio] FOREIGN KEY([id_consultorio])
REFERENCES [dbo].[Consultorio] ([id_consultorio])
GO
ALTER TABLE [dbo].[Cita] CHECK CONSTRAINT [FK_Cita_Consultorio]
GO
ALTER TABLE [dbo].[Cita]  WITH CHECK ADD  CONSTRAINT [FK_Cita_CreadoPor] FOREIGN KEY([creado_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Cita] CHECK CONSTRAINT [FK_Cita_CreadoPor]
GO
ALTER TABLE [dbo].[Cita]  WITH CHECK ADD  CONSTRAINT [FK_Cita_EstadoCita] FOREIGN KEY([id_estado])
REFERENCES [dbo].[Estado_Cita] ([id_estado])
GO
ALTER TABLE [dbo].[Cita] CHECK CONSTRAINT [FK_Cita_EstadoCita]
GO
ALTER TABLE [dbo].[Cita]  WITH CHECK ADD  CONSTRAINT [FK_Cita_Paciente_id_paciente] FOREIGN KEY([id_paciente])
REFERENCES [dbo].[Paciente] ([id_paciente])
GO
ALTER TABLE [dbo].[Cita] CHECK CONSTRAINT [FK_Cita_Paciente_id_paciente]
GO
ALTER TABLE [dbo].[Cita]  WITH CHECK ADD  CONSTRAINT [FK_Cita_Profesional_id_profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Cita] CHECK CONSTRAINT [FK_Cita_Profesional_id_profesional]
GO
ALTER TABLE [dbo].[Cita]  WITH CHECK ADD  CONSTRAINT [FK_Cita_Servicio_id_servicio] FOREIGN KEY([id_servicio])
REFERENCES [dbo].[Servicio] ([id_servicio])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Cita] CHECK CONSTRAINT [FK_Cita_Servicio_id_servicio]
GO
ALTER TABLE [dbo].[Cita_Consultorio_Historial]  WITH CHECK ADD  CONSTRAINT [FK_CitaConsultorioHistorial_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Cita_Consultorio_Historial] CHECK CONSTRAINT [FK_CitaConsultorioHistorial_Cita]
GO
ALTER TABLE [dbo].[Cita_Consultorio_Historial]  WITH CHECK ADD  CONSTRAINT [FK_CitaConsultorioHistorial_ConsultorioAnterior] FOREIGN KEY([id_consultorio_anterior])
REFERENCES [dbo].[Consultorio] ([id_consultorio])
GO
ALTER TABLE [dbo].[Cita_Consultorio_Historial] CHECK CONSTRAINT [FK_CitaConsultorioHistorial_ConsultorioAnterior]
GO
ALTER TABLE [dbo].[Cita_Consultorio_Historial]  WITH CHECK ADD  CONSTRAINT [FK_CitaConsultorioHistorial_ConsultorioNuevo] FOREIGN KEY([id_consultorio_nuevo])
REFERENCES [dbo].[Consultorio] ([id_consultorio])
GO
ALTER TABLE [dbo].[Cita_Consultorio_Historial] CHECK CONSTRAINT [FK_CitaConsultorioHistorial_ConsultorioNuevo]
GO
ALTER TABLE [dbo].[Cita_Consultorio_Historial]  WITH CHECK ADD  CONSTRAINT [FK_CitaConsultorioHistorial_Usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Cita_Consultorio_Historial] CHECK CONSTRAINT [FK_CitaConsultorioHistorial_Usuario]
GO
ALTER TABLE [dbo].[Cita_Historial_Estado]  WITH CHECK ADD  CONSTRAINT [FK_CitaHistorial_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Cita_Historial_Estado] CHECK CONSTRAINT [FK_CitaHistorial_Cita]
GO
ALTER TABLE [dbo].[Cita_Historial_Estado]  WITH CHECK ADD  CONSTRAINT [FK_CitaHistorial_Estado] FOREIGN KEY([id_estado])
REFERENCES [dbo].[Estado_Cita] ([id_estado])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Cita_Historial_Estado] CHECK CONSTRAINT [FK_CitaHistorial_Estado]
GO
ALTER TABLE [dbo].[Cita_Historial_Estado]  WITH CHECK ADD  CONSTRAINT [FK_CitaHistorial_Usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Cita_Historial_Estado] CHECK CONSTRAINT [FK_CitaHistorial_Usuario]
GO
ALTER TABLE [dbo].[CodigoRecuperacion]  WITH CHECK ADD  CONSTRAINT [FK_CodigoRecuperacion_Usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[CodigoRecuperacion] CHECK CONSTRAINT [FK_CodigoRecuperacion_Usuario]
GO
ALTER TABLE [dbo].[Consulta_Clinica]  WITH CHECK ADD  CONSTRAINT [FK_Consulta_Clinica_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Consulta_Clinica] CHECK CONSTRAINT [FK_Consulta_Clinica_Cita]
GO
ALTER TABLE [dbo].[Consulta_Clinica]  WITH CHECK ADD  CONSTRAINT [FK_Consulta_Clinica_Historia] FOREIGN KEY([id_historia])
REFERENCES [dbo].[Historia_Clinica] ([id_historia])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Consulta_Clinica] CHECK CONSTRAINT [FK_Consulta_Clinica_Historia]
GO
ALTER TABLE [dbo].[Consulta_Clinica]  WITH CHECK ADD  CONSTRAINT [FK_Consulta_Clinica_Nota] FOREIGN KEY([id_nota])
REFERENCES [dbo].[Nota_Clinica] ([id_nota])
GO
ALTER TABLE [dbo].[Consulta_Clinica] CHECK CONSTRAINT [FK_Consulta_Clinica_Nota]
GO
ALTER TABLE [dbo].[Consulta_Clinica]  WITH CHECK ADD  CONSTRAINT [FK_Consulta_Clinica_Profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Consulta_Clinica] CHECK CONSTRAINT [FK_Consulta_Clinica_Profesional]
GO
ALTER TABLE [dbo].[Consulta_Clinica]  WITH CHECK ADD  CONSTRAINT [FK_Consulta_Clinica_ProximaCita] FOREIGN KEY([id_proxima_cita])
REFERENCES [dbo].[Cita] ([id_cita])
GO
ALTER TABLE [dbo].[Consulta_Clinica] CHECK CONSTRAINT [FK_Consulta_Clinica_ProximaCita]
GO
ALTER TABLE [dbo].[Consulta_Clinica_Diagnostico]  WITH CHECK ADD  CONSTRAINT [FK_Consulta_Clinica_Diagnostico_CIE10] FOREIGN KEY([codigo_cie10])
REFERENCES [dbo].[Diagnostico_CIE10] ([codigo])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Consulta_Clinica_Diagnostico] CHECK CONSTRAINT [FK_Consulta_Clinica_Diagnostico_CIE10]
GO
ALTER TABLE [dbo].[Consulta_Clinica_Diagnostico]  WITH CHECK ADD  CONSTRAINT [FK_Consulta_Clinica_Diagnostico_Consulta] FOREIGN KEY([id_consulta_clinica])
REFERENCES [dbo].[Consulta_Clinica] ([id_consulta_clinica])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Consulta_Clinica_Diagnostico] CHECK CONSTRAINT [FK_Consulta_Clinica_Diagnostico_Consulta]
GO
ALTER TABLE [dbo].[Consulta_Clinica_Sintoma]  WITH CHECK ADD  CONSTRAINT [FK_Consulta_Clinica_Sintoma_Consulta] FOREIGN KEY([id_consulta_clinica])
REFERENCES [dbo].[Consulta_Clinica] ([id_consulta_clinica])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Consulta_Clinica_Sintoma] CHECK CONSTRAINT [FK_Consulta_Clinica_Sintoma_Consulta]
GO
ALTER TABLE [dbo].[Consultorio_Estado_Operativo]  WITH CHECK ADD  CONSTRAINT [FK_ConsultorioEstadoOperativo_Consultorio] FOREIGN KEY([id_consultorio])
REFERENCES [dbo].[Consultorio] ([id_consultorio])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Consultorio_Estado_Operativo] CHECK CONSTRAINT [FK_ConsultorioEstadoOperativo_Consultorio]
GO
ALTER TABLE [dbo].[Consultorio_Estado_Operativo]  WITH CHECK ADD  CONSTRAINT [FK_ConsultorioEstadoOperativo_Usuario] FOREIGN KEY([actualizado_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Consultorio_Estado_Operativo] CHECK CONSTRAINT [FK_ConsultorioEstadoOperativo_Usuario]
GO
ALTER TABLE [dbo].[Consultorio_Historial]  WITH CHECK ADD  CONSTRAINT [FK_ConsultorioHistorial_Consultorio] FOREIGN KEY([id_consultorio])
REFERENCES [dbo].[Consultorio] ([id_consultorio])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Consultorio_Historial] CHECK CONSTRAINT [FK_ConsultorioHistorial_Consultorio]
GO
ALTER TABLE [dbo].[Consultorio_Historial]  WITH CHECK ADD  CONSTRAINT [FK_ConsultorioHistorial_EstadoAnterior] FOREIGN KEY([id_estado_anterior])
REFERENCES [dbo].[Estado_Consultorio] ([id_estado_consultorio])
GO
ALTER TABLE [dbo].[Consultorio_Historial] CHECK CONSTRAINT [FK_ConsultorioHistorial_EstadoAnterior]
GO
ALTER TABLE [dbo].[Consultorio_Historial]  WITH CHECK ADD  CONSTRAINT [FK_ConsultorioHistorial_EstadoNuevo] FOREIGN KEY([id_estado_nuevo])
REFERENCES [dbo].[Estado_Consultorio] ([id_estado_consultorio])
GO
ALTER TABLE [dbo].[Consultorio_Historial] CHECK CONSTRAINT [FK_ConsultorioHistorial_EstadoNuevo]
GO
ALTER TABLE [dbo].[Consultorio_Historial]  WITH CHECK ADD  CONSTRAINT [FK_ConsultorioHistorial_Usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Consultorio_Historial] CHECK CONSTRAINT [FK_ConsultorioHistorial_Usuario]
GO
ALTER TABLE [dbo].[Control_Postoperatorio]  WITH CHECK ADD  CONSTRAINT [FK_ControlPostop_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Control_Postoperatorio] CHECK CONSTRAINT [FK_ControlPostop_Cita]
GO
ALTER TABLE [dbo].[Credencial_Profesional]  WITH CHECK ADD  CONSTRAINT [FK_Credencial_Profesional_Profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Credencial_Profesional] CHECK CONSTRAINT [FK_Credencial_Profesional_Profesional]
GO
ALTER TABLE [dbo].[Credencial_Profesional]  WITH CHECK ADD  CONSTRAINT [FK_Credencial_Profesional_RevisadoPor] FOREIGN KEY([revisado_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[Credencial_Profesional] CHECK CONSTRAINT [FK_Credencial_Profesional_RevisadoPor]
GO
ALTER TABLE [dbo].[Credencial_Profesional]  WITH CHECK ADD  CONSTRAINT [FK_Credencial_Profesional_SubidoPor] FOREIGN KEY([subido_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Credencial_Profesional] CHECK CONSTRAINT [FK_Credencial_Profesional_SubidoPor]
GO
ALTER TABLE [dbo].[Detalle_Factura]  WITH CHECK ADD  CONSTRAINT [FK_DetalleFactura_Factura] FOREIGN KEY([id_factura])
REFERENCES [dbo].[Factura] ([id_factura])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Detalle_Factura] CHECK CONSTRAINT [FK_DetalleFactura_Factura]
GO
ALTER TABLE [dbo].[Detalle_Factura]  WITH CHECK ADD  CONSTRAINT [FK_DetalleFactura_Servicio] FOREIGN KEY([id_servicio])
REFERENCES [dbo].[Servicio] ([id_servicio])
GO
ALTER TABLE [dbo].[Detalle_Factura] CHECK CONSTRAINT [FK_DetalleFactura_Servicio]
GO
ALTER TABLE [dbo].[Documento_Clinico]  WITH CHECK ADD  CONSTRAINT [FK_DocClinico_Historia] FOREIGN KEY([id_historia])
REFERENCES [dbo].[Historia_Clinica] ([id_historia])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Documento_Clinico] CHECK CONSTRAINT [FK_DocClinico_Historia]
GO
ALTER TABLE [dbo].[Documento_Clinico]  WITH CHECK ADD  CONSTRAINT [FK_DocClinico_Usuario] FOREIGN KEY([subido_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Documento_Clinico] CHECK CONSTRAINT [FK_DocClinico_Usuario]
GO
ALTER TABLE [dbo].[Factura]  WITH CHECK ADD  CONSTRAINT [FK_Factura_AnuladaPor] FOREIGN KEY([anulada_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[Factura] CHECK CONSTRAINT [FK_Factura_AnuladaPor]
GO
ALTER TABLE [dbo].[Factura]  WITH CHECK ADD  CONSTRAINT [FK_Factura_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
GO
ALTER TABLE [dbo].[Factura] CHECK CONSTRAINT [FK_Factura_Cita]
GO
ALTER TABLE [dbo].[Factura]  WITH CHECK ADD  CONSTRAINT [FK_Factura_GeneradaPor] FOREIGN KEY([generada_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[Factura] CHECK CONSTRAINT [FK_Factura_GeneradaPor]
GO
ALTER TABLE [dbo].[Factura]  WITH CHECK ADD  CONSTRAINT [FK_Factura_Paciente] FOREIGN KEY([id_paciente])
REFERENCES [dbo].[Paciente] ([id_paciente])
GO
ALTER TABLE [dbo].[Factura] CHECK CONSTRAINT [FK_Factura_Paciente]
GO
ALTER TABLE [dbo].[Factura]  WITH CHECK ADD  CONSTRAINT [FK_Factura_Profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
GO
ALTER TABLE [dbo].[Factura] CHECK CONSTRAINT [FK_Factura_Profesional]
GO
ALTER TABLE [dbo].[Historia_Clinica]  WITH CHECK ADD  CONSTRAINT [FK_Historia_Clinica_Paciente_id_paciente] FOREIGN KEY([id_paciente])
REFERENCES [dbo].[Paciente] ([id_paciente])
GO
ALTER TABLE [dbo].[Historia_Clinica] CHECK CONSTRAINT [FK_Historia_Clinica_Paciente_id_paciente]
GO
ALTER TABLE [dbo].[Horario_Profesional]  WITH CHECK ADD  CONSTRAINT [FK_HP_Profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
GO
ALTER TABLE [dbo].[Horario_Profesional] CHECK CONSTRAINT [FK_HP_Profesional]
GO
ALTER TABLE [dbo].[Menu]  WITH CHECK ADD  CONSTRAINT [FK_Menu_Padre] FOREIGN KEY([id_menu_padre])
REFERENCES [dbo].[Menu] ([id_menu])
GO
ALTER TABLE [dbo].[Menu] CHECK CONSTRAINT [FK_Menu_Padre]
GO
ALTER TABLE [dbo].[Nota_Clinica]  WITH CHECK ADD  CONSTRAINT [FK_NC_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Nota_Clinica] CHECK CONSTRAINT [FK_NC_Cita]
GO
ALTER TABLE [dbo].[Nota_Clinica]  WITH CHECK ADD  CONSTRAINT [FK_Nota_Historia] FOREIGN KEY([id_historia])
REFERENCES [dbo].[Historia_Clinica] ([id_historia])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Nota_Clinica] CHECK CONSTRAINT [FK_Nota_Historia]
GO
ALTER TABLE [dbo].[Nota_Clinica]  WITH CHECK ADD  CONSTRAINT [FK_Nota_Profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Nota_Clinica] CHECK CONSTRAINT [FK_Nota_Profesional]
GO
ALTER TABLE [dbo].[Notificacion]  WITH CHECK ADD  CONSTRAINT [FK_Notificacion_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Notificacion] CHECK CONSTRAINT [FK_Notificacion_Cita]
GO
ALTER TABLE [dbo].[Notificacion]  WITH CHECK ADD  CONSTRAINT [FK_Notificacion_Paciente] FOREIGN KEY([id_paciente])
REFERENCES [dbo].[Paciente] ([id_paciente])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Notificacion] CHECK CONSTRAINT [FK_Notificacion_Paciente]
GO
ALTER TABLE [dbo].[Notificacion_Leida]  WITH CHECK ADD  CONSTRAINT [FK_NotificacionLeida_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Notificacion_Leida] CHECK CONSTRAINT [FK_NotificacionLeida_Cita]
GO
ALTER TABLE [dbo].[Notificacion_Leida]  WITH CHECK ADD  CONSTRAINT [FK_NotificacionLeida_Paciente] FOREIGN KEY([id_paciente])
REFERENCES [dbo].[Paciente] ([id_paciente])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Notificacion_Leida] CHECK CONSTRAINT [FK_NotificacionLeida_Paciente]
GO
ALTER TABLE [dbo].[Paciente]  WITH CHECK ADD  CONSTRAINT [FK_Paciente_ProfesionalAsignado] FOREIGN KEY([id_profesional_asignado])
REFERENCES [dbo].[Profesional] ([id_profesional])
GO
ALTER TABLE [dbo].[Paciente] CHECK CONSTRAINT [FK_Paciente_ProfesionalAsignado]
GO
ALTER TABLE [dbo].[Paciente]  WITH CHECK ADD  CONSTRAINT [FK_Paciente_Usuario_id_usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[Paciente] CHECK CONSTRAINT [FK_Paciente_Usuario_id_usuario]
GO
ALTER TABLE [dbo].[Pago]  WITH CHECK ADD  CONSTRAINT [FK_Pago_Factura] FOREIGN KEY([id_factura])
REFERENCES [dbo].[Factura] ([id_factura])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Pago] CHECK CONSTRAINT [FK_Pago_Factura]
GO
ALTER TABLE [dbo].[Pago]  WITH CHECK ADD  CONSTRAINT [FK_Pago_Usuario] FOREIGN KEY([registrado_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[Pago] CHECK CONSTRAINT [FK_Pago_Usuario]
GO
ALTER TABLE [dbo].[Pago_Factura]  WITH CHECK ADD  CONSTRAINT [FK_PagoFactura_Factura] FOREIGN KEY([id_factura])
REFERENCES [dbo].[Factura] ([id_factura])
GO
ALTER TABLE [dbo].[Pago_Factura] CHECK CONSTRAINT [FK_PagoFactura_Factura]
GO
ALTER TABLE [dbo].[Pago_Factura]  WITH CHECK ADD  CONSTRAINT [FK_PagoFactura_Usuario] FOREIGN KEY([registrado_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[Pago_Factura] CHECK CONSTRAINT [FK_PagoFactura_Usuario]
GO
ALTER TABLE [dbo].[Plan_Sesion]  WITH CHECK ADD  CONSTRAINT [FK_Plan_Sesion_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Plan_Sesion] CHECK CONSTRAINT [FK_Plan_Sesion_Cita]
GO
ALTER TABLE [dbo].[Plan_Sesion]  WITH CHECK ADD  CONSTRAINT [FK_Plan_Sesion_Nota] FOREIGN KEY([id_nota])
REFERENCES [dbo].[Nota_Clinica] ([id_nota])
GO
ALTER TABLE [dbo].[Plan_Sesion] CHECK CONSTRAINT [FK_Plan_Sesion_Nota]
GO
ALTER TABLE [dbo].[Plan_Sesion]  WITH CHECK ADD  CONSTRAINT [FK_Plan_Sesion_Plan] FOREIGN KEY([id_plan])
REFERENCES [dbo].[Plan_Tratamiento] ([id_plan])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Plan_Sesion] CHECK CONSTRAINT [FK_Plan_Sesion_Plan]
GO
ALTER TABLE [dbo].[Plan_Sesion]  WITH CHECK ADD  CONSTRAINT [FK_Plan_Sesion_ProfesionalAnula] FOREIGN KEY([id_profesional_anula])
REFERENCES [dbo].[Profesional] ([id_profesional])
GO
ALTER TABLE [dbo].[Plan_Sesion] CHECK CONSTRAINT [FK_Plan_Sesion_ProfesionalAnula]
GO
ALTER TABLE [dbo].[Plan_Sesion]  WITH CHECK ADD  CONSTRAINT [FK_Plan_Sesion_ProfesionalRegistro] FOREIGN KEY([id_profesional_registro])
REFERENCES [dbo].[Profesional] ([id_profesional])
GO
ALTER TABLE [dbo].[Plan_Sesion] CHECK CONSTRAINT [FK_Plan_Sesion_ProfesionalRegistro]
GO
ALTER TABLE [dbo].[Plan_Tratamiento]  WITH CHECK ADD  CONSTRAINT [FK_Plan_Tratamiento_Historia] FOREIGN KEY([id_historia])
REFERENCES [dbo].[Historia_Clinica] ([id_historia])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Plan_Tratamiento] CHECK CONSTRAINT [FK_Plan_Tratamiento_Historia]
GO
ALTER TABLE [dbo].[Plan_Tratamiento]  WITH CHECK ADD  CONSTRAINT [FK_Plan_Tratamiento_Profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Plan_Tratamiento] CHECK CONSTRAINT [FK_Plan_Tratamiento_Profesional]
GO
ALTER TABLE [dbo].[Plan_Tratamiento]  WITH CHECK ADD  CONSTRAINT [FK_Plan_Tratamiento_Servicio] FOREIGN KEY([id_servicio])
REFERENCES [dbo].[Servicio] ([id_servicio])
GO
ALTER TABLE [dbo].[Plan_Tratamiento] CHECK CONSTRAINT [FK_Plan_Tratamiento_Servicio]
GO
ALTER TABLE [dbo].[PQR]  WITH CHECK ADD  CONSTRAINT [FK_PQR_Paciente] FOREIGN KEY([id_paciente])
REFERENCES [dbo].[Paciente] ([id_paciente])
GO
ALTER TABLE [dbo].[PQR] CHECK CONSTRAINT [FK_PQR_Paciente]
GO
ALTER TABLE [dbo].[PQR]  WITH CHECK ADD  CONSTRAINT [FK_PQR_Usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[PQR] CHECK CONSTRAINT [FK_PQR_Usuario]
GO
ALTER TABLE [dbo].[Profesional]  WITH CHECK ADD  CONSTRAINT [FK_Profesional_Usuario_id_usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[Profesional] CHECK CONSTRAINT [FK_Profesional_Usuario_id_usuario]
GO
ALTER TABLE [dbo].[Profesional_Especialidad]  WITH CHECK ADD  CONSTRAINT [FK_Profesional_Especialidad_Especialidad_id_especialidad] FOREIGN KEY([id_especialidad])
REFERENCES [dbo].[Especialidad] ([id_especialidad])
GO
ALTER TABLE [dbo].[Profesional_Especialidad] CHECK CONSTRAINT [FK_Profesional_Especialidad_Especialidad_id_especialidad]
GO
ALTER TABLE [dbo].[Profesional_Especialidad]  WITH CHECK ADD  CONSTRAINT [FK_Profesional_Especialidad_Profesional_id_profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Profesional_Especialidad] CHECK CONSTRAINT [FK_Profesional_Especialidad_Profesional_id_profesional]
GO
ALTER TABLE [dbo].[Profesional_Servicio]  WITH CHECK ADD  CONSTRAINT [FK_PS_Profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
GO
ALTER TABLE [dbo].[Profesional_Servicio] CHECK CONSTRAINT [FK_PS_Profesional]
GO
ALTER TABLE [dbo].[Profesional_Servicio]  WITH CHECK ADD  CONSTRAINT [FK_PS_Servicio] FOREIGN KEY([id_servicio])
REFERENCES [dbo].[Servicio] ([id_servicio])
GO
ALTER TABLE [dbo].[Profesional_Servicio] CHECK CONSTRAINT [FK_PS_Servicio]
GO
ALTER TABLE [dbo].[Recordatorio_Cita]  WITH CHECK ADD  CONSTRAINT [FK_Recordatorio_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Recordatorio_Cita] CHECK CONSTRAINT [FK_Recordatorio_Cita]
GO
ALTER TABLE [dbo].[Registro_Odontograma]  WITH CHECK ADD  CONSTRAINT [FK_RO_Cita] FOREIGN KEY([id_cita])
REFERENCES [dbo].[Cita] ([id_cita])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Registro_Odontograma] CHECK CONSTRAINT [FK_RO_Cita]
GO
ALTER TABLE [dbo].[Registro_Odontograma]  WITH CHECK ADD  CONSTRAINT [FK_RO_Historia] FOREIGN KEY([id_historia])
REFERENCES [dbo].[Historia_Clinica] ([id_historia])
GO
ALTER TABLE [dbo].[Registro_Odontograma] CHECK CONSTRAINT [FK_RO_Historia]
GO
ALTER TABLE [dbo].[Registro_Odontograma]  WITH CHECK ADD  CONSTRAINT [FK_RO_Profesional] FOREIGN KEY([id_profesional])
REFERENCES [dbo].[Profesional] ([id_profesional])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Registro_Odontograma] CHECK CONSTRAINT [FK_RO_Profesional]
GO
ALTER TABLE [dbo].[Rol_Menu_Permiso]  WITH CHECK ADD  CONSTRAINT [FK_RMP_Menu] FOREIGN KEY([id_menu])
REFERENCES [dbo].[Menu] ([id_menu])
GO
ALTER TABLE [dbo].[Rol_Menu_Permiso] CHECK CONSTRAINT [FK_RMP_Menu]
GO
ALTER TABLE [dbo].[Rol_Menu_Permiso]  WITH CHECK ADD  CONSTRAINT [FK_RMP_Rol] FOREIGN KEY([id_rol])
REFERENCES [dbo].[Rol] ([id_rol])
GO
ALTER TABLE [dbo].[Rol_Menu_Permiso] CHECK CONSTRAINT [FK_RMP_Rol]
GO
ALTER TABLE [dbo].[Servicio]  WITH CHECK ADD  CONSTRAINT [FK_Servicio_Especialidad] FOREIGN KEY([id_especialidad])
REFERENCES [dbo].[Especialidad] ([id_especialidad])
GO
ALTER TABLE [dbo].[Servicio] CHECK CONSTRAINT [FK_Servicio_Especialidad]
GO
ALTER TABLE [dbo].[Ticket_Soporte]  WITH CHECK ADD  CONSTRAINT [FK_TicketSoporte_Atendido] FOREIGN KEY([atendido_por])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[Ticket_Soporte] CHECK CONSTRAINT [FK_TicketSoporte_Atendido]
GO
ALTER TABLE [dbo].[Ticket_Soporte]  WITH CHECK ADD  CONSTRAINT [FK_TicketSoporte_Usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
GO
ALTER TABLE [dbo].[Ticket_Soporte] CHECK CONSTRAINT [FK_TicketSoporte_Usuario]
GO
ALTER TABLE [dbo].[Usuario]  WITH CHECK ADD  CONSTRAINT [FK_Usuario_Rol_id_rol] FOREIGN KEY([id_rol])
REFERENCES [dbo].[Rol] ([id_rol])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Usuario] CHECK CONSTRAINT [FK_Usuario_Rol_id_rol]
GO
ALTER TABLE [dbo].[UsuariosPreferenciasConsultorio]  WITH CHECK ADD  CONSTRAINT [FK_UsuariosPreferenciasConsultorio_Consultorio] FOREIGN KEY([id_consultorio])
REFERENCES [dbo].[Consultorio] ([id_consultorio])
GO
ALTER TABLE [dbo].[UsuariosPreferenciasConsultorio] CHECK CONSTRAINT [FK_UsuariosPreferenciasConsultorio_Consultorio]
GO
ALTER TABLE [dbo].[UsuariosPreferenciasConsultorio]  WITH CHECK ADD  CONSTRAINT [FK_UsuariosPreferenciasConsultorio_Usuario] FOREIGN KEY([id_usuario])
REFERENCES [dbo].[Usuario] ([id_usuario])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UsuariosPreferenciasConsultorio] CHECK CONSTRAINT [FK_UsuariosPreferenciasConsultorio_Usuario]
GO
ALTER TABLE [dbo].[Alergia_Paciente]  WITH CHECK ADD CHECK  (([severidad]='grave' OR [severidad]='moderada' OR [severidad]='leve'))
GO
ALTER TABLE [dbo].[Alergia_Paciente]  WITH CHECK ADD CHECK  (([tipo]='otro' OR [tipo]='latex' OR [tipo]='ambiental' OR [tipo]='alimento' OR [tipo]='medicamento'))
GO
ALTER TABLE [dbo].[Auditoria]  WITH CHECK ADD  CONSTRAINT [CK_Auditoria_Accion] CHECK  (([accion]='SELECT' OR [accion]='DELETE' OR [accion]='UPDATE' OR [accion]='INSERT'))
GO
ALTER TABLE [dbo].[Auditoria] CHECK CONSTRAINT [CK_Auditoria_Accion]
GO
ALTER TABLE [dbo].[AuditoriaRecuperacion]  WITH CHECK ADD  CONSTRAINT [CK_AuditoriaRecuperacion_Accion] CHECK  (([accion]='envio_fallido' OR [accion]='rate_limit_excedido' OR [accion]='bloqueo_por_intentos' OR [accion]='password_restablecida' OR [accion]='codigo_fallido' OR [accion]='codigo_verificado' OR [accion]='solicitud'))
GO
ALTER TABLE [dbo].[AuditoriaRecuperacion] CHECK CONSTRAINT [CK_AuditoriaRecuperacion_Accion]
GO
ALTER TABLE [dbo].[Ausencia_Profesional]  WITH CHECK ADD CHECK  (([tipo]='otro' OR [tipo]='permiso' OR [tipo]='incapacidad' OR [tipo]='vacaciones'))
GO
ALTER TABLE [dbo].[Ausencia_Profesional]  WITH CHECK ADD  CONSTRAINT [CK_AusenciaProfesional_Rango] CHECK  (([fecha_fin]>=[fecha_inicio]))
GO
ALTER TABLE [dbo].[Ausencia_Profesional] CHECK CONSTRAINT [CK_AusenciaProfesional_Rango]
GO
ALTER TABLE [dbo].[Bloqueo_Profesional]  WITH CHECK ADD  CONSTRAINT [CK_BloqueoProfesional_Rango] CHECK  (([fecha_fin]>[fecha_inicio]))
GO
ALTER TABLE [dbo].[Bloqueo_Profesional] CHECK CONSTRAINT [CK_BloqueoProfesional_Rango]
GO
ALTER TABLE [dbo].[Cita]  WITH CHECK ADD  CONSTRAINT [CK_Cita_DuracionMinutos] CHECK  (([duracion_minutos]>(0) AND [duracion_minutos]<=(1440)))
GO
ALTER TABLE [dbo].[Cita] CHECK CONSTRAINT [CK_Cita_DuracionMinutos]
GO
ALTER TABLE [dbo].[Consultorio]  WITH CHECK ADD  CONSTRAINT [CK_Consultorio_Estado] CHECK  (([estado]='activo' OR [estado]='no_disponible' OR [estado]='no-disponible' OR [estado]='mantenimiento' OR [estado]='ocupado' OR [estado]='disponible'))
GO
ALTER TABLE [dbo].[Consultorio] CHECK CONSTRAINT [CK_Consultorio_Estado]
GO
ALTER TABLE [dbo].[Credencial_Profesional]  WITH CHECK ADD  CONSTRAINT [CK_Credencial_Profesional_Estado] CHECK  (([estado_revision]='rechazado' OR [estado_revision]='aprobado' OR [estado_revision]='pendiente'))
GO
ALTER TABLE [dbo].[Credencial_Profesional] CHECK CONSTRAINT [CK_Credencial_Profesional_Estado]
GO
ALTER TABLE [dbo].[Detalle_Factura]  WITH CHECK ADD CHECK  (([cantidad]>(0)))
GO
ALTER TABLE [dbo].[Horario_Profesional]  WITH CHECK ADD CHECK  (([dia_semana]='Domingo' OR [dia_semana]='Sabado' OR [dia_semana]='Viernes' OR [dia_semana]='Jueves' OR [dia_semana]='Miercoles' OR [dia_semana]='Martes' OR [dia_semana]='Lunes'))
GO
ALTER TABLE [dbo].[Horario_Profesional]  WITH CHECK ADD  CONSTRAINT [CK_HorarioProfesional_Rango] CHECK  (([hora_fin]>[hora_inicio]))
GO
ALTER TABLE [dbo].[Horario_Profesional] CHECK CONSTRAINT [CK_HorarioProfesional_Rango]
GO
ALTER TABLE [dbo].[Notificacion]  WITH CHECK ADD CHECK  (([canal]='whatsapp' OR [canal]='sms' OR [canal]='email' OR [canal]='interno'))
GO
ALTER TABLE [dbo].[Notificacion]  WITH CHECK ADD CHECK  (([estado]='cancelada' OR [estado]='fallida' OR [estado]='leida' OR [estado]='enviada' OR [estado]='pendiente'))
GO
ALTER TABLE [dbo].[Notificacion]  WITH CHECK ADD  CONSTRAINT [CK_Notificacion_tipo] CHECK  (([tipo]='pago' OR [tipo]='factura' OR [tipo]='alerta' OR [tipo]='mensaje' OR [tipo]='cancelacion' OR [tipo]='confirmacion' OR [tipo]='recordatorio'))
GO
ALTER TABLE [dbo].[Notificacion] CHECK CONSTRAINT [CK_Notificacion_tipo]
GO
ALTER TABLE [dbo].[Pago]  WITH CHECK ADD CHECK  (([metodo_pago]='otro' OR [metodo_pago]='transferencia' OR [metodo_pago]='tarjeta' OR [metodo_pago]='efectivo'))
GO
ALTER TABLE [dbo].[Pago]  WITH CHECK ADD CHECK  (([monto]>(0)))
GO
ALTER TABLE [dbo].[Pago_Factura]  WITH CHECK ADD CHECK  (([monto]>(0)))
GO
ALTER TABLE [dbo].[Plan_Sesion]  WITH CHECK ADD  CONSTRAINT [CK_Plan_Sesion_Anulacion] CHECK  (([anulada]=(0) AND [motivo_anulacion] IS NULL AND [anulada_en] IS NULL AND [id_profesional_anula] IS NULL OR [anulada]=(1) AND nullif(ltrim(rtrim([motivo_anulacion])),N'') IS NOT NULL AND [anulada_en] IS NOT NULL AND [id_profesional_anula] IS NOT NULL))
GO
ALTER TABLE [dbo].[Plan_Sesion] CHECK CONSTRAINT [CK_Plan_Sesion_Anulacion]
GO
ALTER TABLE [dbo].[Plan_Tratamiento]  WITH CHECK ADD  CONSTRAINT [CK_Plan_Tratamiento_Estado] CHECK  (([estado]=N'cancelado' OR [estado]=N'completado' OR [estado]=N'pausado' OR [estado]=N'en-curso' OR [estado]=N'aprobado' OR [estado]=N'propuesto'))
GO
ALTER TABLE [dbo].[Plan_Tratamiento] CHECK CONSTRAINT [CK_Plan_Tratamiento_Estado]
GO
ALTER TABLE [dbo].[Plan_Tratamiento]  WITH CHECK ADD  CONSTRAINT [CK_Plan_Tratamiento_NumeroFdi] CHECK  (([numero_fdi] IS NULL OR len([numero_fdi])=(2) AND (left([numero_fdi],(1))>=N'1' AND left([numero_fdi],(1))<=N'4') AND (right([numero_fdi],(1))>=N'1' AND right([numero_fdi],(1))<=N'8')))
GO
ALTER TABLE [dbo].[Plan_Tratamiento] CHECK CONSTRAINT [CK_Plan_Tratamiento_NumeroFdi]
GO
ALTER TABLE [dbo].[Plan_Tratamiento]  WITH CHECK ADD  CONSTRAINT [CK_Plan_Tratamiento_SesionesEstimadas] CHECK  (([sesiones_estimadas]>=(0)))
GO
ALTER TABLE [dbo].[Plan_Tratamiento] CHECK CONSTRAINT [CK_Plan_Tratamiento_SesionesEstimadas]
GO
ALTER TABLE [dbo].[Profesional]  WITH CHECK ADD  CONSTRAINT [CK_Profesional_Estado] CHECK  (([estado]='inactivo' OR [estado]='vacaciones' OR [estado]='activo'))
GO
ALTER TABLE [dbo].[Profesional] CHECK CONSTRAINT [CK_Profesional_Estado]
GO
ALTER TABLE [dbo].[Recordatorio_Cita]  WITH CHECK ADD CHECK  (([canal]='interno' OR [canal]='whatsapp' OR [canal]='sms' OR [canal]='email'))
GO
ALTER TABLE [dbo].[Recordatorio_Cita]  WITH CHECK ADD CHECK  (([estado]='cancelado' OR [estado]='fallido' OR [estado]='enviado' OR [estado]='pendiente'))
GO
ALTER TABLE [dbo].[Ticket_Soporte]  WITH CHECK ADD CHECK  (([categoria]='otro' OR [categoria]='solicitud' OR [categoria]='consulta' OR [categoria]='incidente'))
GO
ALTER TABLE [dbo].[Ticket_Soporte]  WITH CHECK ADD CHECK  (([estado]='cerrado' OR [estado]='resuelto' OR [estado]='en_proceso' OR [estado]='abierto'))
GO
ALTER TABLE [dbo].[Ticket_Soporte]  WITH CHECK ADD CHECK  (([modulo_afectado]='sistema' OR [modulo_afectado]='reportes' OR [modulo_afectado]='facturacion' OR [modulo_afectado]='pacientes' OR [modulo_afectado]='citas'))
GO
ALTER TABLE [dbo].[Ticket_Soporte]  WITH CHECK ADD CHECK  (([severidad]='alta' OR [severidad]='media' OR [severidad]='baja'))
GO
/****** Object:  StoredProcedure [dbo].[sp_CrearCita]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- ============================================================
-- 8) PROCEDIMIENTOS ALMACENADOS
-- Módulos: Gestión de Citas / Gestión de Profesionales
-- ============================================================

-- ────────────────────────────────────────────────────────────
-- sp_CrearCita
-- Crea una nueva cita validando:
--   • que el paciente exista y esté activo,
--   • que el profesional exista y esté activo,
--   • que la hora de inicio sea anterior a la de fin,
--   • que no exista solapamiento con otra cita del mismo
--     profesional en el mismo día,
--   • y sincroniza id_estado a partir del catálogo Estado_Cita.
--
-- Columnas reales de dbo.Cita que usa este SP:
--   id_cita, id_paciente, id_profesional, id_servicio,
--   fecha_hora, fecha, hora_inicio, hora_fin,
--   motivo_consulta, notas, id_consultorio, id_estado,
--   estado, fecha_creacion, creado_por
-- ────────────────────────────────────────────────────────────
CREATE   PROCEDURE [dbo].[sp_CrearCita]
    @id_paciente     INT,
    @id_profesional  INT,
    @id_servicio     INT          = NULL,
    @fecha           DATE,
    @hora_inicio     TIME,
    @hora_fin        TIME,
    @motivo_consulta VARCHAR(MAX) = NULL,
    @notas           VARCHAR(MAX) = NULL,
    @id_consultorio  INT          = NULL,
    @creado_por      INT          = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY

        -- 1. Validar paciente activo
        IF NOT EXISTS (
            SELECT 1
            FROM dbo.Paciente
            WHERE id_paciente = @id_paciente
              AND estado = 'activo'
        )
            THROW 50001, 'El paciente no existe o se encuentra inactivo.', 1;

        -- 2. Validar profesional activo
        IF NOT EXISTS (
            SELECT 1
            FROM dbo.Profesional
            WHERE id_profesional = @id_profesional
              AND estado = 'activo'
        )
            THROW 50002, 'El profesional no existe o no está activo.', 1;

        -- 3. Coherencia de horario
        IF @hora_inicio >= @hora_fin
            THROW 50003, 'La hora de inicio debe ser menor que la hora de finalización.', 1;

        -- 4. Evitar solapamiento de citas del profesional
        --    Compara intervalos [hora_inicio, hora_fin) del mismo día.
        --    Se excluyen citas canceladas y de no-asistencia porque
        --    esos slots quedan disponibles.
        IF EXISTS (
            SELECT 1
            FROM dbo.Cita
            WHERE id_profesional = @id_profesional
              AND fecha          = @fecha
              AND estado NOT IN ('Cancelada', 'No asistio')
              AND @hora_inicio   < hora_fin
              AND @hora_fin      > hora_inicio
        )
            THROW 50004, 'El profesional ya tiene una cita asignada en ese horario.', 1;

        -- 5. Resolver id_estado desde el catálogo
        DECLARE @id_estado_nuevo INT;
        SELECT TOP 1 @id_estado_nuevo = id_estado
        FROM dbo.Estado_Cita
        WHERE nombre_estado = 'Agendada';

        -- 6. Insertar la cita
        INSERT INTO dbo.Cita
        (
            id_paciente,
            id_profesional,
            id_servicio,
            fecha_hora,
            fecha,
            hora_inicio,
            hora_fin,
            motivo_consulta,
            notas,
            id_consultorio,
            id_estado,
            estado,
            fecha_creacion,
            creado_por
        )
        VALUES
        (
            @id_paciente,
            @id_profesional,
            @id_servicio,
            -- fecha_hora = combinación de @fecha + @hora_inicio (columna heredada)
            DATEADD(SECOND,
                    DATEDIFF(SECOND, CAST('00:00:00' AS TIME), @hora_inicio),
                    CAST(@fecha AS DATETIME)),
            @fecha,
            @hora_inicio,
            @hora_fin,
            @motivo_consulta,
            @notas,
            @id_consultorio,
            @id_estado_nuevo,
            'Agendada',
            GETDATE(),
            @creado_por
        );

        SELECT
            SCOPE_IDENTITY()               AS id_cita,
            'Cita creada correctamente.'   AS mensaje;

    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END;
GO
/****** Object:  StoredProcedure [dbo].[sp_ObtenerAgendaProfesional]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- ────────────────────────────────────────────────────────────
-- sp_ObtenerAgendaProfesional
-- Devuelve todas las citas de un profesional en una fecha dada,
-- ordenadas por hora de inicio.
-- Útil para la vista st-odo-02-agenda y el panel auxiliar.
-- ────────────────────────────────────────────────────────────
CREATE   PROCEDURE [dbo].[sp_ObtenerAgendaProfesional]
    @id_profesional INT,
    @fecha          DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.id_cita,
        c.fecha,
        c.hora_inicio,
        c.hora_fin,
        c.estado,
        c.motivo_consulta,
        c.notas,
        p.id_paciente,
        CONCAT(p.nombres, ' ', p.apellidos) AS paciente,
        p.documento,
        s.nombre                            AS servicio,
        co.nombre                           AS consultorio
    FROM  dbo.Cita       c
    INNER JOIN dbo.Paciente    p  ON p.id_paciente   = c.id_paciente
    LEFT  JOIN dbo.Servicio    s  ON s.id_servicio   = c.id_servicio
    LEFT  JOIN dbo.Consultorio co ON co.id_consultorio = c.id_consultorio
    WHERE c.id_profesional = @id_profesional
      AND c.fecha          = @fecha
    ORDER BY c.hora_inicio;
END;
GO
/****** Object:  StoredProcedure [dbo].[sp_ProfesionalServicio_ActualizarPrecio]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- sp_ProfesionalServicio_ActualizarPrecio
CREATE   PROCEDURE [dbo].[sp_ProfesionalServicio_ActualizarPrecio]
    @IdProfesional INT,
    @IdServicio INT,
    @PrecioPersonalizado DECIMAL(12,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @PrecioPersonalizado IS NOT NULL
       AND @PrecioPersonalizado <= 0
        THROW 51008, 'El precio personalizado debe ser mayor que cero.', 1;

    UPDATE dbo.Profesional_Servicio
    SET precio_personalizado = @PrecioPersonalizado
    WHERE id_profesional = @IdProfesional
      AND id_servicio = @IdServicio;

    IF @@ROWCOUNT = 0
        THROW 51009, 'No existe la asignación indicada.', 1;

    SELECT
        'Precio actualizado correctamente.' AS mensaje,
        precio_personalizado,
        'COP' AS moneda
    FROM dbo.Profesional_Servicio
    WHERE id_profesional = @IdProfesional
      AND id_servicio = @IdServicio;
END;

GO
/****** Object:  StoredProcedure [dbo].[sp_ProfesionalServicio_Asignar]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- sp_ProfesionalServicio_Asignar
CREATE   PROCEDURE [dbo].[sp_ProfesionalServicio_Asignar]
    @IdProfesional INT,
    @IdServicio INT,
    @PrecioPersonalizado DECIMAL(12,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @IdProfesional IS NULL OR @IdProfesional <= 0
        THROW 51001, 'El identificador del profesional no es válido.', 1;

    IF @IdServicio IS NULL OR @IdServicio <= 0
        THROW 51002, 'El identificador del servicio no es válido.', 1;

    IF @PrecioPersonalizado IS NOT NULL
       AND @PrecioPersonalizado <= 0
        THROW 51003, 'El precio personalizado debe ser mayor que cero.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.Profesional WITH (UPDLOCK, HOLDLOCK)
            WHERE id_profesional = @IdProfesional
              AND estado = 'activo'
        )
            THROW 51004, 'El profesional no existe o está inactivo.', 1;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.Servicio WITH (UPDLOCK, HOLDLOCK)
            WHERE id_servicio = @IdServicio
              AND estado = 'activo'
        )
            THROW 51005, 'El servicio no existe o está inactivo.', 1;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.Servicio s
            INNER JOIN dbo.Profesional_Especialidad pe
                ON pe.id_especialidad = s.id_especialidad
            WHERE s.id_servicio = @IdServicio
              AND pe.id_profesional = @IdProfesional
        )
            THROW 51006, 'El profesional no tiene la especialidad requerida para este servicio.', 1;

        IF EXISTS (
            SELECT 1
            FROM dbo.Profesional_Servicio
            WHERE id_profesional = @IdProfesional
              AND id_servicio = @IdServicio
        )
            THROW 51007, 'Esta asignación ya existe.', 1;

        INSERT INTO dbo.Profesional_Servicio (
            id_profesional,
            id_servicio,
            precio_personalizado,
            activo
        )
        VALUES (
            @IdProfesional,
            @IdServicio,
            @PrecioPersonalizado,
            1
        );

        COMMIT TRANSACTION;

        SELECT
            'Asignación creada correctamente.' AS mensaje,
            @IdProfesional AS id_profesional,
            @IdServicio AS id_servicio;

    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;

GO
/****** Object:  StoredProcedure [dbo].[sp_ProfesionalServicio_CambiarEstado]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- sp_ProfesionalServicio_CambiarEstado
CREATE   PROCEDURE [dbo].[sp_ProfesionalServicio_CambiarEstado]
    @IdProfesional INT,
    @IdServicio INT,
    @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.Profesional_Servicio WITH (UPDLOCK, HOLDLOCK)
            WHERE id_profesional = @IdProfesional
              AND id_servicio = @IdServicio
        )
            THROW 51010, 'No existe la asignación indicada.', 1;

        IF @Activo = 1
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.Profesional
                WHERE id_profesional = @IdProfesional
                  AND estado = 'activo'
            )
                THROW 51011, 'No se puede activar: el profesional está inactivo.', 1;

            IF NOT EXISTS (
                SELECT 1
                FROM dbo.Servicio
                WHERE id_servicio = @IdServicio
                  AND estado = 'activo'
            )
                THROW 51012, 'No se puede activar: el servicio está inactivo.', 1;

            IF NOT EXISTS (
                SELECT 1
                FROM dbo.Servicio s
                INNER JOIN dbo.Profesional_Especialidad pe
                    ON pe.id_especialidad = s.id_especialidad
                WHERE s.id_servicio = @IdServicio
                  AND pe.id_profesional = @IdProfesional
            )
                THROW 51013, 'El profesional no tiene la especialidad requerida.', 1;
        END;

        UPDATE dbo.Profesional_Servicio
        SET activo = @Activo
        WHERE id_profesional = @IdProfesional
          AND id_servicio = @IdServicio;

        COMMIT TRANSACTION;

        SELECT
            'Estado de asignación actualizado.' AS mensaje,
            @Activo AS activo;

    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;

GO
/****** Object:  StoredProcedure [dbo].[sp_ProfesionalServicio_ConsultarPorProfesional]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- sp_ProfesionalServicio_ConsultarPorProfesional
CREATE   PROCEDURE [dbo].[sp_ProfesionalServicio_ConsultarPorProfesional]
    @IdProfesional INT,
    @SoloActivos BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.id_profesional,
        p.nombres,
        p.apellidos,
        s.id_servicio,
        s.nombre AS servicio,
        s.categoria,
        e.nombre AS especialidad,
        s.precio AS precio_base,
        ps.precio_personalizado,
        COALESCE(ps.precio_personalizado, s.precio) AS precio_aplicado,
        'COP' AS moneda,
        CASE
            WHEN ps.precio_personalizado IS NULL
                THEN 'Precio base'
            ELSE 'Precio personalizado'
        END AS tipo_precio,
        s.duracion_minutos,
        ps.activo
    FROM dbo.Profesional_Servicio ps
    INNER JOIN dbo.Profesional p
        ON p.id_profesional = ps.id_profesional
    INNER JOIN dbo.Servicio s
        ON s.id_servicio = ps.id_servicio
    LEFT JOIN dbo.Especialidad e
        ON e.id_especialidad = s.id_especialidad
    WHERE ps.id_profesional = @IdProfesional
      AND (@SoloActivos = 0 OR ps.activo = 1)
    ORDER BY s.nombre;
END;

GO
/****** Object:  StoredProcedure [dbo].[sp_ProfesionalServicio_ConsultarPorServicio]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- sp_ProfesionalServicio_ConsultarPorServicio
CREATE   PROCEDURE [dbo].[sp_ProfesionalServicio_ConsultarPorServicio]
    @IdServicio INT,
    @SoloActivos BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.id_servicio,
        s.nombre AS servicio,
        p.id_profesional,
        p.nombres,
        p.apellidos,
        e.nombre AS especialidad,
        s.precio AS precio_base,
        ps.precio_personalizado,
        COALESCE(ps.precio_personalizado, s.precio) AS precio_aplicado,
        'COP' AS moneda,
        CASE
            WHEN ps.precio_personalizado IS NULL
                THEN 'Precio base'
            ELSE 'Precio personalizado'
        END AS tipo_precio,
        ps.activo AS asignacion_activa
    FROM dbo.Profesional_Servicio ps
    INNER JOIN dbo.Servicio s
        ON s.id_servicio = ps.id_servicio
    INNER JOIN dbo.Profesional p
        ON p.id_profesional = ps.id_profesional
    LEFT JOIN dbo.Especialidad e
        ON e.id_especialidad = s.id_especialidad
    WHERE ps.id_servicio = @IdServicio
      AND (@SoloActivos = 0 OR ps.activo = 1)
    ORDER BY p.apellidos, p.nombres;
END;

GO
/****** Object:  UserDefinedFunction [dbo].[fn_VerificarDisponibilidadProfesional]    Script Date: 12/11/2026 8:06:36 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE FUNCTION [dbo].[fn_VerificarDisponibilidadProfesional]
(
    @IdProfesional INT,
    @FechaHoraInicio DATETIME,
    @DuracionMinutos INT = 60,
    @IdCitaExcluir INT = NULL
)
RETURNS BIT
AS
BEGIN
    DECLARE @FechaHoraFin DATETIME = DATEADD(MINUTE, @DuracionMinutos, @FechaHoraInicio);
    DECLARE @Fecha DATE = CAST(@FechaHoraInicio AS DATE);

    -- Verificar que el profesional esté activo
    IF EXISTS (SELECT 1 FROM dbo.Profesional WHERE id_profesional = @IdProfesional AND estado <> 'activo')
        RETURN 0;

    -- Si el profesional tiene horarios configurados, la cita debe caber en uno.
    DECLARE @DiaSemana VARCHAR(12) = CASE ((DATEDIFF(DAY, '19000101', @Fecha) % 7 + 7) % 7)
        WHEN 0 THEN 'Lunes' WHEN 1 THEN 'Martes' WHEN 2 THEN 'Miercoles'
        WHEN 3 THEN 'Jueves' WHEN 4 THEN 'Viernes' WHEN 5 THEN 'Sabado' ELSE 'Domingo' END;
    IF EXISTS (SELECT 1 FROM dbo.Horario_Profesional WHERE id_profesional = @IdProfesional)
       AND NOT EXISTS (
           SELECT 1 FROM dbo.Horario_Profesional
           WHERE id_profesional = @IdProfesional AND activo = 1
             AND dia_semana = @DiaSemana
             AND hora_inicio <= CAST(@FechaHoraInicio AS TIME)
             AND hora_fin >= CAST(@FechaHoraFin AS TIME)
       )
        RETURN 0;

    -- Verificar ausencias registradas
    IF EXISTS (
        SELECT 1 FROM dbo.Ausencia_Profesional
        WHERE id_profesional = @IdProfesional
          AND fecha_inicio <= @Fecha
          AND fecha_fin >= @Fecha
    )
        RETURN 0;

    -- Verificar bloqueos de agenda
    IF EXISTS (
        SELECT 1 FROM dbo.Bloqueo_Profesional
        WHERE id_profesional = @IdProfesional
          AND fecha_inicio < @FechaHoraFin
          AND fecha_fin > @FechaHoraInicio
    )
        RETURN 0;

    -- Verificar citas solapadas existentes (no canceladas)
    IF EXISTS (
        SELECT 1 FROM dbo.Cita
        WHERE id_profesional = @IdProfesional
          AND (@IdCitaExcluir IS NULL OR id_cita <> @IdCitaExcluir)
          AND LOWER(ISNULL(estado, '')) NOT IN ('cancelada', 'cancelado')
          AND fecha_hora < @FechaHoraFin
          AND DATEADD(MINUTE, ISNULL(duracion_minutos, @DuracionMinutos), fecha_hora) > @FechaHoraInicio
    )
        RETURN 0;

    RETURN 1;
END
GO
/****** Object:  StoredProcedure [dbo].[sp_RegistrarCitaConValidacion]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[sp_RegistrarCitaConValidacion]
(
    @IdPaciente INT,
    @IdProfesional INT = NULL,
    @IdServicio INT = NULL,
    @IdConsultorio INT = NULL,
    @FechaHora DATETIME,
    @DuracionMinutos INT = 60,
    @Estado VARCHAR(30) = 'Agendada',
    @Notas VARCHAR(MAX) = NULL,
    @CreadoPor INT = NULL,
    @IdCitaCreada INT OUTPUT,
    @MensajeError VARCHAR(250) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @IdCitaCreada = 0;
    SET @MensajeError = '';

    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;

    IF @IdServicio IS NOT NULL
    BEGIN
        SELECT @DuracionMinutos = duracion_minutos
        FROM dbo.Servicio
        WHERE id_servicio = @IdServicio AND duracion_minutos > 0;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Paciente WHERE id_paciente = @IdPaciente AND estado = 'activo')
    BEGIN
        SET @MensajeError = 'El paciente especificado no existe o no se encuentra activo.';
        ROLLBACK TRANSACTION;
        RETURN -1;
    END

    IF @IdProfesional IS NOT NULL AND @IdProfesional > 0
    BEGIN
        IF dbo.fn_VerificarDisponibilidadProfesional(@IdProfesional, @FechaHora, @DuracionMinutos, NULL) = 0
        BEGIN
            SET @MensajeError = 'El profesional seleccionado no tiene disponibilidad en la fecha y horario solicitados.';
            ROLLBACK TRANSACTION;
            RETURN -2;
        END
    END

    IF @IdConsultorio IS NOT NULL AND EXISTS (
        SELECT 1 FROM dbo.Cita
        WHERE id_consultorio = @IdConsultorio
          AND estado NOT IN ('Cancelada', 'Cancelado', 'cancelada', 'cancelado')
          AND fecha_hora < DATEADD(MINUTE, @DuracionMinutos, @FechaHora)
          AND DATEADD(MINUTE, ISNULL(duracion_minutos, @DuracionMinutos), fecha_hora) > @FechaHora
    )
    BEGIN
        SET @MensajeError = 'El consultorio seleccionado ya está ocupado en ese horario.';
        ROLLBACK TRANSACTION;
        RETURN -3;
    END

    DECLARE @IdEstado INT = NULL;
    SELECT TOP 1 @IdEstado = id_estado FROM dbo.Estado_Cita WHERE LOWER(nombre_estado) = LOWER(@Estado);

    BEGIN TRY
        INSERT INTO dbo.Cita (
            id_paciente,
            id_profesional,
            id_servicio,
            id_consultorio,
            fecha_hora,
            duracion_minutos,
            estado,
            id_estado,
            notas,
            fecha_creacion,
            creado_por
        )
        VALUES (
            @IdPaciente,
            @IdProfesional,
            @IdServicio,
            @IdConsultorio,
            @FechaHora,
            @DuracionMinutos,
            @Estado,
            @IdEstado,
            @Notas,
            GETDATE(),
            @CreadoPor
        );

        SET @IdCitaCreada = SCOPE_IDENTITY();
        SET @MensajeError = 'Cita registrada exitosamente.';
        COMMIT TRANSACTION;
        RETURN 0;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @MensajeError = ERROR_MESSAGE();
        RETURN -99;
    END CATCH
END
GO
/****** Object:  StoredProcedure [dbo].[sp_ReporteCitas]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- ────────────────────────────────────────────────────────────
-- sp_ReporteCitas
-- Devuelve el conteo de citas por estado dentro de un rango de
-- fechas. Útil para el dashboard de administrador y reportes.
-- ────────────────────────────────────────────────────────────
CREATE   PROCEDURE [dbo].[sp_ReporteCitas]
    @fecha_inicio DATE,
    @fecha_fin    DATE
AS
BEGIN
    SET NOCOUNT ON;

    IF @fecha_inicio > @fecha_fin
        THROW 50005, 'La fecha de inicio no puede ser mayor que la fecha de fin.', 1;

    SELECT
        c.estado,
        COUNT(*)          AS cantidad
    FROM  dbo.Cita c
    WHERE c.fecha BETWEEN @fecha_inicio AND @fecha_fin
    GROUP BY c.estado
    ORDER BY cantidad DESC;
END;
GO
/****** Object:  StoredProcedure [dbo].[sp_Servicio_Actualizar]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- sp_Servicio_Actualizar (BORRADOR: mantiene especialidad obligatoria y categoría periodoncia)
CREATE   PROCEDURE [dbo].[sp_Servicio_Actualizar]
    @IdServicio INT,
    @Nombre NVARCHAR(150),
    @Descripcion NVARCHAR(500) = NULL,
    @Precio DECIMAL(12,2),
    @Categoria VARCHAR(50),
    @DuracionMinutos INT,
    @IdEspecialidad INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Nombre = LTRIM(RTRIM(@Nombre));
    SET @Descripcion = NULLIF(LTRIM(RTRIM(@Descripcion)), N'');
    SET @Categoria = LOWER(LTRIM(RTRIM(@Categoria)));

    IF @IdServicio IS NULL OR @IdServicio <= 0
        THROW 50002, 'El identificador del servicio no es válido.', 1;

    IF @Nombre IS NULL OR LEN(@Nombre) < 3 OR LEN(@Nombre) > 150
        THROW 50002, 'El nombre debe tener entre 3 y 150 caracteres.', 1;

    IF @Precio IS NULL OR @Precio <= 0
        THROW 50002, 'El precio debe ser mayor que $0 COP.', 1;

    IF @DuracionMinutos IS NULL OR @DuracionMinutos NOT BETWEEN 5 AND 480
        THROW 50002, 'La duración debe estar entre 5 y 480 minutos.', 1;

    IF @Categoria IS NULL OR @Categoria NOT IN (
        'prevencion','estetica','cirugia',
        'ortodoncia','endodoncia','general','periodoncia'
    )
        THROW 50002, 'La categoría del servicio no es válida.', 1;

    IF @IdEspecialidad IS NULL
       OR NOT EXISTS (
            SELECT 1
            FROM dbo.Especialidad
            WHERE id_especialidad = @IdEspecialidad
       )
        THROW 50002, 'Debe seleccionar una especialidad válida.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.Servicio WITH (UPDLOCK, HOLDLOCK)
            WHERE id_servicio = @IdServicio
        )
            THROW 50002, 'El servicio no existe.', 1;

        IF EXISTS (
            SELECT 1
            FROM dbo.Servicio WITH (UPDLOCK, HOLDLOCK)
            WHERE id_servicio <> @IdServicio
              AND LOWER(LTRIM(RTRIM(nombre))) = LOWER(@Nombre)
        )
            THROW 50001, 'Ya existe otro servicio con ese nombre.', 1;

        UPDATE dbo.Servicio
        SET nombre = @Nombre,
            descripcion = @Descripcion,
            precio = @Precio,
            categoria = @Categoria,
            duracion_minutos = @DuracionMinutos,
            id_especialidad = @IdEspecialidad
        WHERE id_servicio = @IdServicio;

        COMMIT TRANSACTION;

    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;

GO
/****** Object:  StoredProcedure [dbo].[sp_Servicio_CambiarEstado]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE   PROCEDURE [dbo].[sp_Servicio_CambiarEstado]
    @IdServicio INT,
    @Estado NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;

    SET @Estado = LOWER(LTRIM(RTRIM(@Estado)));

    IF @IdServicio IS NULL OR @IdServicio <= 0
    BEGIN
        ;THROW 50002, 'El identificador del servicio no es válido.', 1;
    END;

    IF @Estado IS NULL OR @Estado NOT IN ('activo', 'inactivo')
    BEGIN
        ;THROW 50002, 'El estado debe ser activo o inactivo.', 1;
    END;

    UPDATE dbo.Servicio
    SET estado = @Estado
    WHERE id_servicio = @IdServicio;

    IF @@ROWCOUNT = 0
    BEGIN
        ;THROW 50002, 'El servicio no existe.', 1;
    END;
END;
GO
/****** Object:  StoredProcedure [dbo].[sp_Servicio_Crear]    Script Date: 12/11/2026 8:06:46 p. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- sp_Servicio_Crear (BORRADOR: mantiene especialidad obligatoria y categoría periodoncia)
CREATE   PROCEDURE [dbo].[sp_Servicio_Crear]
    @Nombre NVARCHAR(150),
    @Descripcion NVARCHAR(500) = NULL,
    @Precio DECIMAL(12,2),
    @Categoria VARCHAR(50),
    @DuracionMinutos INT,
    @IdEspecialidad INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Nombre = LTRIM(RTRIM(@Nombre));
    SET @Descripcion = NULLIF(LTRIM(RTRIM(@Descripcion)), N'');
    SET @Categoria = LOWER(LTRIM(RTRIM(@Categoria)));

    IF @Nombre IS NULL OR LEN(@Nombre) < 3 OR LEN(@Nombre) > 150
        THROW 50002, 'El nombre debe tener entre 3 y 150 caracteres.', 1;

    IF @Precio IS NULL OR @Precio <= 0
        THROW 50002, 'El precio debe ser mayor que $0 COP.', 1;

    IF @DuracionMinutos IS NULL OR @DuracionMinutos NOT BETWEEN 5 AND 480
        THROW 50002, 'La duración debe estar entre 5 y 480 minutos.', 1;

    IF @Categoria IS NULL OR @Categoria NOT IN (
        'prevencion','estetica','cirugia',
        'ortodoncia','endodoncia','general','periodoncia'
    )
        THROW 50002, 'La categoría del servicio no es válida.', 1;

    IF @IdEspecialidad IS NULL
       OR NOT EXISTS (
            SELECT 1
            FROM dbo.Especialidad
            WHERE id_especialidad = @IdEspecialidad
       )
        THROW 50002, 'Debe seleccionar una especialidad válida.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (
            SELECT 1
            FROM dbo.Servicio WITH (UPDLOCK, HOLDLOCK)
            WHERE LOWER(LTRIM(RTRIM(nombre))) = LOWER(@Nombre)
        )
            THROW 50001, 'Ya existe otro servicio con ese nombre.', 1;

        INSERT INTO dbo.Servicio (
            nombre, descripcion, precio, estado,
            categoria, duracion_minutos, id_especialidad
        )
        VALUES (
            @Nombre, @Descripcion, @Precio, 'activo',
            @Categoria, @DuracionMinutos, @IdEspecialidad
        );

        DECLARE @IdServicio INT = CONVERT(INT, SCOPE_IDENTITY());

        COMMIT TRANSACTION;

        SELECT
            id_servicio AS IdServicio,
            nombre AS Nombre,
            descripcion AS Descripcion,
            precio AS Precio,
            estado AS Estado,
            categoria AS Categoria,
            duracion_minutos AS DuracionMinutos,
            id_especialidad AS IdEspecialidad
        FROM dbo.Servicio
        WHERE id_servicio = @IdServicio;

    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;

GO
IF OBJECT_ID(N'dbo.RegistroConsentimiento', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RegistroConsentimiento](
        [id_registro] [int] IDENTITY(1,1) NOT NULL,
        [id_usuario] [int] NOT NULL,
        [version_politica] [nvarchar](10) NOT NULL,
        [fecha_consentimiento] [datetime2](7) NOT NULL,
        [ip_origen] [nvarchar](45) NULL,
        [canal] [nvarchar](30) NOT NULL CONSTRAINT [DF_RegistroConsentimiento_Canal] DEFAULT (N'web-registro'),
        [finalidad] [nvarchar](255) NOT NULL CONSTRAINT [DF_RegistroConsentimiento_Finalidad] DEFAULT (N'atencion-clinica'),
        [accion] [nvarchar](20) NOT NULL CONSTRAINT [DF_RegistroConsentimiento_Accion] DEFAULT (N'otorgar'),
        [tipo_datos] [nvarchar](100) NOT NULL CONSTRAINT [DF_RegistroConsentimiento_TipoDatos] DEFAULT (N'datos-salud,datos-personales'),
        [responsable] [nvarchar](100) NOT NULL CONSTRAINT [DF_RegistroConsentimiento_Responsable] DEFAULT (N'DEMO-NO-VALIDADO'),
        CONSTRAINT [PK_RegistroConsentimiento] PRIMARY KEY CLUSTERED ([id_registro] ASC),
        CONSTRAINT [FK_RegistroConsentimiento_Usuario]
            FOREIGN KEY ([id_usuario]) REFERENCES [dbo].[Usuario]([id_usuario]) ON DELETE CASCADE
    );
END;
GO
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_RegistroConsentimiento_Usuario'
      AND [object_id] = OBJECT_ID(N'dbo.RegistroConsentimiento')
)
BEGIN
    CREATE INDEX [IX_RegistroConsentimiento_Usuario]
        ON [dbo].[RegistroConsentimiento]([id_usuario], [fecha_consentimiento] DESC);
END;
GO
IF OBJECT_ID(N'dbo.Reporte_Generado', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Reporte_Generado](
        [id_reporte] [int] IDENTITY(1,1) NOT NULL,
        [nombre] [nvarchar](120) NOT NULL,
        [categoria] [nvarchar](30) NOT NULL,
        [periodo] [char](7) NOT NULL,
        [notas] [nvarchar](1000) NULL,
        [fecha_generacion_utc] [datetime2](7) NOT NULL,
        [generado_por] [int] NULL,
        [datos_json] [nvarchar](max) NOT NULL,
        CONSTRAINT [PK_Reporte_Generado] PRIMARY KEY CLUSTERED ([id_reporte] ASC),
        CONSTRAINT [FK_Reporte_Generado_Usuario_generado_por]
            FOREIGN KEY ([generado_por]) REFERENCES [dbo].[Usuario]([id_usuario]) ON DELETE SET NULL
    );
END;
GO
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Reporte_Generado_categoria_fecha_generacion_utc'
      AND [object_id] = OBJECT_ID(N'dbo.Reporte_Generado')
)
BEGIN
    CREATE INDEX [IX_Reporte_Generado_categoria_fecha_generacion_utc]
        ON [dbo].[Reporte_Generado]([categoria], [fecha_generacion_utc]);
END;
GO
USE [master]
GO
ALTER DATABASE [SmileTrackDB] SET  READ_WRITE
GO
