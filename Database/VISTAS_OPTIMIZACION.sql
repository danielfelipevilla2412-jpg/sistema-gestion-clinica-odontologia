-- ============================================================
-- VISTAS_OPTIMIZACION.sql
-- Vistas SQL para optimizar consultas en Gestión de Citas y Profesionales
-- Proyecto: SmileTrack
-- Fecha: 2025-01-XX
-- ============================================================

USE [SmileTrackDB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '   CREACIÓN DE VISTAS DE OPTIMIZACIÓN - SMILETRACK';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
GO

-- ============================================================
-- VISTA 1: vw_Citas_Dashboard
-- Propósito: Optimizar consultas del dashboard de citas
-- Uso: CitaService, GestionCitasController
-- ============================================================

IF OBJECT_ID('dbo.vw_Citas_Dashboard', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Citas_Dashboard;
GO

CREATE VIEW dbo.vw_Citas_Dashboard
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

PRINT '✓ Vista vw_Citas_Dashboard creada exitosamente';
GO

-- ============================================================
-- VISTA 2: vw_Profesionales_Completo
-- Propósito: Optimizar listados de profesionales con relaciones
-- Uso: ProfesionalService, GestionProfesionalesController
-- ============================================================

IF OBJECT_ID('dbo.vw_Profesionales_Completo', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Profesionales_Completo;
GO

CREATE VIEW dbo.vw_Profesionales_Completo
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

PRINT '✓ Vista vw_Profesionales_Completo creada exitosamente';
GO

-- ============================================================
-- VISTA 3: vw_Disponibilidad_Profesional
-- Propósito: Consultar disponibilidad completa de profesionales
-- Uso: CitaService.ValidarDisponibilidadProfesionalAsync
-- ============================================================

IF OBJECT_ID('dbo.vw_Disponibilidad_Profesional', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Disponibilidad_Profesional;
GO

CREATE VIEW dbo.vw_Disponibilidad_Profesional
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

PRINT '✓ Vista vw_Disponibilidad_Profesional creada exitosamente';
GO

-- ============================================================
-- VISTA 4: vw_Citas_Conflictos
-- Propósito: Detectar solapamientos de agenda
-- Uso: CitaService.VerificarConflictoCompletoAsync
-- ============================================================

IF OBJECT_ID('dbo.vw_Citas_Conflictos', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Citas_Conflictos;
GO

CREATE VIEW dbo.vw_Citas_Conflictos
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

PRINT '✓ Vista vw_Citas_Conflictos creada exitosamente';
GO

-- ============================================================
-- VISTA 5: vw_KPI_Citas_Resumen
-- Propósito: Pre-calcular KPIs mensuales
-- Uso: CitasDashboardService, Reportes
-- ============================================================

IF OBJECT_ID('dbo.vw_KPI_Citas_Resumen', 'V') IS NOT NULL
    DROP VIEW dbo.vw_KPI_Citas_Resumen;
GO

CREATE VIEW dbo.vw_KPI_Citas_Resumen
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
    
    -- Servicios más demandados
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

PRINT '✓ Vista vw_KPI_Citas_Resumen creada exitosamente';
GO

-- ============================================================
-- ÍNDICES RECOMENDADOS PARA OPTIMIZAR VISTAS
-- ============================================================

PRINT '';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '   CREACIÓN DE ÍNDICES DE OPTIMIZACIÓN';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
GO

-- Índice 1: Citas por fecha y estado (usado por vw_Citas_Dashboard)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_FechaHora_Estado_Covering')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Cita_FechaHora_Estado_Covering
    ON Cita(fecha_hora, estado)
    INCLUDE (id_paciente, id_profesional, id_servicio, id_consultorio, duracion_minutos);
    
    PRINT '✓ Índice IX_Cita_FechaHora_Estado_Covering creado';
END
ELSE
    PRINT '○ Índice IX_Cita_FechaHora_Estado_Covering ya existe';
GO

-- Índice 2: Citas activas por profesional (usado por vw_Disponibilidad_Profesional)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_Profesional_FechaHora_NoCancel')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Cita_Profesional_FechaHora_NoCancel
    ON Cita(id_profesional, fecha_hora)
    WHERE estado NOT IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado');
    
    PRINT '✓ Índice IX_Cita_Profesional_FechaHora_NoCancel creado';
END
ELSE
    PRINT '○ Índice IX_Cita_Profesional_FechaHora_NoCancel ya existe';
GO

-- Índice 3: Horarios activos por profesional
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HorarioProfesional_IdProf_Activo_Covering')
BEGIN
    CREATE NONCLUSTERED INDEX IX_HorarioProfesional_IdProf_Activo_Covering
    ON Horario_Profesional(id_profesional, activo)
    INCLUDE (dia_semana, hora_inicio, hora_fin);
    
    PRINT '✓ Índice IX_HorarioProfesional_IdProf_Activo_Covering creado';
END
ELSE
    PRINT '○ Índice IX_HorarioProfesional_IdProf_Activo_Covering ya existe';
GO

-- Índice 4: Ausencias vigentes por profesional
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AusenciaProfesional_IdProf_Fechas_Covering')
BEGIN
    CREATE NONCLUSTERED INDEX IX_AusenciaProfesional_IdProf_Fechas_Covering
    ON Ausencia_Profesional(id_profesional, fecha_inicio, fecha_fin)
    INCLUDE (tipo, observaciones);
    
    PRINT '✓ Índice IX_AusenciaProfesional_IdProf_Fechas_Covering creado';
END
ELSE
    PRINT '○ Índice IX_AusenciaProfesional_IdProf_Fechas_Covering ya existe';
GO

-- Índice 5: Bloqueos activos por profesional
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BloqueoProfesional_IdProf_Fechas_Covering')
BEGIN
    CREATE NONCLUSTERED INDEX IX_BloqueoProfesional_IdProf_Fechas_Covering
    ON Bloqueo_Profesional(id_profesional, fecha_inicio, fecha_fin)
    INCLUDE (motivo);
    
    PRINT '✓ Índice IX_BloqueoProfesional_IdProf_Fechas_Covering creado';
END
ELSE
    PRINT '○ Índice IX_BloqueoProfesional_IdProf_Fechas_Covering ya existe';
GO

-- Índice 6: Profesional-Especialidad con principal
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProfesionalEspecialidad_Principal_Covering')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProfesionalEspecialidad_Principal_Covering
    ON Profesional_Especialidad(id_profesional, principal)
    INCLUDE (id_especialidad);
    
    PRINT '✓ Índice IX_ProfesionalEspecialidad_Principal_Covering creado';
END
ELSE
    PRINT '○ Índice IX_ProfesionalEspecialidad_Principal_Covering ya existe';
GO

-- Índice 7: Profesionales por estado
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Profesional_Estado_Covering')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Profesional_Estado_Covering
    ON Profesional(estado)
    INCLUDE (nombres, apellidos, registro_medico, id_usuario);
    
    PRINT '✓ Índice IX_Profesional_Estado_Covering creado';
END
ELSE
    PRINT '○ Índice IX_Profesional_Estado_Covering ya existe';
GO

-- Índice 8: Citas del día (optimización para dashboard diario)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_FechaSola_Covering')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Cita_FechaSola_Covering
    ON Cita(CAST(fecha_hora AS DATE))
    INCLUDE (id_profesional, id_paciente, id_consultorio, estado);
    
    PRINT '✓ Índice IX_Cita_FechaSola_Covering creado';
END
ELSE
    PRINT '○ Índice IX_Cita_FechaSola_Covering ya existe';
GO

-- ============================================================
-- ESTADÍSTICAS Y VERIFICACIÓN
-- ============================================================

PRINT '';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '   ACTUALIZACIÓN DE ESTADÍSTICAS';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
GO

-- Actualizar estadísticas de las tablas principales
UPDATE STATISTICS Cita WITH FULLSCAN;
UPDATE STATISTICS Profesional WITH FULLSCAN;
UPDATE STATISTICS Horario_Profesional WITH FULLSCAN;
UPDATE STATISTICS Ausencia_Profesional WITH FULLSCAN;
UPDATE STATISTICS Bloqueo_Profesional WITH FULLSCAN;

PRINT '✓ Estadísticas actualizadas para optimización de consultas';
GO

-- ============================================================
-- RESUMEN FINAL
-- ============================================================

PRINT '';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '   RESUMEN DE CREACIÓN';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '';
PRINT 'Vistas creadas:';
PRINT '  1. vw_Citas_Dashboard';
PRINT '  2. vw_Profesionales_Completo';
PRINT '  3. vw_Disponibilidad_Profesional';
PRINT '  4. vw_Citas_Conflictos';
PRINT '  5. vw_KPI_Citas_Resumen';
PRINT '';
PRINT 'Índices creados/verificados: 8';
PRINT '';
PRINT '✓ Script ejecutado exitosamente';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
GO
