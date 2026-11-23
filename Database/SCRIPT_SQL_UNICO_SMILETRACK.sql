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
	[id_paciente] [int] NULL,
	[id_profesional] [int] NULL,
	[fecha_servicio] [date] NULL,
	[modulo_software] [nvarchar](100) NULL,
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
SET IDENTITY_INSERT [dbo].[Auditoria] ON

INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (1, 1, N'Profesional', 10, N'INSERT', N'::1', NULL, N'{"IdProfesional":10,"IdUsuario":6,"Nombres":"Johan","Apellidos":"Santamaria","RegistroMedico":"RM-020","Estado":"activo","Correo":"johanmauricio2007@gmail.com","Rol":"Profesional","IdEspecialidad":3}', N'Profesional creado con cuenta de acceso. IdProfesional=10', CAST(N'2026-08-28T18:32:15.317' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (2, 1, N'Profesional', 10, N'UPDATE', N'::1', NULL, N'{"IdProfesional":10,"IdUsuario":6,"Nombres":"Johan","Apellidos":"Santamaria","RegistroMedico":"RM-020","Estado":"Inactivo","Correo":"johanmauricio2007@gmail.com","Rol":"Profesional","IdEspecialidad":3}', N'Profesional actualizado y cuenta de acceso sincronizada. IdProfesional=10', CAST(N'2026-08-28T18:46:51.763' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (3, 1, N'Cita', 26, N'INSERT', N'::1', NULL, NULL, N'Cita guardada desde Agenda.', CAST(N'2026-09-07T17:25:20.053' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (4, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:41:57.110' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (5, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:41:57.893' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (6, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:41:58.410' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (7, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:42:00.193' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (8, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:00.923' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (9, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:01.547' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (10, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:02.043' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (11, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:02.690' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (12, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:42:29.730' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (13, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:30.177' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (14, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:30.393' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (15, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:30.760' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (16, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:32.207' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (17, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:32.537' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (18, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:32.743' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (19, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:32.923' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (20, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:33.200' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (21, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:42:37.570' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (22, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:38.053' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (23, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:42:44.627' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (24, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:44:02.243' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (25, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:44:02.747' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (26, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:44:02.947' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (27, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:44:03.373' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (28, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:44:04.147' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (29, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:44:05.027' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (30, 1, N'Usuario', 3, N'UPDATE', N'::1', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:44:05.717' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (31, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:44:06.347' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (32, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:44:07.227' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (33, 1, N'Usuario', 3, N'UPDATE', N'::1', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:44:07.670' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (34, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:44:20.370' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (35, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:48:55.847' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (36, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:48:56.520' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (37, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:48:57.697' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (38, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:48:58.777' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (39, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:48:59.613' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (40, 1, N'Usuario', 3, N'UPDATE', N'::1', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:49:00.677' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (41, 1, N'Usuario', 4, N'UPDATE', N'::1', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:49:01.683' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (42, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:49:02.723' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (43, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:49:14.553' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (44, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:49:15.163' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (45, 1, N'Usuario', 4, N'UPDATE', N'::1', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:49:21.120' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (46, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:51:28.687' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (47, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:51:29.303' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (48, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:55:57.633' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (49, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:55:58.263' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (50, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:55:58.767' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (51, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:00.117' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (52, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:00.753' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (53, 1, N'Usuario', 4, N'UPDATE', N'::1', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:56:02.977' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (54, 1, N'Usuario', 4, N'UPDATE', N'::1', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:03.770' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (55, 1, N'Usuario', 4, N'UPDATE', N'::1', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:04.353' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (56, 1, N'Usuario', 4, N'UPDATE', N'::1', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:04.780' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (57, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T21:56:05.837' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (58, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:06.617' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (59, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:06.853' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (60, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:07.047' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (61, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:07.230' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (62, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:07.443' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (63, 1, N'Usuario', 3, N'UPDATE', N'::1', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:11.010' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (64, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:11.487' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (65, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:12.057' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (66, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T21:56:13.177' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (67, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T22:07:23.173' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (68, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:23.603' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (69, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:23.870' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (70, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:24.107' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (71, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:24.300' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (72, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:24.467' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (73, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:24.643' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (74, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:24.833' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (75, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:25.033' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (76, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T22:07:25.913' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (77, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:26.090' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (78, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:26.240' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (79, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:26.450' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (80, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:26.660' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (81, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:26.897' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (82, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T22:07:31.677' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (83, 1, N'Usuario', 4, N'UPDATE', N'::1', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T22:07:32.003' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (84, 1, N'Usuario', 3, N'UPDATE', N'::1', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-14T22:07:32.587' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (85, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:33.610' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (86, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-14T22:07:34.823' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (87, 1, N'Profesional', 10, N'DELETE', N'::1', N'{"IdProfesional":10,"RegistroMedico":"RM-020","EstadoAnterior":"Inactivo"}', N'{"Estado":"inactivo"}', N'Baja lógica vía API REST. IdProfesional=10. EstadoAnterior=''Inactivo''.', CAST(N'2026-09-14T22:18:29.840' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (88, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-15T17:53:18.297' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (89, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T17:53:19.053' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (90, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T17:53:19.550' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (91, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T17:53:19.757' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (92, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T17:53:19.987' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (93, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T17:53:20.163' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (94, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T17:53:20.907' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (95, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T17:53:21.333' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (96, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T17:53:21.540' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (97, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T17:53:22.140' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (98, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-15T18:14:41.897' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (99, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:14:42.843' AS DateTime))
GO
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (100, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:14:43.433' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (101, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-15T18:14:44.613' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (102, 1, N'Usuario', 3, N'UPDATE', N'::1', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:14:45.460' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (103, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:14:55.007' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (104, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:14:56.610' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (105, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:01.733' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (106, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:01.957' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (107, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:02.150' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (108, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:02.337' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (109, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:02.510' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (110, 1, N'Usuario', 3, N'UPDATE', N'::1', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:16.350' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (111, 1, N'Usuario', 3, N'UPDATE', N'::1', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:16.837' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (112, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:22.847' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (113, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:23.737' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (114, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:24.193' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (115, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:24.463' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (116, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:24.710' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (117, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:24.960' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (118, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:25.767' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (119, 1, N'Usuario', 3, N'UPDATE', N'::1', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":3,"Nombre":"Profesional","Apellidos":"Prueba","Correo":"prof@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:26.277' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (120, 1, N'Usuario', 4, N'UPDATE', N'::1', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":4,"Nombre":"Recepcionista","Apellidos":"Prueba","Correo":"recep@smiletrack.co","IdRol":4,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:27.640' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (121, 1, N'Usuario', 6, N'UPDATE', N'::1', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":6,"Nombre":"Johan","Apellidos":"Santamaria","Correo":"johanmauricio2007@gmail.com","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:28.037' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (122, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:38.213' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (123, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:40.130' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (124, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:40.420' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (125, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:40.607' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (126, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:40.793' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (127, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:40.987' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (128, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:41.177' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (129, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:41.377' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (130, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:41.567' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (131, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:41.773' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (132, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:42.037' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (133, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:42.197' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (134, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:42.420' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (135, 1, N'Usuario', 2, N'UPDATE', N'::1', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":2,"Nombre":"Paciente","Apellidos":"Prueba","Correo":"pac@smiletrack.co","IdRol":3,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:15:42.670' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (136, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-15T18:16:45.380' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (137, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:16:46.423' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (138, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:16:46.997' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (139, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:16:47.293' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (140, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:16:47.477' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (141, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:16:47.687' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (142, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:16:47.890' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (143, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:16:48.093' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (144, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:16:48.293' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (145, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:16:48.497' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (146, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-15T18:16:48.693' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (147, 1, N'Profesional', 6, N'DELETE', N'::1', N'{"IdProfesional":6,"RegistroMedico":"RM-005","EstadoAnterior":"activo"}', N'{"Estado":"inactivo"}', N'Baja lógica vía API REST. IdProfesional=6. EstadoAnterior=''activo''.', CAST(N'2026-09-16T22:31:54.863' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (148, 1, N'Profesional', 6, N'UPDATE', N'::1', N'{"Estado":"inactivo"}', N'{"Estado":"activo"}', N'Cambio de estado vía API REST. IdProfesional=6. De ''inactivo'' a ''activo''.', CAST(N'2026-09-22T00:35:38.087' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (149, 1, N'Profesional', 10, N'UPDATE', N'::1', N'{"Estado":"inactivo"}', N'{"Estado":"activo"}', N'Cambio de estado vía API REST. IdProfesional=10. De ''inactivo'' a ''activo''.', CAST(N'2026-09-22T00:35:49.213' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (150, 1, N'Profesional', 6, N'DELETE', N'::1', N'{"IdProfesional":6,"RegistroMedico":"RM-005","EstadoAnterior":"activo"}', N'{"Estado":"inactivo"}', N'Baja lógica vía API REST. IdProfesional=6. EstadoAnterior=''activo''.', CAST(N'2026-09-22T00:37:32.463' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (151, 1, N'Profesional', 6, N'UPDATE', N'::1', N'{"Estado":"inactivo"}', N'{"Estado":"activo"}', N'Cambio de estado vía API REST. IdProfesional=6. De ''inactivo'' a ''activo''.', CAST(N'2026-09-22T00:37:40.957' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (152, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-22T20:26:40.627' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (153, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:26:41.503' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (154, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:26:41.997' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (155, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:26:42.357' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (156, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:26:42.587' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (157, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-22T20:28:34.233' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (158, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:34.600' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (159, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:34.790' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (160, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:34.983' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (161, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:35.173' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (162, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:35.343' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (163, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:35.550' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (164, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:35.840' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (165, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:36.603' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (166, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:36.790' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (167, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:37.000' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (168, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:39.563' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (169, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:39.767' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (170, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:39.970' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (171, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:40.163' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (172, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:40.377' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (173, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:40.603' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (174, 1, N'Usuario', 8, N'UPDATE', N'::1', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":8,"Nombre":"Carolina","Apellidos":"P\u00E9rez R\u00EDos","Correo":"dra.perez@smiletrack.co","IdRol":2,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:40.887' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (175, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'Estado cambiado a inactivo por API.', CAST(N'2026-09-22T20:28:47.477' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (176, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"inactivo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:48.050' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (177, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:48.237' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (178, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:48.410' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (179, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:48.590' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (180, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:48.760' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (181, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:48.930' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (182, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:50.977' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (183, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:51.267' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (184, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:51.493' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (185, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:51.700' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (186, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:28:51.987' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (187, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:29:31.407' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (188, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:29:31.690' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (189, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:29:31.867' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (190, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:29:32.057' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (191, 1, N'Usuario', 5, N'UPDATE', N'::1', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'{"IdUsuario":5,"Nombre":"Auxiliar","Apellidos":"Prueba","Correo":"aux@smiletrack.co","IdRol":5,"Estado":"activo","IntentosFallidos":0}', N'Estado cambiado a activo por API.', CAST(N'2026-09-22T20:29:32.263' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (192, 4, N'Cita', 31, N'INSERT', N'::1', NULL, NULL, N'Cita guardada desde Agenda.', CAST(N'2026-09-22T17:17:49.020' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (193, 4, N'Cita', 30, N'UPDATE', N'::1', NULL, NULL, N'Cita guardada desde Agenda.', CAST(N'2026-09-22T17:19:08.767' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (194, 2, N'Cita', 32, N'INSERT', N'::1', NULL, NULL, N'Solicitud de cita creada por paciente.', CAST(N'2026-09-23T10:37:43.963' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (195, 1, N'Profesional', 12, N'INSERT', N'::1', NULL, N'{"IdProfesional":12,"IdUsuario":9,"Nombres":"Nestor","Apellidos":"Monta\u00F1o","RegistroMedico":"RM-023-26","Estado":"activo","Correo":"nestormonta\u00F1o@smiletrack.co","Rol":"Profesional","IdEspecialidad":2}', N'Profesional creado vía API REST. IdProfesional=12', CAST(N'2026-09-23T16:07:07.877' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (196, 1, N'Profesional', 12, N'DELETE', N'::1', N'{"IdProfesional":12,"RegistroMedico":"RM-023-26","EstadoAnterior":"activo"}', N'{"Estado":"inactivo"}', N'Baja lógica vía API REST. IdProfesional=12. EstadoAnterior=''activo''.', CAST(N'2026-09-23T16:11:25.600' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (197, 4, N'Cita', 33, N'UPDATE', N'::1', NULL, NULL, N'Cita guardada desde Agenda.', CAST(N'2026-09-23T12:00:04.113' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (198, 4, N'Cita', 33, N'UPDATE', N'::1', NULL, NULL, N'Cita guardada desde Agenda.', CAST(N'2026-09-23T12:00:21.087' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (199, 4, N'Cita', 34, N'INSERT', N'::1', NULL, NULL, N'Cita guardada desde Agenda.', CAST(N'2026-09-23T12:01:35.543' AS DateTime))
GO
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (200, 4, N'Factura', 7, N'INSERT', NULL, NULL, NULL, N'Factura FAC-2026-00004 creada desde cita 17.', CAST(N'2026-10-27T17:15:25.707' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (201, 4, N'Factura', 2, N'INSERT', NULL, NULL, NULL, N'Se enviaron 1 recordatorio(s) de pago a pacientes con facturas vencidas.', CAST(N'2026-10-28T16:46:48.873' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (202, 4, N'Factura', 2, N'INSERT', NULL, NULL, NULL, N'Se enviaron 1 recordatorio(s) de pago a pacientes con facturas vencidas.', CAST(N'2026-10-29T17:18:13.943' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (203, 4, N'Factura', 8, N'INSERT', NULL, NULL, NULL, N'Factura FAC-2026-00005 creada con pago inicial.', CAST(N'2026-10-29T17:49:03.733' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (204, 4, N'Cita', 35, N'INSERT', N'::1', NULL, NULL, N'Cita guardada desde Agenda.', CAST(N'2026-11-03T16:12:30.463' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (205, 4, N'Cita', 35, N'UPDATE', N'::1', NULL, NULL, N'Cita guardada desde Agenda.', CAST(N'2026-11-03T16:13:19.800' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (206, 2, N'Cita', 36, N'INSERT', N'::1', NULL, NULL, N'Solicitud de cita creada por paciente.', CAST(N'2026-11-04T17:06:09.643' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (207, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-10T21:24:08.283' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (208, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:24:13.197' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (209, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:24:13.843' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (210, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-10T21:40:51.983' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (211, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el odontograma MVC.', CAST(N'2026-11-10T21:41:39.610' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (212, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:46:02.997' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (213, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:46:03.113' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (214, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:46:16.157' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (215, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:46:16.237' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (216, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-10T21:47:10.840' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (217, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:47:13.460' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (218, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:47:13.790' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (219, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:47:42.037' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (220, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:47:42.147' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (221, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:47:43.480' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (222, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:47:43.597' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (223, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:47:45.990' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (224, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:47:46.087' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (225, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Exportación PDF de historia clínica.', CAST(N'2026-11-10T21:49:42.423' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (226, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:54:19.533' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (227, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:54:19.660' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (228, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:54:20.323' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (229, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:54:20.423' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (230, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:54:22.200' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (231, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:54:22.270' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (232, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:54:29.747' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (233, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:54:29.853' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (234, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:54:30.090' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (235, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:54:30.207' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (236, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-10T21:54:30.260' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (237, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-10T21:54:30.320' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (238, 1, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde administración.', CAST(N'2026-11-11T20:53:07.100' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (239, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-11T21:39:09.110' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (240, 3, N'Plan_Tratamiento', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta del seguimiento de tratamientos.', CAST(N'2026-11-11T21:40:22.097' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (241, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-11T21:40:29.493' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (242, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-11T22:00:12.180' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (243, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-11T22:00:12.900' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (244, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-11T22:00:15.863' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (245, 3, N'Plan_Tratamiento', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta del seguimiento de tratamientos.', CAST(N'2026-11-11T22:01:17.830' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (246, 3, N'Plan_Tratamiento', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta del seguimiento de tratamientos.', CAST(N'2026-11-11T22:02:42.647' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (247, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-11T22:08:29.893' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (248, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-11T22:10:03.127' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (249, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-11T22:10:29.827' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (250, 1, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde administración.', CAST(N'2026-11-12T19:00:24.780' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (251, 1, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde administración.', CAST(N'2026-11-12T20:47:58.277' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (252, 1, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde administración.', CAST(N'2026-11-12T20:48:06.130' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (253, 1, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde administración.', CAST(N'2026-11-12T20:49:04.793' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (254, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T20:49:19.637' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (255, 1, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde administración.', CAST(N'2026-11-12T20:50:13.007' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (256, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T20:52:23.130' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (257, 1, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde administración.', CAST(N'2026-11-12T21:51:57.943' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (258, 1, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde administración.', CAST(N'2026-11-12T21:51:58.733' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (259, 2, N'Historia_Clinica', 5, N'SELECT', N'::1', NULL, NULL, N'Consulta del historial clínico propio desde el portal de paciente.', CAST(N'2026-11-12T22:02:01.960' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (260, 2, N'Historia_Clinica', 5, N'SELECT', N'::1', NULL, NULL, N'Consulta del historial clínico propio desde el portal de paciente.', CAST(N'2026-11-12T22:02:24.757' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (261, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T22:05:52.380' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (262, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T22:06:34.860' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (263, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T22:06:34.723' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (264, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T22:06:40.863' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (265, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T22:13:37.787' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (266, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T22:13:42.847' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (267, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T22:13:43.200' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (268, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T22:13:57.460' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (269, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T22:15:26.510' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (270, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-12T22:19:17.863' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (271, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-12T22:19:22.643' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (272, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-12T22:19:22.870' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (273, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el odontograma MVC.', CAST(N'2026-11-12T22:24:22.953' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (274, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-12T22:26:14.680' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (275, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-12T22:26:14.807' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (276, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el odontograma MVC.', CAST(N'2026-11-12T22:26:26.697' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (277, 3, N'Consulta_Clinica', 1, N'SELECT', N'::1', NULL, NULL, N'Consulta clínica estructurada consultada desde el historial MVC.', CAST(N'2026-11-12T22:26:33.320' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (278, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el historial MVC.', CAST(N'2026-11-12T22:26:33.473' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (279, 3, N'Historia_Clinica', 17, N'SELECT', N'::1', NULL, NULL, N'Consulta de historia clínica desde el odontograma MVC.', CAST(N'2026-11-12T22:26:35.150' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (280, 1, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde administración.', CAST(N'2026-11-12T22:28:52.917' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (281, 3, N'Plan_Tratamiento', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta del seguimiento de tratamientos.', CAST(N'2026-11-12T22:33:56.903' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (282, 4, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del historial clínico desde recepción.', CAST(N'2026-11-12T22:36:37.180' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (283, 3, N'Historia_Clinica', NULL, N'SELECT', N'::1', NULL, NULL, N'Consulta paginada del listado de pacientes clínicos del profesional.', CAST(N'2026-11-12T22:46:27.927' AS DateTime))
INSERT [dbo].[Auditoria] ([id_auditoria], [id_usuario], [tabla_afectada], [id_registro], [accion], [ip_origen], [datos_anteriores], [datos_nuevos], [descripcion], [fecha]) VALUES (284, 1, N'Profesional', 1, N'UPDATE', N'::1', N'{"Estado":"inactivo"}', N'{"Estado":"activo"}', N'Cambio de estado vía API REST. IdProfesional=1. De ''inactivo'' a ''activo''.', CAST(N'2026-11-12T23:03:55.343' AS DateTime))
SET IDENTITY_INSERT [dbo].[Auditoria] OFF
GO
SET IDENTITY_INSERT [dbo].[AuditoriaRecuperacion] ON

INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (4, 1, N'admin@smiletrack.co', N'solicitud', N'::1', CAST(N'2026-08-04T22:13:52.847' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (5, 1, N'admin@smiletrack.co', N'solicitud', N'::1', CAST(N'2026-08-04T22:16:13.073' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (6, 1, N'admin@smiletrack.co', N'solicitud', N'::1', CAST(N'2026-08-10T18:18:01.737' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (8, NULL, N'smiletrackodontologia@gmail.com', N'solicitud', N'::1', CAST(N'2026-08-11T18:43:04.460' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (9, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:15:34.820' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (10, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:15:35.220' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (11, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:15:35.423' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (12, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:15:35.623' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (13, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:15:35.843' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (14, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:15:36.007' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (15, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:15:36.177' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (16, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:15:36.327' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (17, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:15:41.893' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (18, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:15:43.073' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (19, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:23:44.263' AS DateTime))
INSERT [dbo].[AuditoriaRecuperacion] ([id_auditoria], [id_usuario], [correo_solicitado], [accion], [ip_origen], [fecha]) VALUES (20, 7, N'samuel1050torres@gmail.com', N'envio_fallido', N'::1', CAST(N'2026-09-22T22:23:45.810' AS DateTime))
SET IDENTITY_INSERT [dbo].[AuditoriaRecuperacion] OFF
GO
SET IDENTITY_INSERT [dbo].[Cita] ON

INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (1, 1, 3, 5, CAST(N'2026-08-31T14:30:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.630' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (2, 2, 6, 10, CAST(N'2026-06-08T15:30:00.0000000' AS DateTime2), N'Atendida', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 4, CAST(N'2026-08-04T22:08:36.637' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (3, 3, 9, 3, CAST(N'2026-08-21T12:00:00.0000000' AS DateTime2), N'Cancelada', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 5, CAST(N'2026-08-04T22:08:36.640' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (4, 4, 3, 8, CAST(N'2026-07-02T11:30:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.643' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (5, 5, 6, 1, CAST(N'2026-08-20T14:00:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.647' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (6, 6, 9, 6, CAST(N'2026-07-19T14:00:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.650' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (7, 7, 3, 11, CAST(N'2026-08-07T15:30:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.653' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (8, 8, 6, 4, CAST(N'2026-06-09T10:30:00.0000000' AS DateTime2), N'Atendida', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 4, CAST(N'2026-08-04T22:08:36.657' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (9, 9, 9, 9, CAST(N'2026-08-28T14:00:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.660' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (10, 10, 3, 2, CAST(N'2026-07-20T08:30:00.0000000' AS DateTime2), N'Cancelada', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 5, CAST(N'2026-08-04T22:08:36.663' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (11, 11, 6, 7, CAST(N'2026-08-11T11:00:00.0000000' AS DateTime2), N'Cancelada', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 5, CAST(N'2026-08-04T22:08:36.667' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (12, 12, 9, 12, CAST(N'2026-07-29T10:30:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.670' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (13, 13, 3, 5, CAST(N'2026-09-13T15:00:00.0000000' AS DateTime2), N'Cancelada', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 5, CAST(N'2026-08-04T22:08:36.670' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (14, 14, 6, 10, CAST(N'2026-07-12T13:30:00.0000000' AS DateTime2), N'Cancelada', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 5, CAST(N'2026-08-04T22:08:36.673' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (15, 15, 9, 3, CAST(N'2026-08-28T13:00:00.0000000' AS DateTime2), N'Cancelada', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 5, CAST(N'2026-08-04T22:08:36.680' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (16, 16, 3, 8, CAST(N'2026-06-13T17:00:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.687' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (17, 1, 6, 1, CAST(N'2026-09-15T14:00:00.0000000' AS DateTime2), N'Atendida', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 4, CAST(N'2026-08-04T22:08:36.690' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (18, 2, 9, 6, CAST(N'2026-07-22T13:30:00.0000000' AS DateTime2), N'Atendida', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 4, CAST(N'2026-08-04T22:08:36.690' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (19, 3, 3, 11, CAST(N'2026-09-09T15:30:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.697' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (20, 4, 6, 4, CAST(N'2026-07-04T17:30:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.700' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (21, 5, 9, 9, CAST(N'2026-08-06T09:00:00.0000000' AS DateTime2), N'Cancelada', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 5, CAST(N'2026-08-04T22:08:36.707' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (22, 6, 3, 2, CAST(N'2026-07-21T14:00:00.0000000' AS DateTime2), N'Atendida', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 4, CAST(N'2026-08-04T22:08:36.710' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (23, 7, 6, 7, CAST(N'2026-08-18T14:00:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.713' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (24, 8, 9, 12, CAST(N'2026-07-01T16:30:00.0000000' AS DateTime2), N'Atendida', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 4, CAST(N'2026-08-04T22:08:36.720' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (25, 9, 3, 5, CAST(N'2026-08-13T16:30:00.0000000' AS DateTime2), N'No asistio', N'Registro ficticio generado para pruebas.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, CAST(N'2026-08-04T22:08:36.723' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (26, 11, 6, 2, CAST(N'2026-09-08T09:00:00.0000000' AS DateTime2), N'No asistio', N'Validación E2E desde navegador', NULL, NULL, NULL, NULL, NULL, NULL, 1, 6, CAST(N'2026-09-07T22:25:20.157' AS DateTime), NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (30, 17, 10, 1, CAST(N'2026-09-25T15:00:00.0000000' AS DateTime2), N'No asistio', N'ddfdfdfdfdfdf', NULL, NULL, NULL, N'ddfdfdfdfdfdf', NULL, NULL, 5, 6, NULL, NULL, NULL, 30, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (31, 17, 1, 2, CAST(N'2026-09-23T09:00:00.0000000' AS DateTime2), N'No asistio', N'', NULL, NULL, NULL, N'', NULL, NULL, 4, 6, CAST(N'2026-09-22T22:17:48.583' AS DateTime), NULL, NULL, 30, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (32, 5, NULL, 2, CAST(N'2026-09-24T08:00:00.0000000' AS DateTime2), N'No asistio', N'Cita de blanqueamiento', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, NULL, NULL, NULL, 60, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (33, 17, 1, 3, CAST(N'2026-09-24T08:00:00.0000000' AS DateTime2), N'No asistio', N'limpieza profunda', NULL, NULL, NULL, N'limpieza profunda', NULL, NULL, 2, 6, NULL, NULL, NULL, 30, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (34, 7, 1, 2, CAST(N'2026-09-25T09:00:00.0000000' AS DateTime2), N'No asistio', N'', NULL, NULL, NULL, N'', NULL, NULL, 2, 6, CAST(N'2026-09-23T17:01:35.420' AS DateTime), NULL, NULL, 30, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (35, 3, 1, 4, CAST(N'2026-11-04T09:00:00.0000000' AS DateTime2), N'No asistio', N'', NULL, NULL, NULL, N'', NULL, NULL, 2, 6, CAST(N'2026-11-03T21:12:30.117' AS DateTime), NULL, NULL, 30, NULL)
INSERT [dbo].[Cita] ([id_cita], [id_paciente], [id_profesional], [id_servicio], [fecha_hora], [estado], [notas], [fecha], [hora_inicio], [hora_fin], [motivo_consulta], [notas_previas], [tipo_cita], [id_consultorio], [id_estado], [fecha_creacion], [creado_por], [archivo_adjunto], [duracion_minutos], [motivo_cancelacion]) VALUES (36, 5, NULL, 4, CAST(N'2026-11-05T08:00:00.0000000' AS DateTime2), N'No asistio', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 6, NULL, NULL, NULL, 60, NULL)
SET IDENTITY_INSERT [dbo].[Cita] OFF
GO
SET IDENTITY_INSERT [dbo].[Cita_Historial_Estado] ON

INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (4, 30, 1, N'Agendada', NULL, N'Creación de la cita', CAST(N'2026-09-21T19:43:24.5597041' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (5, 31, 1, N'Agendada', NULL, N'Creación de la cita', CAST(N'2026-09-22T22:17:48.8310786' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (6, 31, 1, N'Agendada', NULL, N'Cita creada', CAST(N'2026-09-22T22:17:48.8775095' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (7, 32, 7, N'Solicitada', NULL, N'Creación de la cita', CAST(N'2026-09-23T15:37:43.9613003' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (8, 33, 1, N'Agendada', NULL, N'Creación de la cita', CAST(N'2026-09-23T15:56:11.8930738' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (9, 34, 1, N'Agendada', NULL, N'Creación de la cita', CAST(N'2026-09-23T17:01:35.4762266' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (10, 34, 1, N'Agendada', NULL, N'Cita creada', CAST(N'2026-09-23T17:01:35.4457003' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (11, 30, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (12, 34, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (13, 33, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (14, 32, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (15, 31, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (16, 19, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (17, 26, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (18, 1, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (19, 9, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (20, 5, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (21, 23, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (22, 25, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (23, 20, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (24, 4, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (25, 16, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-10-28T21:27:39.8792331' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (26, 35, 1, N'Agendada', NULL, N'Creación de la cita', CAST(N'2026-11-03T21:12:30.3970763' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (27, 35, 1, N'Agendada', NULL, N'Cita creada', CAST(N'2026-11-03T21:12:30.2565721' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (28, 36, 7, N'Solicitada', NULL, N'Creación de la cita', CAST(N'2026-11-04T22:06:09.3082676' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (29, 36, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-11-06T20:14:20.0171237' AS DateTime2))
INSERT [dbo].[Cita_Historial_Estado] ([id_historial], [id_cita], [id_estado], [estado_texto], [id_usuario], [motivo], [fecha_cambio]) VALUES (30, 35, 6, N'No asistio', NULL, N'Cambio registrado en la cita', CAST(N'2026-11-06T20:14:20.0171237' AS DateTime2))
SET IDENTITY_INSERT [dbo].[Cita_Historial_Estado] OFF
GO
SET IDENTITY_INSERT [dbo].[CodigoRecuperacion] ON

INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (1, 1, N'$2a$11$ATRuxhQk9JXlxLlmnwLrmOzzuxFn12p3vcu0BkPs7EFnyGrcNqCFy', CAST(N'2026-08-04T22:09:21.413' AS DateTime), CAST(N'2026-08-04T22:24:21.413' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (2, 1, N'$2a$11$SRjJHzx9lwt1r5o.W1RIDe97bTADdG2T5eaBQE018K.3yQsWOs/.e', CAST(N'2026-08-04T22:11:05.463' AS DateTime), CAST(N'2026-08-04T22:26:05.463' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (3, 1, N'$2a$11$fyEqXoOUvCOe1dhEwiXAy.qhHa8TLi4yVDKfexpQRIpN7SuEVET9u', CAST(N'2026-08-04T22:12:06.327' AS DateTime), CAST(N'2026-08-04T22:27:06.327' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (4, 1, N'$2a$11$6x1mZEtBpYBxyh42DRe4OeOZydkfp7jI.LTyaaiZYcDLqsRGas3DO', CAST(N'2026-08-04T22:13:50.853' AS DateTime), CAST(N'2026-08-04T22:28:50.853' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (5, 1, N'$2a$11$v3mLM4dacMm/lkL/4OE9SOuFnzgGZ23IMol1w5cJJVRVT46hsJJTu', CAST(N'2026-08-04T22:16:11.137' AS DateTime), CAST(N'2026-08-04T22:31:11.137' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (6, 1, N'$2a$11$6bWzlNsan9uQVeIaf/eKiel1NwHi.Jw6sCAuLaRvMiQKlrHX0/r6m', CAST(N'2026-08-10T18:17:59.340' AS DateTime), CAST(N'2026-08-10T18:32:59.340' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (7, 1, N'$2a$11$tNE4ueD0hodTI2QgGUwMze/9.I4QT2RZNURfs7KgZOG4zcDfTb1Ui', CAST(N'2026-08-11T18:42:51.663' AS DateTime), CAST(N'2026-08-11T18:57:51.663' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (8, 7, N'$2a$11$eIwrzQ4.tw1YDv86qna76uD82YuMp7bJXbwLSg4sWuD0V0Xtb/Yvu', CAST(N'2026-09-22T22:15:34.607' AS DateTime), CAST(N'2026-09-22T22:30:34.607' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (9, 7, N'$2a$11$037vZ8b.Ea.x9NETdTDpaeLL.SKXVqJxXRkj8wMio7M4Wz9bQuR4K', CAST(N'2026-09-22T22:15:35.080' AS DateTime), CAST(N'2026-09-22T22:30:35.080' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (10, 7, N'$2a$11$iKj/s4bumK/Wc4w2C91VruiYL7c0NGmuTwgs3z7q/J7FfV7SvWYKK', CAST(N'2026-09-22T22:15:35.283' AS DateTime), CAST(N'2026-09-22T22:30:35.283' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (11, 7, N'$2a$11$YQAJk5/O1qI33Ih0z43.3OemEhlsSDLnnlWEHW/f7GMyA8gU6Flc.', CAST(N'2026-09-22T22:15:35.483' AS DateTime), CAST(N'2026-09-22T22:30:35.483' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (12, 7, N'$2a$11$T.wnjj1VAchTuy5tPyY6ged4DnAwwW/RiMc4pQVPZdEJGUujkgS4a', CAST(N'2026-09-22T22:15:35.683' AS DateTime), CAST(N'2026-09-22T22:30:35.683' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (13, 7, N'$2a$11$6YwQ48Q/eR5EdyI2ARLZMOXzhQlnEPdY.VfeEK3xcBqjLLeO9y7V2', CAST(N'2026-09-22T22:15:35.870' AS DateTime), CAST(N'2026-09-22T22:30:35.870' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (14, 7, N'$2a$11$FbvfHigSO8D0fjAa7UQ9MOH8dLySxTTDymJZjV1hH4JXhJNs7sera', CAST(N'2026-09-22T22:15:36.047' AS DateTime), CAST(N'2026-09-22T22:30:36.047' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (15, 7, N'$2a$11$QczZDtHRXIuKddwlMWJpp.pWs8kjUZUUNm0zn.NQsiaHsifZPUxWm', CAST(N'2026-09-22T22:15:36.200' AS DateTime), CAST(N'2026-09-22T22:30:36.200' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (16, 7, N'$2a$11$gN8ctF9Y5oMqV2WTvdS0HO2yx0BlHfTrszCxWGDACLxXy8EXi.3qq', CAST(N'2026-09-22T22:15:41.763' AS DateTime), CAST(N'2026-09-22T22:30:41.763' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (17, 7, N'$2a$11$.Ug88evHq.7gCflap8QxWe/8VLOqcOOZWKLY00/4w2D7ZL9M2BBD2', CAST(N'2026-09-22T22:15:42.940' AS DateTime), CAST(N'2026-09-22T22:30:42.940' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (18, 7, N'$2a$11$THYg8h.iycMG3l6Hmvlgge8ppb407VoKIBgWC/qu9l67cdEEJopOW', CAST(N'2026-09-22T22:23:44.123' AS DateTime), CAST(N'2026-09-22T22:38:44.123' AS DateTime), 0, 0, N'::1')
INSERT [dbo].[CodigoRecuperacion] ([id_codigo], [id_usuario], [codigo_hash], [fecha_creacion], [fecha_expiracion], [intentos_fallidos], [usado], [ip_origen]) VALUES (19, 7, N'$2a$11$l0VOVY3yn5/.VV9VOLq7J.CsU965sqHiQGuk67ntFSL..HXKSTTlS', CAST(N'2026-09-22T22:23:45.673' AS DateTime), CAST(N'2026-09-22T22:38:45.673' AS DateTime), 0, 0, N'::1')
SET IDENTITY_INSERT [dbo].[CodigoRecuperacion] OFF
GO
SET IDENTITY_INSERT [dbo].[Configuracion_General] ON

INSERT [dbo].[Configuracion_General] ([id_configuracion], [clave], [valor], [descripcion], [modulo]) VALUES (1, N'cita_duracion_minutos', N'60', N'Duración predeterminada de cada cita en minutos. Usado para calcular bloques de agenda y detectar conflictos de horario.', N'citas')
INSERT [dbo].[Configuracion_General] ([id_configuracion], [clave], [valor], [descripcion], [modulo]) VALUES (2, N'horario_apertura', N'07:00', N'Hora de apertura de la clínica (formato HH:mm). Las citas no pueden agendarse antes de este horario.', N'citas')
INSERT [dbo].[Configuracion_General] ([id_configuracion], [clave], [valor], [descripcion], [modulo]) VALUES (3, N'horario_cierre', N'18:00', N'Hora de cierre de la clínica (formato HH:mm). Las citas no pueden agendarse después de este horario.', N'citas')
INSERT [dbo].[Configuracion_General] ([id_configuracion], [clave], [valor], [descripcion], [modulo]) VALUES (4, N'dias_atencion', N'1,2,3,4,5,6', N'Días de atención separados por coma (1=Lunes … 7=Domingo). Valor predeterminado: lunes a sábado.', N'citas')
SET IDENTITY_INSERT [dbo].[Configuracion_General] OFF
GO
SET IDENTITY_INSERT [dbo].[Consulta_Clinica] ON

INSERT [dbo].[Consulta_Clinica] ([id_consulta_clinica], [id_historia], [id_cita], [id_nota], [id_proxima_cita], [id_profesional], [fecha_consulta], [motivo_consulta], [enfermedad_actual], [antecedentes_medicos], [habitos], [alergias], [hallazgos], [odontograma_observaciones], [examenes_complementarios], [evolucion_clinica], [prescripcion], [origen], [fecha_registro]) VALUES (1, 17, NULL, NULL, NULL, NULL, NULL, N'Me duele mucho una muela del lado inferior derecho cuando tomo agua fría o mastico cosas duras desde hace tres días.', N'Paciente refiere dolor de instauración gradual hace 72 horas localizado en la zona del órgano dental 46. El dolor es de intensidad moderada (6/10 en escala EVA) y de tipo punzante. Se agrava significativamente con estímulos térmicos fríos y durante la masticación. Cede parcialmente tras la ingesta de paracetamol de 500 mg y al retirar el estímulo.', NULL, N'Higiene oral reportada de 2 veces al día con cepillo manual, uso ocasional de hilo dental. Consumo moderado de café (2 tazas al día). Niega tabaquismo y alcoholismo. Ligero bruxismo céntrico nocturno autodeclarado.', NULL, N'A la inspección clínica se observa una lesión cariosa profunda con pérdida de estructura adamantina y dentinaria en la cara ocluso-vestibular del órgano dental 46. Tejido gingival circundante de características normales, rosado y firme, sin signos de inflamación aguda ni sangrado espontáneo. Presencia de placa bacteriana leve en caras linguales de molares inferiores.', N'', N'Se solicita radiografía periapical digital de la zona inferior derecha (pieza 46) para evaluar la proximidad de la caries a la cámara pulpar y descartar posibles lesiones periapicales asociadas.', N'Paciente acude a consulta por primera vez presentando dolor en cuadrante 4. Se realiza anamnesis detallada y examen clínico intraoral. Se evidenció caries profunda en pieza 46. Se realizaron pruebas de vitalidad pulpar térmica (positiva transitoria aumentada) y percusión vertical (negativa). Se procedió a la toma de radiografía periapical intraoral. Se explica el plan de tratamiento al paciente, quien firma el consentimiento informado. Se agenda próxima cita para remoción de caries y restauración.', N'Ibuprofeno 400 mg tabletas: Tomar 1 tableta vía oral cada 8 horas por 3 días únicamente en caso de presentar dolor moderado, preferiblemente con alimentos.\n\nInstrucciones: Mantener una rigurosa higiene oral mediante cepillado meticuloso después de cada comida, incorporar el uso diario de hilo dental y evitar alimentos excesivamente duros, pegajosos o temperaturas extremas en el lado derecho hasta realizar la restauración definitiva.', N'migracion-json', CAST(N'2026-11-10T19:14:20.9685655' AS DateTime2))
SET IDENTITY_INSERT [dbo].[Consulta_Clinica] OFF
GO
SET IDENTITY_INSERT [dbo].[Consulta_Clinica_Diagnostico] ON

INSERT [dbo].[Consulta_Clinica_Diagnostico] ([id_diagnostico], [id_consulta_clinica], [codigo_cie10], [descripcion], [es_principal]) VALUES (1, 1, N'K02.1', N'K02.1 - Caries de la dentina', 1)
SET IDENTITY_INSERT [dbo].[Consulta_Clinica_Diagnostico] OFF
GO
SET IDENTITY_INSERT [dbo].[Consulta_Clinica_Sintoma] ON

INSERT [dbo].[Consulta_Clinica_Sintoma] ([id_sintoma], [id_consulta_clinica], [descripcion], [orden]) VALUES (1, 1, N'Sensibilidad al frío', 0)
INSERT [dbo].[Consulta_Clinica_Sintoma] ([id_sintoma], [id_consulta_clinica], [descripcion], [orden]) VALUES (2, 1, N'Dolor al masticar', 1)
SET IDENTITY_INSERT [dbo].[Consulta_Clinica_Sintoma] OFF
GO
SET IDENTITY_INSERT [dbo].[Consultorio] ON

INSERT [dbo].[Consultorio] ([id_consultorio], [nombre], [ubicacion], [tipo], [nombre_estado], [capacidad], [estado]) VALUES (1, N'Box 01 - General', N'Planta 1 - Ala Sur', N'Consulta General', N'Disponible', 1, N'mantenimiento')
INSERT [dbo].[Consultorio] ([id_consultorio], [nombre], [ubicacion], [tipo], [nombre_estado], [capacidad], [estado]) VALUES (2, N'Box 02 - Ortodoncia', N'Planta 1 - Ala Norte', N'Ortodoncia', N'Disponible', 1, N'mantenimiento')
INSERT [dbo].[Consultorio] ([id_consultorio], [nombre], [ubicacion], [tipo], [nombre_estado], [capacidad], [estado]) VALUES (3, N'Box 03 - Cirugía', N'Planta 2 - Ala Central', N'Cirugía y Procedimientos', N'Disponible', 1, N'mantenimiento')
INSERT [dbo].[Consultorio] ([id_consultorio], [nombre], [ubicacion], [tipo], [nombre_estado], [capacidad], [estado]) VALUES (4, N'Consultorio 1', N'Piso 1 - ala norte', N'general', NULL, 1, N'disponible')
INSERT [dbo].[Consultorio] ([id_consultorio], [nombre], [ubicacion], [tipo], [nombre_estado], [capacidad], [estado]) VALUES (5, N'Consultorio 2', N'Piso 1 - ala sur', N'quirófano', NULL, 1, N'disponible')
INSERT [dbo].[Consultorio] ([id_consultorio], [nombre], [ubicacion], [tipo], [nombre_estado], [capacidad], [estado]) VALUES (6, N'Consultorio 3', N'Piso 2 - ala este', N'especializado', NULL, 1, N'no-disponible')
INSERT [dbo].[Consultorio] ([id_consultorio], [nombre], [ubicacion], [tipo], [nombre_estado], [capacidad], [estado]) VALUES (7, N'Box 04 - Odontopediatria', N'Planta 1 - Ala Este', N'Odontopediatria', N'Disponible', 1, N'mantenimiento')
INSERT [dbo].[Consultorio] ([id_consultorio], [nombre], [ubicacion], [tipo], [nombre_estado], [capacidad], [estado]) VALUES (8, N'Box 05 - Rehabilitacion Oral', N'Planta 2 - Ala Oeste', N'Rehabilitacion Oral', N'Disponible', 1, N'disponible')
SET IDENTITY_INSERT [dbo].[Consultorio] OFF
GO
SET IDENTITY_INSERT [dbo].[Consultorio_Estado_Operativo] ON

INSERT [dbo].[Consultorio_Estado_Operativo] ([id_estado_operativo], [id_consultorio], [checklist_json], [observaciones], [actualizado_por], [actualizado_en]) VALUES (1, 1, N'[{"text":"Limpieza y desinfecci\u00F3n de superficies","checked":true},{"text":"Instrumental esterilizado y empaquetado","checked":true},{"text":"Residuos biol\u00F3gicos eliminados","checked":true},{"text":"Guantes y tapabocas reabastecidos","checked":true},{"text":"Historia cl\u00EDnica lista para pr\u00F3ximo","checked":false},{"text":"Equipos verificados y encendidos","checked":false},{"text":"Consultorio ventilado","checked":false}]', N'', 5, CAST(N'2026-11-10T19:26:54.9667904' AS DateTime2))
INSERT [dbo].[Consultorio_Estado_Operativo] ([id_estado_operativo], [id_consultorio], [checklist_json], [observaciones], [actualizado_por], [actualizado_en]) VALUES (2, 6, N'[{"text":"Limpieza y desinfecci\u00F3n de superficies","checked":true},{"text":"Instrumental esterilizado y empaquetado","checked":true},{"text":"Residuos biol\u00F3gicos eliminados","checked":true},{"text":"Guantes y tapabocas reabastecidos","checked":false},{"text":"Historia cl\u00EDnica lista para pr\u00F3ximo","checked":false},{"text":"Equipos verificados y encendidos","checked":false},{"text":"Consultorio ventilado","checked":false}]', N'', 5, CAST(N'2026-11-10T19:26:59.9918099' AS DateTime2))
INSERT [dbo].[Consultorio_Estado_Operativo] ([id_estado_operativo], [id_consultorio], [checklist_json], [observaciones], [actualizado_por], [actualizado_en]) VALUES (3, 4, N'[{"text":"Limpieza y desinfecci\u00F3n de superficies","checked":true},{"text":"Instrumental esterilizado y empaquetado","checked":true},{"text":"Residuos biol\u00F3gicos eliminados","checked":true},{"text":"Guantes y tapabocas reabastecidos","checked":false},{"text":"Historia cl\u00EDnica lista para pr\u00F3ximo","checked":false},{"text":"Equipos verificados y encendidos","checked":false},{"text":"Consultorio ventilado","checked":false}]', N'', 5, CAST(N'2026-11-10T19:27:24.2736548' AS DateTime2))
INSERT [dbo].[Consultorio_Estado_Operativo] ([id_estado_operativo], [id_consultorio], [checklist_json], [observaciones], [actualizado_por], [actualizado_en]) VALUES (4, 5, N'[{"text":"Limpieza y desinfecci\u00F3n de superficies","checked":true},{"text":"Instrumental esterilizado y empaquetado","checked":true},{"text":"Residuos biol\u00F3gicos eliminados","checked":true},{"text":"Guantes y tapabocas reabastecidos","checked":false},{"text":"Historia cl\u00EDnica lista para pr\u00F3ximo","checked":false},{"text":"Equipos verificados y encendidos","checked":false},{"text":"Consultorio ventilado","checked":false}]', N'', 5, CAST(N'2026-11-10T19:27:26.9046676' AS DateTime2))
INSERT [dbo].[Consultorio_Estado_Operativo] ([id_estado_operativo], [id_consultorio], [checklist_json], [observaciones], [actualizado_por], [actualizado_en]) VALUES (5, 7, N'[{"text":"Limpieza y desinfecci\u00F3n de superficies","checked":true},{"text":"Instrumental esterilizado y empaquetado","checked":true},{"text":"Residuos biol\u00F3gicos eliminados","checked":true},{"text":"Guantes y tapabocas reabastecidos","checked":false},{"text":"Historia cl\u00EDnica lista para pr\u00F3ximo","checked":false},{"text":"Equipos verificados y encendidos","checked":false},{"text":"Consultorio ventilado","checked":false}]', N'', 5, CAST(N'2026-11-03T22:26:59.5400974' AS DateTime2))
INSERT [dbo].[Consultorio_Estado_Operativo] ([id_estado_operativo], [id_consultorio], [checklist_json], [observaciones], [actualizado_por], [actualizado_en]) VALUES (6, 3, N'[{"text":"Limpieza y desinfecci\u00F3n de superficies","checked":true},{"text":"Instrumental esterilizado y empaquetado","checked":true},{"text":"Residuos biol\u00F3gicos eliminados","checked":true},{"text":"Guantes y tapabocas reabastecidos","checked":false},{"text":"Historia cl\u00EDnica lista para pr\u00F3ximo","checked":false},{"text":"Equipos verificados y encendidos","checked":false},{"text":"Consultorio ventilado","checked":false}]', N'', 5, CAST(N'2026-11-03T22:26:50.3556648' AS DateTime2))
INSERT [dbo].[Consultorio_Estado_Operativo] ([id_estado_operativo], [id_consultorio], [checklist_json], [observaciones], [actualizado_por], [actualizado_en]) VALUES (7, 8, N'[{"text":"Limpieza y desinfecci\u00F3n de superficies","checked":true},{"text":"Instrumental esterilizado y empaquetado","checked":true},{"text":"Residuos biol\u00F3gicos eliminados","checked":true},{"text":"Guantes y tapabocas reabastecidos","checked":false},{"text":"Historia cl\u00EDnica lista para pr\u00F3ximo","checked":false},{"text":"Equipos verificados y encendidos","checked":false},{"text":"Consultorio ventilado","checked":false}]', N'', 5, CAST(N'2026-11-11T20:54:52.6177503' AS DateTime2))
INSERT [dbo].[Consultorio_Estado_Operativo] ([id_estado_operativo], [id_consultorio], [checklist_json], [observaciones], [actualizado_por], [actualizado_en]) VALUES (8, 2, N'[{"text":"Limpieza y desinfecci\u00F3n de superficies","checked":true},{"text":"Instrumental esterilizado y empaquetado","checked":true},{"text":"Residuos biol\u00F3gicos eliminados","checked":true},{"text":"Guantes y tapabocas reabastecidos","checked":false},{"text":"Historia cl\u00EDnica lista para pr\u00F3ximo","checked":false},{"text":"Equipos verificados y encendidos","checked":false},{"text":"Consultorio ventilado","checked":false}]', N'', 5, CAST(N'2026-11-10T19:27:03.2156085' AS DateTime2))
SET IDENTITY_INSERT [dbo].[Consultorio_Estado_Operativo] OFF
GO
SET IDENTITY_INSERT [dbo].[Consultorio_Historial] ON

INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (1, 1, N'disponible', 5, N'', CAST(N'2026-09-22T01:19:52.1472831' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (2, 1, N'mantenimiento', 5, N'', CAST(N'2026-09-22T01:21:45.2583099' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (6, 1, N'mantenimiento', 5, N'', CAST(N'2026-10-29T22:42:06.7242928' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (7, 6, N'disponible', 5, N'', CAST(N'2026-10-29T22:42:08.3500010' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (8, 6, N'disponible', 5, N'', CAST(N'2026-10-29T22:42:12.7091210' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (9, 4, N'disponible', 5, N'', CAST(N'2026-10-29T22:42:19.4439189' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (10, 6, N'disponible', 5, N'', CAST(N'2026-10-29T22:42:21.7992181' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (11, 6, N'disponible', 5, N'', CAST(N'2026-10-29T22:42:27.3260274' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (12, 6, N'disponible', 5, N'', CAST(N'2026-10-29T22:43:51.8182065' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (13, 6, N'disponible', 5, N'', CAST(N'2026-11-03T21:22:49.2150176' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (14, 4, N'disponible', 5, N'', CAST(N'2026-11-03T21:22:53.0593152' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (15, 5, N'disponible', 5, N'', CAST(N'2026-11-03T21:22:56.0647705' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (16, 5, N'disponible', 5, N'', CAST(N'2026-11-03T21:23:05.2869959' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (17, 7, N'disponible', 5, N'', CAST(N'2026-11-03T21:23:07.1515300' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (18, 7, N'mantenimiento', 5, N'', CAST(N'2026-11-03T21:23:09.1739037' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (19, 7, N'mantenimiento', 5, N'', CAST(N'2026-11-03T21:23:12.7380738' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (20, 3, N'disponible', 5, N'', CAST(N'2026-11-03T21:23:15.9296563' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (21, 7, N'mantenimiento', 5, N'', CAST(N'2026-11-03T21:23:20.2821241' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (22, 1, N'mantenimiento', 5, N'', CAST(N'2026-11-03T21:23:24.4832609' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (23, 1, N'disponible', 5, N'', CAST(N'2026-11-03T21:23:27.9420646' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (24, 1, N'disponible', 5, N'', CAST(N'2026-11-03T21:23:33.9470179' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (25, 3, N'disponible', 5, N'', CAST(N'2026-11-03T21:23:36.9187697' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (26, 6, N'disponible', 5, N'', CAST(N'2026-11-03T21:23:39.6797769' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (27, 8, N'disponible', 5, N'', CAST(N'2026-11-03T21:23:42.1585753' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (28, 7, N'mantenimiento', 5, N'', CAST(N'2026-11-03T21:23:44.1965584' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (29, 1, N'disponible', 5, N'', CAST(N'2026-11-03T21:24:36.8295715' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (30, 3, N'disponible', 5, N'', CAST(N'2026-11-03T21:24:41.8245980' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (31, 3, N'disponible', 5, N'', CAST(N'2026-11-03T21:24:43.6906489' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (32, 3, N'mantenimiento', 5, N'', CAST(N'2026-11-03T21:24:46.0001910' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (33, 3, N'mantenimiento', 5, N'', CAST(N'2026-11-03T21:24:47.3603279' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (34, 6, N'disponible', 5, N'', CAST(N'2026-11-03T21:24:51.2305945' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (35, 3, N'mantenimiento', 5, N'', CAST(N'2026-11-03T21:24:53.8204231' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (36, 3, N'mantenimiento', 5, N'', CAST(N'2026-11-03T21:41:28.4807648' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (37, 3, N'mantenimiento', 5, N'', CAST(N'2026-11-03T21:50:34.1877310' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (38, 3, N'mantenimiento', 5, N'', CAST(N'2026-11-03T22:15:50.3511110' AS DateTime2), NULL, NULL)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (39, 1, N'mantenimiento', 5, N'', CAST(N'2026-11-10T19:24:34.9397962' AS DateTime2), 1, 3)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (40, 2, N'mantenimiento', 5, N'', CAST(N'2026-11-10T19:24:44.1938880' AS DateTime2), 1, 3)
INSERT [dbo].[Consultorio_Historial] ([id_historial], [id_consultorio], [estado], [id_usuario], [motivo], [fecha_cambio], [id_estado_anterior], [id_estado_nuevo]) VALUES (41, 6, N'no-disponible', 5, N'', CAST(N'2026-11-10T19:26:59.9918099' AS DateTime2), 1, 4)
SET IDENTITY_INSERT [dbo].[Consultorio_Historial] OFF
GO
SET IDENTITY_INSERT [dbo].[Credencial_Profesional] ON

INSERT [dbo].[Credencial_Profesional] ([id_credencial], [id_profesional], [subido_por], [tipo], [nombre_original], [ruta_privada], [content_type], [tamano_bytes], [estado_revision], [observacion_revision], [fecha_subida], [revisado_por], [fecha_revision]) VALUES (1, 1, 3, N'Certificado médico', N'Taller Algoritmos 2018.pdf', N'private_uploads\credenciales-profesionales\1\75c32a884cf446d0b5cf844c541ba9db.pdf', N'application/pdf', 1443479, N'rechazado', NULL, CAST(N'2026-11-10T21:09:58.3011025' AS DateTime2), 1, CAST(N'2026-11-12T17:49:22.9097199' AS DateTime2))
INSERT [dbo].[Credencial_Profesional] ([id_credencial], [id_profesional], [subido_por], [tipo], [nombre_original], [ruta_privada], [content_type], [tamano_bytes], [estado_revision], [observacion_revision], [fecha_subida], [revisado_por], [fecha_revision]) VALUES (2, 1, 3, N'Estudios complementarios', N'Captura de pantalla 2026-07-14 114932.png', N'private_uploads\credenciales-profesionales\1\544dc0ca3630444c85496205e173cdd4.png', N'image/png', 25627, N'aprobado', NULL, CAST(N'2026-11-10T21:12:59.0199915' AS DateTime2), 1, CAST(N'2026-11-12T17:49:19.6604437' AS DateTime2))
SET IDENTITY_INSERT [dbo].[Credencial_Profesional] OFF
GO
SET IDENTITY_INSERT [dbo].[Detalle_Factura] ON

INSERT [dbo].[Detalle_Factura] ([id_detalle], [id_factura], [id_servicio], [descripcion], [cantidad], [precio_unitario], [subtotal_linea], [descuento]) VALUES (1, 7, 1, N'Limpieza dental', 1, CAST(80000.00 AS Decimal(12, 2)), CAST(80000.00 AS Decimal(12, 2)), CAST(0.00 AS Decimal(12, 2)))
INSERT [dbo].[Detalle_Factura] ([id_detalle], [id_factura], [id_servicio], [descripcion], [cantidad], [precio_unitario], [subtotal_linea], [descuento]) VALUES (2, 8, 1, N'Limpieza dental', 1, CAST(80000.00 AS Decimal(12, 2)), CAST(80000.00 AS Decimal(12, 2)), CAST(0.00 AS Decimal(12, 2)))
SET IDENTITY_INSERT [dbo].[Detalle_Factura] OFF
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
SET IDENTITY_INSERT [dbo].[Especialidad] ON

INSERT [dbo].[Especialidad] ([id_especialidad], [nombre], [descripcion]) VALUES (1, N'Odontología General', N'Atención dental primaria y preventiva')
INSERT [dbo].[Especialidad] ([id_especialidad], [nombre], [descripcion]) VALUES (2, N'Ortodoncia', N'Corrección de la posición dental')
INSERT [dbo].[Especialidad] ([id_especialidad], [nombre], [descripcion]) VALUES (3, N'Endodoncia', N'Tratamiento de conductos radiculares')
INSERT [dbo].[Especialidad] ([id_especialidad], [nombre], [descripcion]) VALUES (4, N'Periodoncia', N'Enfermedades de encías y tejidos de soporte')
INSERT [dbo].[Especialidad] ([id_especialidad], [nombre], [descripcion]) VALUES (5, N'Rehabilitación Oral y Estética Dental', N'Restauración funcional y estética')
INSERT [dbo].[Especialidad] ([id_especialidad], [nombre], [descripcion]) VALUES (6, N'Cirugía Oral', N'Procedimientos quirúrgicos orales')
INSERT [dbo].[Especialidad] ([id_especialidad], [nombre], [descripcion]) VALUES (7, N'Odontopediatría', N'Odontología para niños y adolescentes')
SET IDENTITY_INSERT [dbo].[Especialidad] OFF
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
SET IDENTITY_INSERT [dbo].[Factura] ON

INSERT [dbo].[Factura] ([id_factura], [numero_factura], [fecha_factura], [subtotal], [total], [estado], [id_paciente], [notas], [generada_por], [monto_pagado], [fecha_pago], [motivo_anulacion], [fecha_anulacion], [anulada_por], [id_cita], [id_profesional], [descuento], [clave_idempotencia]) VALUES (1, N'FAC-2026-001', CAST(N'2026-08-06' AS Date), CAST(1200000.00 AS Decimal(12, 2)), CAST(1200000.00 AS Decimal(12, 2)), N'pendiente', 1, N'Tratamiento ortodoncia', 1, CAST(0.00 AS Decimal(12, 2)), NULL, NULL, NULL, NULL, NULL, NULL, CAST(0.00 AS Decimal(12, 2)), NULL)
INSERT [dbo].[Factura] ([id_factura], [numero_factura], [fecha_factura], [subtotal], [total], [estado], [id_paciente], [notas], [generada_por], [monto_pagado], [fecha_pago], [motivo_anulacion], [fecha_anulacion], [anulada_por], [id_cita], [id_profesional], [descuento], [clave_idempotencia]) VALUES (2, N'FAC-2026-002', CAST(N'2026-08-08' AS Date), CAST(850000.00 AS Decimal(12, 2)), CAST(850000.00 AS Decimal(12, 2)), N'pendiente', 2, N'Limpieza y blanqueamiento', 1, CAST(0.00 AS Decimal(12, 2)), NULL, NULL, NULL, NULL, NULL, NULL, CAST(0.00 AS Decimal(12, 2)), NULL)
INSERT [dbo].[Factura] ([id_factura], [numero_factura], [fecha_factura], [subtotal], [total], [estado], [id_paciente], [notas], [generada_por], [monto_pagado], [fecha_pago], [motivo_anulacion], [fecha_anulacion], [anulada_por], [id_cita], [id_profesional], [descuento], [clave_idempotencia]) VALUES (3, N'FAC-2026-003', CAST(N'2026-08-10' AS Date), CAST(560000.00 AS Decimal(12, 2)), CAST(560000.00 AS Decimal(12, 2)), N'pendiente', 3, N'Consulta general', 1, CAST(0.00 AS Decimal(12, 2)), NULL, NULL, NULL, NULL, NULL, NULL, CAST(0.00 AS Decimal(12, 2)), NULL)
INSERT [dbo].[Factura] ([id_factura], [numero_factura], [fecha_factura], [subtotal], [total], [estado], [id_paciente], [notas], [generada_por], [monto_pagado], [fecha_pago], [motivo_anulacion], [fecha_anulacion], [anulada_por], [id_cita], [id_profesional], [descuento], [clave_idempotencia]) VALUES (7, N'FAC-2026-00004', CAST(N'2026-10-27' AS Date), CAST(80000.00 AS Decimal(12, 2)), CAST(80000.00 AS Decimal(12, 2)), N'pendiente', 1, NULL, 4, CAST(0.00 AS Decimal(12, 2)), NULL, NULL, NULL, NULL, 17, 6, CAST(0.00 AS Decimal(12, 2)), NULL)
INSERT [dbo].[Factura] ([id_factura], [numero_factura], [fecha_factura], [subtotal], [total], [estado], [id_paciente], [notas], [generada_por], [monto_pagado], [fecha_pago], [motivo_anulacion], [fecha_anulacion], [anulada_por], [id_cita], [id_profesional], [descuento], [clave_idempotencia]) VALUES (8, N'FAC-2026-00005', CAST(N'2026-10-29' AS Date), CAST(80000.00 AS Decimal(12, 2)), CAST(80000.00 AS Decimal(12, 2)), N'pagada', 3, NULL, 4, CAST(80000.00 AS Decimal(12, 2)), CAST(N'2026-10-29T17:49:03.200' AS DateTime), NULL, NULL, NULL, NULL, 3, CAST(0.00 AS Decimal(12, 2)), N'a35087e6-d558-4bd9-b307-3a18e818a951')
SET IDENTITY_INSERT [dbo].[Factura] OFF
GO
SET IDENTITY_INSERT [dbo].[Historia_Clinica] ON

INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (1, 1, CAST(N'2026-07-31T20:48:55.5475044' AS DateTime2), N'{"registros":{"266":{"NombrePieza":"TOOTH_UP_11_TOOTH.BMP_0","Tratamientos":[{"Key":"caries","Obs":"dad","Fecha":"2026-07-31T21:03:43.566Z"}]}},"mapeoFDI":{},"actualizadoEn":"2026-07-31T21:03:43.575083Z"}', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (2, 2, CAST(N'2026-07-31T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (3, 3, CAST(N'2026-07-31T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (4, 4, CAST(N'2026-07-31T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (5, 5, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (6, 6, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (7, 7, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (8, 8, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (9, 9, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (10, 10, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (11, 11, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (12, 12, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (13, 13, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (14, 14, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (15, 15, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (16, 16, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'Paciente sin antecedentes relevantes registrados al momento de apertura.', 1)
INSERT [dbo].[Historia_Clinica] ([id_historia], [id_paciente], [fecha_apertura], [observaciones_generales], [activa]) VALUES (17, 17, CAST(N'2026-09-22T18:10:19.8840669' AS DateTime2), N'{"formularioHistoria":{"MotivoConsulta":"Me duele mucho una muela del lado inferior derecho cuando tomo agua fr\u00EDa o mastico cosas duras desde hace tres d\u00EDas.","EnfermedadActual":"Paciente refiere dolor de instauraci\u00F3n gradual hace 72 horas localizado en la zona del \u00F3rgano dental 46. El dolor es de intensidad moderada (6/10 en escala EVA) y de tipo punzante. Se agrava significativamente con est\u00EDmulos t\u00E9rmicos fr\u00EDos y durante la masticaci\u00F3n. Cede parcialmente tras la ingesta de paracetamol de 500 mg y al retirar el est\u00EDmulo.","SintomasSignos":["Sensibilidad al fr\u00EDo","Dolor al masticar"],"Habitos":"Higiene oral reportada de 2 veces al d\u00EDa con cepillo manual, uso ocasional de hilo dental. Consumo moderado de caf\u00E9 (2 tazas al d\u00EDa). Niega tabaquismo y alcoholismo. Ligero bruxismo c\u00E9ntrico nocturno autodeclarado.","Hallazgos":"A la inspecci\u00F3n cl\u00EDnica se observa una lesi\u00F3n cariosa profunda con p\u00E9rdida de estructura adamantina y dentinaria en la cara ocluso-vestibular del \u00F3rgano dental 46. Tejido gingival circundante de caracter\u00EDsticas normales, rosado y firme, sin signos de inflamaci\u00F3n aguda ni sangrado espont\u00E1neo. Presencia de placa bacteriana leve en caras linguales de molares inferiores.","OdontogramaObservaciones":"","ExamenesComplementarios":"Se solicita radiograf\u00EDa periapical digital de la zona inferior derecha (pieza 46) para evaluar la proximidad de la caries a la c\u00E1mara pulpar y descartar posibles lesiones periapicales asociadas.","DiagnosticoPrincipal":"K02.1 - Caries de la dentina","DiagnosticoSecundario":"","EvolucionClinica":"Paciente acude a consulta por primera vez presentando dolor en cuadrante 4. Se realiza anamnesis detallada y examen cl\u00EDnico intraoral. Se evidenci\u00F3 caries profunda en pieza 46. Se realizaron pruebas de vitalidad pulpar t\u00E9rmica (positiva transitoria aumentada) y percusi\u00F3n vertical (negativa). Se procedi\u00F3 a la toma de radiograf\u00EDa periapical intraoral. Se explica el plan de tratamiento al paciente, quien firma el consentimiento informado. Se agenda pr\u00F3xima cita para remoci\u00F3n de caries y restauraci\u00F3n.","Prescripcion":"Ibuprofeno 400 mg tabletas: Tomar 1 tableta v\u00EDa oral cada 8 horas por 3 d\u00EDas \u00FAnicamente en caso de presentar dolor moderado, preferiblemente con alimentos.\\n\\nInstrucciones: Mantener una rigurosa higiene oral mediante cepillado meticuloso despu\u00E9s de cada comida, incorporar el uso diario de hilo dental y evitar alimentos excesivamente duros, pegajosos o temperaturas extremas en el lado derecho hasta realizar la restauraci\u00F3n definitiva."}}', 1)
SET IDENTITY_INSERT [dbo].[Historia_Clinica] OFF
GO
SET IDENTITY_INSERT [dbo].[Horario_Profesional] ON

INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (106, 1, N'Lunes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (107, 1, N'Martes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (108, 1, N'Miercoles', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (109, 1, N'Jueves', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (110, 1, N'Viernes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (86, 3, N'Lunes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (87, 3, N'Martes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (88, 3, N'Miercoles', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (89, 3, N'Jueves', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (90, 3, N'Viernes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (111, 3, N'Sabado', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (71, 5, N'Lunes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (72, 5, N'Martes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (73, 5, N'Miercoles', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (74, 5, N'Jueves', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (75, 5, N'Viernes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (112, 5, N'Sabado', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (56, 6, N'Lunes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (57, 6, N'Martes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (58, 6, N'Miercoles', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (59, 6, N'Jueves', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (60, 6, N'Viernes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (113, 6, N'Sabado', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (96, 7, N'Lunes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (97, 7, N'Martes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (98, 7, N'Miercoles', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (99, 7, N'Jueves', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (100, 7, N'Viernes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (114, 7, N'Sabado', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (66, 8, N'Lunes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (67, 8, N'Martes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (68, 8, N'Miercoles', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (69, 8, N'Jueves', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (70, 8, N'Viernes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (115, 8, N'Sabado', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (61, 9, N'Lunes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (62, 9, N'Martes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (63, 9, N'Miercoles', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (64, 9, N'Jueves', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (65, 9, N'Viernes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (116, 9, N'Sabado', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (101, 10, N'Lunes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (102, 10, N'Martes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (103, 10, N'Miercoles', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (104, 10, N'Jueves', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (105, 10, N'Viernes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (117, 10, N'Sabado', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (81, 11, N'Lunes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (82, 11, N'Martes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (83, 11, N'Miercoles', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (84, 11, N'Jueves', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (85, 11, N'Viernes', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
INSERT [dbo].[Horario_Profesional] ([id_horario], [id_profesional], [dia_semana], [hora_inicio], [hora_fin], [activo]) VALUES (118, 11, N'Sabado', CAST(N'08:00:00' AS Time), CAST(N'17:00:00' AS Time), 1)
SET IDENTITY_INSERT [dbo].[Horario_Profesional] OFF
GO
SET IDENTITY_INSERT [dbo].[Menu] ON

INSERT [dbo].[Menu] ([id_menu], [nombre], [url], [icono], [orden], [id_menu_padre], [modulo], [activo]) VALUES (1, N'Inicio', N'/Publico/homepage.html', N'??', 1, NULL, N'publico', 1)
SET IDENTITY_INSERT [dbo].[Menu] OFF
GO
SET IDENTITY_INSERT [dbo].[Notificacion] ON

INSERT [dbo].[Notificacion] ([id_notificacion], [id_paciente], [id_cita], [tipo], [titulo], [contenido], [canal], [estado], [fecha_programada], [fecha_envio], [fecha_lectura], [intentos], [ultimo_error], [creada_en]) VALUES (1, 17, 31, N'confirmacion', N'Cita agendada', N'Tu cita fue agendada para el 23/09/2026 a las 09:00.', N'interno', N'pendiente', CAST(N'2026-09-22T22:17:48.9340592' AS DateTime2), NULL, NULL, 0, NULL, CAST(N'2026-09-22T22:17:48.9340979' AS DateTime2))
INSERT [dbo].[Notificacion] ([id_notificacion], [id_paciente], [id_cita], [tipo], [titulo], [contenido], [canal], [estado], [fecha_programada], [fecha_envio], [fecha_lectura], [intentos], [ultimo_error], [creada_en]) VALUES (2, 7, 34, N'confirmacion', N'Cita agendada', N'Tu cita fue agendada para el 25/09/2026 a las 09:00.', N'interno', N'pendiente', CAST(N'2026-09-23T17:01:35.4814451' AS DateTime2), NULL, NULL, 0, NULL, CAST(N'2026-09-23T17:01:35.4814867' AS DateTime2))
INSERT [dbo].[Notificacion] ([id_notificacion], [id_paciente], [id_cita], [tipo], [titulo], [contenido], [canal], [estado], [fecha_programada], [fecha_envio], [fecha_lectura], [intentos], [ultimo_error], [creada_en]) VALUES (6, 1, 17, N'factura', N'Factura creada', N'Se creó la factura FAC-2026-00004 por $ 80.000,00.', N'interno', N'pendiente', NULL, NULL, NULL, 0, NULL, CAST(N'2026-10-27T17:15:25.2027345' AS DateTime2))
INSERT [dbo].[Notificacion] ([id_notificacion], [id_paciente], [id_cita], [tipo], [titulo], [contenido], [canal], [estado], [fecha_programada], [fecha_envio], [fecha_lectura], [intentos], [ultimo_error], [creada_en]) VALUES (7, 2, NULL, N'factura', N'Pago pendiente', N'La factura FAC-2026-002 por $ 850.000,00 presenta un saldo pendiente desde el 08/08/2026. Por favor acércate a la clínica para regularizar tu pago.', N'interno', N'pendiente', NULL, NULL, NULL, 0, NULL, CAST(N'2026-10-28T16:46:48.8336228' AS DateTime2))
INSERT [dbo].[Notificacion] ([id_notificacion], [id_paciente], [id_cita], [tipo], [titulo], [contenido], [canal], [estado], [fecha_programada], [fecha_envio], [fecha_lectura], [intentos], [ultimo_error], [creada_en]) VALUES (8, 2, NULL, N'factura', N'Pago pendiente', N'La factura FAC-2026-002 por $ 850.000,00 presenta un saldo pendiente desde el 08/08/2026. Por favor acércate a la clínica para regularizar tu pago.', N'interno', N'pendiente', NULL, NULL, NULL, 0, NULL, CAST(N'2026-10-29T17:18:13.8573378' AS DateTime2))
INSERT [dbo].[Notificacion] ([id_notificacion], [id_paciente], [id_cita], [tipo], [titulo], [contenido], [canal], [estado], [fecha_programada], [fecha_envio], [fecha_lectura], [intentos], [ultimo_error], [creada_en]) VALUES (9, 3, NULL, N'factura', N'Factura pagada', N'Se creó la factura FAC-2026-00005 por $ 80.000,00 y se registró un pago de $ 80.000,00.', N'interno', N'pendiente', NULL, NULL, NULL, 0, NULL, CAST(N'2026-10-29T17:49:03.5242808' AS DateTime2))
INSERT [dbo].[Notificacion] ([id_notificacion], [id_paciente], [id_cita], [tipo], [titulo], [contenido], [canal], [estado], [fecha_programada], [fecha_envio], [fecha_lectura], [intentos], [ultimo_error], [creada_en]) VALUES (10, 3, 35, N'confirmacion', N'Cita agendada', N'Tu cita fue agendada para el 04/11/2026 a las 09:00.', N'interno', N'pendiente', CAST(N'2026-11-03T21:12:30.3348645' AS DateTime2), NULL, NULL, 0, NULL, CAST(N'2026-11-03T21:12:30.3349895' AS DateTime2))
SET IDENTITY_INSERT [dbo].[Notificacion] OFF
GO
SET IDENTITY_INSERT [dbo].[Paciente] ON

INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (1, NULL, N'CC', N'10239485', N'Julián', N'Restrepo', CAST(N'1995-04-12T00:00:00.0000000' AS DateTime2), N'M', NULL, N'julian@ejemplo.com', NULL, NULL, NULL, NULL, NULL, NULL, NULL, CAST(N'2026-07-31T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (2, NULL, N'CC', N'52109432', N'Lucía', N'Torres', CAST(N'1990-08-25T00:00:00.0000000' AS DateTime2), N'F', NULL, N'lucia@ejemplo.com', NULL, NULL, NULL, NULL, NULL, NULL, NULL, CAST(N'2026-07-31T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (3, NULL, N'CC', N'88764321', N'Mariana', N'Esparza', CAST(N'1985-11-30T00:00:00.0000000' AS DateTime2), N'F', NULL, N'mariana@ejemplo.com', NULL, NULL, NULL, NULL, NULL, NULL, NULL, CAST(N'2026-07-31T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (4, NULL, N'CC', N'11098452', N'Sebastián', N'Correa', CAST(N'2002-02-14T00:00:00.0000000' AS DateTime2), N'M', NULL, N'sebastian@ejemplo.com', NULL, NULL, NULL, NULL, NULL, NULL, NULL, CAST(N'2026-07-31T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (5, 2, N'CC', N'0000000001', N'Paciente', N'Prueba', CAST(N'2001-08-04T00:00:00.0000000' AS DateTime2), N'M', NULL, N'pac@smiletrack.co', NULL, NULL, NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (6, NULL, N'CC', N'10345678', N'Andrea', N'Gomez', CAST(N'1998-06-03T00:00:00.0000000' AS DateTime2), N'F', N'3005799284', N'andrea.gomez@ejemplo.com', NULL, N'Bogota', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (7, NULL, N'CC', N'10456789', N'Camilo', N'Vargas', CAST(N'1993-09-21T00:00:00.0000000' AS DateTime2), N'M', N'3002515398', N'camilo.vargas@ejemplo.com', NULL, N'Bucaramanga', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (8, NULL, N'CC', N'10567890', N'Valentina', N'Rios', CAST(N'2010-01-17T00:00:00.0000000' AS DateTime2), N'F', N'3001504685', N'valentina.rios@ejemplo.com', NULL, N'Bogota', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (9, NULL, N'CC', N'10678901', N'Santiago', N'Pena', CAST(N'1988-12-05T00:00:00.0000000' AS DateTime2), N'M', N'3009139542', N'santiago.pena@ejemplo.com', NULL, N'Cali', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (10, NULL, N'CC', N'10789012', N'Isabella', N'Suarez', CAST(N'2015-03-09T00:00:00.0000000' AS DateTime2), N'F', N'3009652848', N'isabella.suarez@ejemplo.com', NULL, N'Medellin', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (11, NULL, N'CC', N'10890123', N'Nicolas', N'Cardenas', CAST(N'1979-07-22T00:00:00.0000000' AS DateTime2), N'M', N'3009694573', N'nicolas.cardenas@ejemplo.com', NULL, N'Bogota', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (12, NULL, N'CC', N'10901234', N'Daniela', N'Morales', CAST(N'2001-10-11T00:00:00.0000000' AS DateTime2), N'F', N'3003904562', N'daniela.morales@ejemplo.com', NULL, N'Barranquilla', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (13, NULL, N'CC', N'11012345', N'Felipe', N'Ortiz', CAST(N'1996-05-27T00:00:00.0000000' AS DateTime2), N'M', N'3005385833', N'felipe.ortiz@ejemplo.com', NULL, N'Bogota', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (14, NULL, N'CC', N'11123456', N'Gabriela', N'Munoz', CAST(N'1992-02-02T00:00:00.0000000' AS DateTime2), N'F', N'3009635509', N'gabriela.munoz@ejemplo.com', NULL, N'Cali', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (15, NULL, N'CC', N'11234567', N'Tomas', N'Herrera', CAST(N'1983-08-14T00:00:00.0000000' AS DateTime2), N'M', N'3009039383', N'tomas.herrera@ejemplo.com', NULL, N'Medellin', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (16, NULL, N'CC', N'11345678', N'Paula', N'Jimenez', CAST(N'2005-04-30T00:00:00.0000000' AS DateTime2), N'F', N'3004455543', N'paula.jimenez@ejemplo.com', NULL, N'Bogota', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
INSERT [dbo].[Paciente] ([id_paciente], [id_usuario], [tipo_documento], [documento], [nombres], [apellidos], [fecha_nacimiento], [genero], [telefono], [correo], [direccion], [ciudad], [grupo_sanguineo], [alergias], [antecedentes_medicos], [contacto_emergencia], [telefono_emergencia], [fecha_registro], [estado], [archivo_adjunto], [medicamentos], [estado_civil], [departamento], [parentesco_emergencia], [eps_aseguradora], [numero_poliza], [tipo_afiliacion], [medicamentos_actuales], [antecedentes_familiares], [id_profesional_asignado]) VALUES (17, 7, N'CC', N'1031423248', N'Samuel', N'Torres Acero', CAST(N'2006-09-15T00:00:00.0000000' AS DateTime2), N'M', N'3208209895', N'samuel1050torres@gmail.com', NULL, N'Bogota', NULL, NULL, NULL, NULL, NULL, CAST(N'2026-09-15T00:00:00.0000000' AS DateTime2), N'activo', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
SET IDENTITY_INSERT [dbo].[Paciente] OFF
GO
SET IDENTITY_INSERT [dbo].[Pago] ON

INSERT [dbo].[Pago] ([id_pago], [id_factura], [monto], [metodo_pago], [referencia_pago], [observaciones], [fecha_pago], [registrado_por], [clave_idempotencia], [referencia]) VALUES (1, 8, CAST(80000.00 AS Decimal(12, 2)), N'efectivo', NULL, NULL, CAST(N'2026-10-29T17:49:03.200' AS DateTime), 4, N'a35087e6-d558-4bd9-b307-3a18e818a951', N'CAJ-1d5da336efee468ea0d10f5c2a149e03')
INSERT [dbo].[Pago] ([id_pago], [id_factura], [monto], [metodo_pago], [referencia_pago], [observaciones], [fecha_pago], [registrado_por], [clave_idempotencia], [referencia]) VALUES (3, 8, CAST(80000.00 AS Decimal(12, 2)), N'efectivo', NULL, N'Migrado desde Pago_Factura (método original: historico; estado: migrado).', CAST(N'2026-10-29T17:49:03.200' AS DateTime), 4, NULL, N'LEGACY-8')
SET IDENTITY_INSERT [dbo].[Pago] OFF
GO
SET IDENTITY_INSERT [dbo].[Pago_Factura] ON

INSERT [dbo].[Pago_Factura] ([id_pago], [id_factura], [monto], [metodo_pago], [estado], [referencia], [registrado_por], [fecha_utc], [observaciones]) VALUES (1, 8, CAST(80000.00 AS Decimal(12, 2)), N'historico', N'migrado', N'LEGACY-8', 4, CAST(N'2026-10-29T17:49:03.2000000' AS DateTime2), NULL)
SET IDENTITY_INSERT [dbo].[Pago_Factura] OFF
GO
SET IDENTITY_INSERT [dbo].[PQR] ON

INSERT [dbo].[PQR] ([id_pqr], [id_paciente], [id_usuario], [tipo], [asunto], [descripcion], [estado], [prioridad], [fecha_creacion], [fecha_respuesta], [respuesta], [atendida_por], [evidencia_adjunto]) VALUES (1, 5, 2, N'peticion', N'demora en la atencion', N'demora en la atencion', N'rechazada', N'media', CAST(N'2026-09-22T16:52:42.840' AS DateTime), CAST(N'2026-09-22T18:28:06.680' AS DateTime), N'solicitud rechazada', 1, NULL)
INSERT [dbo].[PQR] ([id_pqr], [id_paciente], [id_usuario], [tipo], [asunto], [descripcion], [estado], [prioridad], [fecha_creacion], [fecha_respuesta], [respuesta], [atendida_por], [evidencia_adjunto]) VALUES (2, 5, 2, N'peticion', N'PREGUNTA', N'realizan tratamiendos los fines de semana', N'resuelta', N'media', CAST(N'2026-09-23T11:20:57.213' AS DateTime), CAST(N'2026-09-23T11:24:48.650' AS DateTime), N'no se realizan tratamiento los fines de semana solamente trabajamos de lunes a viernes en horario de 8am a 5 pm', 1, NULL)
SET IDENTITY_INSERT [dbo].[PQR] OFF
GO
SET IDENTITY_INSERT [dbo].[Profesional] ON

INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (1, 3, N'Profesional', N'SmileTrack', N'REG-PROF-001', NULL, N'Odontólogo General', N'3100000001', N'activo', CAST(N'2026-09-22T00:00:00.0000000' AS DateTime2), 1)
INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (2, NULL, N'Elena', N'Sotelo', N'RM-002', NULL, N'Ortodoncista', N'3100000002', N'activo', CAST(N'2026-07-31T00:00:00.0000000' AS DateTime2), 1)
INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (3, NULL, N'Carlos', N'Ruiz', N'RM-003', NULL, N'Endodoncista', N'3100000003', N'activo', CAST(N'2026-07-31T00:00:00.0000000' AS DateTime2), 1)
INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (4, 1, N'Dr. Juan', N'Smith', N'SMT-001', N'Especialista en ortodoncia', N'Ortodoncia', N'3001234567', N'activo', CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), 1)
INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (5, NULL, N'Veronica', N'Lozano', N'RM-004', N'Especialista en periodoncia', N'Periodoncista', N'3101179762', N'activo', CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), 1)
INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (6, NULL, N'Andres', N'Beltran', N'RM-005', N'Especialista en cirugia oral', N'Cirujano Oral', N'3109348746', N'activo', CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), 1)
INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (7, NULL, N'Monica', N'Salazar', N'RM-006', N'Especialista en odontopediatria', N'Odontopediatra', N'3102516635', N'activo', CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), 1)
INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (8, NULL, N'Diego', N'Fajardo', N'RM-007', N'Especialista en rehabilitacion oral y estetica dental', N'Rehabilitador Oral', N'3104906251', N'activo', CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), 1)
INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (9, NULL, N'Laura', N'Cifuentes', N'RM-008', N'Especialista en odontologia general', N'Odontologa General', N'3105957012', N'activo', CAST(N'2026-08-04T00:00:00.0000000' AS DateTime2), 1)
INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (10, 6, N'Johan', N'Santamaria', N'RM-020', NULL, N'Endodoncia', N'3123627335', N'activo', CAST(N'2026-08-28T00:00:00.0000000' AS DateTime2), 1)
INSERT [dbo].[Profesional] ([id_profesional], [id_usuario], [nombres], [apellidos], [registro_medico], [descripcion], [categoria], [telefono], [estado], [fecha_ingreso], [credenciales_aprobadas]) VALUES (11, 8, N'Carolina', N'Pérez Ríos', N'RM-12345678-CB', N'Ortodoncia y estética dental', N'Odontóloga Especialista', N'310-1234567', N'activo', CAST(N'2026-09-22T00:52:12.8966667' AS DateTime2), 1)
SET IDENTITY_INSERT [dbo].[Profesional] OFF
GO
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (1, 1, 1)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (2, 2, 1)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (3, 3, 1)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (4, 2, 1)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (5, 4, 1)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (6, 6, 1)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (7, 7, 1)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (8, 5, 1)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (9, 1, 1)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (10, 3, 1)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (11, 5, 0)
INSERT [dbo].[Profesional_Especialidad] ([id_profesional], [id_especialidad], [principal]) VALUES (11, 2, 1)
GO
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (2, 3, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (3, 4, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (4, 3, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (5, 16, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (6, 5, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (6, 12, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (7, 6, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (8, 2, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (8, 10, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (9, 1, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (9, 9, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (9, 11, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (10, 4, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (11, 2, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (11, 3, NULL, 1)
INSERT [dbo].[Profesional_Servicio] ([id_profesional], [id_servicio], [precio_personalizado], [activo]) VALUES (11, 10, NULL, 1)
GO
SET IDENTITY_INSERT [dbo].[Rol] ON

INSERT [dbo].[Rol] ([id_rol], [nombre_rol], [descripcion]) VALUES (1, N'Administrador', N'Acceso total')
INSERT [dbo].[Rol] ([id_rol], [nombre_rol], [descripcion]) VALUES (2, N'Profesional', N'Gestión clínica')
INSERT [dbo].[Rol] ([id_rol], [nombre_rol], [descripcion]) VALUES (3, N'Paciente', N'Paciente de la clínica')
INSERT [dbo].[Rol] ([id_rol], [nombre_rol], [descripcion]) VALUES (4, N'Recepcionista', N'Gestión de citas')
INSERT [dbo].[Rol] ([id_rol], [nombre_rol], [descripcion]) VALUES (5, N'Auxiliar', N'Apoyo clínico')
SET IDENTITY_INSERT [dbo].[Rol] OFF
GO
SET IDENTITY_INSERT [dbo].[Servicio] ON

INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (1, N'Limpieza dental', N'Elimina el sarro y placa bacteriana', CAST(80000.00 AS Decimal(12, 2)), N'activo', N'general', 30, 1)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (2, N'Blanqueamiento', N'Recupera el brillo natural', CAST(350000.00 AS Decimal(12, 2)), N'activo', N'general', 30, 5)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (3, N'Ortodoncia', N'Corrección de la posición dental', CAST(200000.00 AS Decimal(12, 2)), N'activo', N'general', 30, 2)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (4, N'Endodoncia', N'Tratamiento de conductos', CAST(450000.00 AS Decimal(12, 2)), N'activo', N'general', 30, 3)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (5, N'Implantes', N'Reemplaza piezas perdidas', CAST(2800000.00 AS Decimal(12, 2)), N'activo', N'general', 30, 6)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (6, N'Odontopediatría', N'Cuidado dental especializado para niños', CAST(60000.00 AS Decimal(12, 2)), N'activo', N'general', 30, 7)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (7, N'Implante dental', N'Reemplazo de piezas perdidas', CAST(2800000.00 AS Decimal(12, 2)), N'inactivo', N'general', 30, NULL)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (8, N'Odontopediatria', N'Cuidado dental especializado para ninos', CAST(60000.00 AS Decimal(12, 2)), N'inactivo', N'general', 30, NULL)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (9, N'Extraccion simple', N'Extraccion de pieza dental sin complicaciones', CAST(120000.00 AS Decimal(12, 2)), N'activo', N'general', 30, 1)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (10, N'Resina dental', N'Restauracion estetica de una pieza danada', CAST(90000.00 AS Decimal(12, 2)), N'activo', N'general', 30, 5)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (11, N'Profilaxis y fluor', N'Prevencion de caries en ninos y adultos', CAST(70000.00 AS Decimal(12, 2)), N'activo', N'general', 30, 1)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (12, N'Cirugia de terceros molares', N'Extraccion quirurgica de muelas del juicio', CAST(650000.00 AS Decimal(12, 2)), N'activo', N'general', 30, 6)
INSERT [dbo].[Servicio] ([id_servicio], [nombre], [descripcion], [precio], [estado], [categoria], [duracion_minutos], [id_especialidad]) VALUES (16, N'Raspado y alisado radicular', N'Procedimiento periodontal para eliminar placa y cálculo dental.', CAST(50000.00 AS Decimal(12, 2)), N'activo', N'periodoncia', 60, 4)
SET IDENTITY_INSERT [dbo].[Servicio] OFF
GO
SET IDENTITY_INSERT [dbo].[Usuario] ON

INSERT [dbo].[Usuario] ([id_usuario], [creado_por], [nombre], [apellidos], [correo], [contrasena], [id_rol], [estado], [fecha_nacimiento], [fecha_creacion], [ultimo_login], [codigo_recuperacion], [fecha_expiracion_codigo], [intentos_fallidos], [ultimo_logout]) VALUES (1, NULL, N'Admin', N'SmileTrack', N'admin@smiletrack.co', N'$2a$11$CxlqXT8AY0OmVg5fY6b32.trpz8VVsRboiWbWc1HEvnTUS7CgLiYu', 1, N'activo', NULL, CAST(N'2026-07-31T18:20:59.5965123' AS DateTime2), CAST(N'2026-11-12T23:03:34.6371687' AS DateTime2), NULL, NULL, 0, NULL)
INSERT [dbo].[Usuario] ([id_usuario], [creado_por], [nombre], [apellidos], [correo], [contrasena], [id_rol], [estado], [fecha_nacimiento], [fecha_creacion], [ultimo_login], [codigo_recuperacion], [fecha_expiracion_codigo], [intentos_fallidos], [ultimo_logout]) VALUES (2, NULL, N'Paciente', N'Prueba', N'pac@smiletrack.co', N'$2a$11$IGClrrWCDp8XI4JvYLRVDOpYyjPC/Ozy/GiSAUcsnYKGj1H3jyTm2', 3, N'activo', CAST(N'2001-08-04T00:00:00.0000000' AS DateTime2), CAST(N'2026-07-31T18:21:00.0439488' AS DateTime2), CAST(N'2026-11-12T22:00:01.0158158' AS DateTime2), NULL, NULL, 0, NULL)
INSERT [dbo].[Usuario] ([id_usuario], [creado_por], [nombre], [apellidos], [correo], [contrasena], [id_rol], [estado], [fecha_nacimiento], [fecha_creacion], [ultimo_login], [codigo_recuperacion], [fecha_expiracion_codigo], [intentos_fallidos], [ultimo_logout]) VALUES (3, NULL, N'Profesional', N'Prueba', N'prof@smiletrack.co', N'$2a$11$geEMj1YZouopUilhlDFQJeHdi7TOjdDPATkoii3X4gC/UOlXNgHvO', 2, N'activo', NULL, CAST(N'2026-07-31T18:21:00.4388417' AS DateTime2), CAST(N'2026-11-12T23:04:49.1198969' AS DateTime2), NULL, NULL, 0, NULL)
INSERT [dbo].[Usuario] ([id_usuario], [creado_por], [nombre], [apellidos], [correo], [contrasena], [id_rol], [estado], [fecha_nacimiento], [fecha_creacion], [ultimo_login], [codigo_recuperacion], [fecha_expiracion_codigo], [intentos_fallidos], [ultimo_logout]) VALUES (4, NULL, N'Recepcionista', N'Prueba', N'recep@smiletrack.co', N'$2a$11$sgPzn841ma5rgYGL8XKSPOJSFE1lgIuXMYjZX9oegvu02ov994Lvi', 4, N'activo', NULL, CAST(N'2026-07-31T18:21:00.8298154' AS DateTime2), CAST(N'2026-11-12T22:46:44.1435858' AS DateTime2), NULL, NULL, 0, NULL)
INSERT [dbo].[Usuario] ([id_usuario], [creado_por], [nombre], [apellidos], [correo], [contrasena], [id_rol], [estado], [fecha_nacimiento], [fecha_creacion], [ultimo_login], [codigo_recuperacion], [fecha_expiracion_codigo], [intentos_fallidos], [ultimo_logout]) VALUES (5, NULL, N'Auxiliar', N'Prueba', N'aux@smiletrack.co', N'$2a$11$fB8BLCL3kqK1NnFA.ER7ruSnKPz3UQ4pwtChKWKoI4ETt8OlWBGjy', 5, N'activo', NULL, CAST(N'2026-07-31T18:21:01.0926010' AS DateTime2), CAST(N'2026-11-12T22:12:32.6320754' AS DateTime2), NULL, NULL, 0, NULL)
INSERT [dbo].[Usuario] ([id_usuario], [creado_por], [nombre], [apellidos], [correo], [contrasena], [id_rol], [estado], [fecha_nacimiento], [fecha_creacion], [ultimo_login], [codigo_recuperacion], [fecha_expiracion_codigo], [intentos_fallidos], [ultimo_logout]) VALUES (6, 1, N'Johan', N'Santamaria', N'johanmauricio2007@gmail.com', N'$2a$11$WQ0./4kBZMbmONYEBh7uw.ShIgQ3.suzRHNuBWUum88.U0eQgUS3K', 2, N'activo', NULL, CAST(N'2026-08-28T18:32:15.2134103' AS DateTime2), NULL, NULL, NULL, 0, NULL)
INSERT [dbo].[Usuario] ([id_usuario], [creado_por], [nombre], [apellidos], [correo], [contrasena], [id_rol], [estado], [fecha_nacimiento], [fecha_creacion], [ultimo_login], [codigo_recuperacion], [fecha_expiracion_codigo], [intentos_fallidos], [ultimo_logout]) VALUES (7, NULL, N'Samuel', N'Torres Acero', N'samuel1050torres@gmail.com', N'$2a$11$y6dXZoaTXrCx6JcdCIF/gOkPD9pYkW1bc6E9LHG4wIzZCGmUF9n5e', 3, N'activo', NULL, CAST(N'2026-09-15T21:37:40.0797850' AS DateTime2), CAST(N'2026-11-12T23:15:47.7008253' AS DateTime2), NULL, NULL, 0, NULL)
INSERT [dbo].[Usuario] ([id_usuario], [creado_por], [nombre], [apellidos], [correo], [contrasena], [id_rol], [estado], [fecha_nacimiento], [fecha_creacion], [ultimo_login], [codigo_recuperacion], [fecha_expiracion_codigo], [intentos_fallidos], [ultimo_logout]) VALUES (8, NULL, N'Carolina', N'Pérez Ríos', N'dra.perez@smiletrack.co', N'$2a$11$u.Lp05p02n3H8i1j/3CgkuM9Vl8y7D2pXfG7zT66.qG4q/3.X9G1a', 2, N'activo', CAST(N'1988-05-22T00:00:00.0000000' AS DateTime2), CAST(N'2026-09-22T00:52:12.8766667' AS DateTime2), NULL, NULL, NULL, 0, NULL)
INSERT [dbo].[Usuario] ([id_usuario], [creado_por], [nombre], [apellidos], [correo], [contrasena], [id_rol], [estado], [fecha_nacimiento], [fecha_creacion], [ultimo_login], [codigo_recuperacion], [fecha_expiracion_codigo], [intentos_fallidos], [ultimo_logout]) VALUES (9, 1, N'Nestor', N'Montaño', N'nestormontaño@smiletrack.co', N'$2a$11$0Jk3adJHXKcbON3x3ScSdu6ndm/b8wtZjm4lXf3OWpXf0sF4AGbf.', 2, N'inactivo', NULL, CAST(N'2026-09-23T16:07:07.7703870' AS DateTime2), NULL, NULL, NULL, 0, NULL)
SET IDENTITY_INSERT [dbo].[Usuario] OFF
GO
SET IDENTITY_INSERT [dbo].[UsuariosPreferenciasConsultorio] ON

INSERT [dbo].[UsuariosPreferenciasConsultorio] ([id_preferencia], [id_usuario], [id_consultorio], [actualizado_en]) VALUES (1, 5, 8, CAST(N'2026-11-10T19:27:31.0665956' AS DateTime2))
SET IDENTITY_INSERT [dbo].[UsuariosPreferenciasConsultorio] OFF
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
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_Paciente_TipoDocumento_Documento] ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Paciente_TipoDocumento_Documento] ON [dbo].[Paciente]
(
	[tipo_documento] ASC,
	[documento] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
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
ALTER TABLE dbo.PQR ADD CONSTRAINT FK_PQR_Profesional_Soporte
    FOREIGN KEY (id_profesional) REFERENCES dbo.Profesional (id_profesional) ON DELETE SET NULL;
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
ALTER TABLE [dbo].[Notificacion]  WITH CHECK ADD  CONSTRAINT [CK_Notificacion_tipo] CHECK  (([tipo]='pago' OR [tipo]='factura' OR [tipo]='alerta' OR [tipo]='mensaje' OR [tipo]='cancelacion' OR [tipo]='confirmacion' OR [tipo]='recordatorio' OR [tipo]='cambio_estado'))
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
IF COL_LENGTH(N'dbo.Profesional', N'tarjeta_profesional') IS NULL
    ALTER TABLE [dbo].[Profesional] ADD [tarjeta_profesional] [nvarchar](50) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_Profesional_TarjetaProfesional' AND [object_id] = OBJECT_ID(N'dbo.Profesional'))
    CREATE UNIQUE INDEX [UX_Profesional_TarjetaProfesional] ON [dbo].[Profesional]([tarjeta_profesional]) WHERE [tarjeta_profesional] IS NOT NULL;
GO
IF COL_LENGTH(N'dbo.Cita', N'id_profesional_preferido') IS NULL
    ALTER TABLE [dbo].[Cita] ADD [id_profesional_preferido] [int] NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Cita_ProfesionalPreferido' AND [parent_object_id] = OBJECT_ID(N'dbo.Cita'))
    ALTER TABLE [dbo].[Cita] WITH CHECK ADD CONSTRAINT [FK_Cita_ProfesionalPreferido] FOREIGN KEY ([id_profesional_preferido]) REFERENCES [dbo].[Profesional]([id_profesional]) ON DELETE NO ACTION;
GO
IF OBJECT_ID(N'dbo.Lista_Espera_Cita', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Lista_Espera_Cita](
        [id_lista_espera] [int] IDENTITY(1,1) NOT NULL,
        [id_paciente] [int] NOT NULL,
        [id_servicio] [int] NULL,
        [id_profesional_preferido] [int] NULL,
        [id_cita_asignada] [int] NULL,
        [fecha_deseada_inicio] [datetime2](7) NOT NULL,
        [fecha_deseada_fin] [datetime2](7) NOT NULL,
        [estado] [nvarchar](20) NOT NULL CONSTRAINT [DF_Lista_Espera_Cita_estado] DEFAULT (N'pendiente'),
        [fecha_registro] [datetime2](7) NOT NULL CONSTRAINT [DF_Lista_Espera_Cita_fecha_registro] DEFAULT (SYSUTCDATETIME()),
        [notas] [nvarchar](500) NULL,
        CONSTRAINT [PK_Lista_Espera_Cita] PRIMARY KEY CLUSTERED ([id_lista_espera]),
        CONSTRAINT [CK_Lista_Espera_Cita_fechas] CHECK ([fecha_deseada_fin] >= [fecha_deseada_inicio]),
        CONSTRAINT [FK_Lista_Espera_Cita_Paciente] FOREIGN KEY ([id_paciente]) REFERENCES [dbo].[Paciente]([id_paciente]) ON DELETE CASCADE,
        CONSTRAINT [FK_Lista_Espera_Cita_Servicio] FOREIGN KEY ([id_servicio]) REFERENCES [dbo].[Servicio]([id_servicio]) ON DELETE SET NULL,
        CONSTRAINT [FK_Lista_Espera_Cita_Profesional] FOREIGN KEY ([id_profesional_preferido]) REFERENCES [dbo].[Profesional]([id_profesional]) ON DELETE SET NULL,
        CONSTRAINT [FK_Lista_Espera_Cita_Cita] FOREIGN KEY ([id_cita_asignada]) REFERENCES [dbo].[Cita]([id_cita]) ON DELETE SET NULL
    );
END;
GO
IF COL_LENGTH(N'dbo.Lista_Espera_Cita', N'id_cita_asignada') IS NULL
    ALTER TABLE [dbo].[Lista_Espera_Cita] ADD [id_cita_asignada] [int] NULL;
GO

IF COL_LENGTH(N'dbo.Lista_Espera_Cita', N'id_profesional_preferido') IS NULL
    ALTER TABLE [dbo].[Lista_Espera_Cita] ADD [id_profesional_preferido] [int] NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Lista_Espera_Cita_Profesional' AND [parent_object_id] = OBJECT_ID(N'dbo.Lista_Espera_Cita'))
    ALTER TABLE [dbo].[Lista_Espera_Cita] WITH CHECK ADD CONSTRAINT [FK_Lista_Espera_Cita_Profesional] FOREIGN KEY ([id_profesional_preferido]) REFERENCES [dbo].[Profesional]([id_profesional]) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Lista_Espera_Cita_Cita' AND [parent_object_id] = OBJECT_ID(N'dbo.Lista_Espera_Cita'))
    ALTER TABLE [dbo].[Lista_Espera_Cita] WITH CHECK ADD CONSTRAINT [FK_Lista_Espera_Cita_Cita] FOREIGN KEY ([id_cita_asignada]) REFERENCES [dbo].[Cita]([id_cita]) ON DELETE SET NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ListaEspera_Estado_Franja' AND [object_id] = OBJECT_ID(N'dbo.Lista_Espera_Cita'))
    CREATE INDEX [IX_ListaEspera_Estado_Franja] ON [dbo].[Lista_Espera_Cita]([estado], [fecha_deseada_inicio], [fecha_deseada_fin]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_ListaEspera_CitaAsignada' AND [object_id] = OBJECT_ID(N'dbo.Lista_Espera_Cita'))
    CREATE UNIQUE INDEX [UX_ListaEspera_CitaAsignada] ON [dbo].[Lista_Espera_Cita]([id_cita_asignada]) WHERE [id_cita_asignada] IS NOT NULL;
GO
DECLARE @EstadosBrechas TABLE ([nombre_estado] [nvarchar](50) NOT NULL);
INSERT INTO @EstadosBrechas ([nombre_estado]) VALUES
    (N'Solicitada'), (N'Programada'), (N'Confirmada'),
    (N'En sala de espera'), (N'En atención'), (N'Atendida'),
    (N'Cancelada'), (N'No asistió'), (N'Rechazada');
INSERT INTO [dbo].[Estado_Cita] ([nombre_estado])
SELECT e.[nombre_estado] FROM @EstadosBrechas e
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Estado_Cita] actual WHERE LOWER(actual.[nombre_estado]) = LOWER(e.[nombre_estado]));
GO
USE [master]
GO
ALTER DATABASE [SmileTrackDB] SET  READ_WRITE
GO

IF OBJECT_ID(N'dbo.PQR_Aviso_Leido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PQR_Aviso_Leido (
        id_usuario int NOT NULL,
        clave nvarchar(64) NOT NULL,
        CONSTRAINT PK_PQR_Aviso_Leido PRIMARY KEY (id_usuario, clave),
        CONSTRAINT FK_PQR_Aviso_Leido_Usuario FOREIGN KEY (id_usuario)
            REFERENCES dbo.Usuario (id_usuario) ON DELETE CASCADE
    );
END;
GO
