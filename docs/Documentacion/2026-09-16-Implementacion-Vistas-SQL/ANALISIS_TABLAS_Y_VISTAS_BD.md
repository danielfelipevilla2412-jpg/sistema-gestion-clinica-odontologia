# 📊 Análisis de Tablas de Base de Datos - SmileTrack

## Fecha de Análisis
**Fecha**: 2025-01-XX  
**Analista**: Kiro AI  
**Módulos Analizados**: Gestión de Citas y Gestión de Profesionales

---

## 🎯 Resumen Ejecutivo

Este documento analiza exhaustivamente:
1. Qué tablas utilizan los módulos de **Gestión de Citas** y **Gestión de Profesionales**
2. Qué tablas existen en la base de datos pero NO se usan en estos módulos
3. Propuestas de **Vistas SQL** para optimizar consultas frecuentes

---

## 📋 1. MÓDULO: GESTIÓN DE CITAS

### 1.1 Tablas Utilizadas Directamente

#### Tablas Core (Uso Intensivo)
| Tabla | Propósito | Frecuencia de Uso |
|-------|-----------|-------------------|
| **`Cita`** | Almacena todas las citas médicas | 🔴 Muy Alta |
| **`Paciente`** | Información de pacientes | 🔴 Muy Alta |
| **`Profesional`** | Odontólogos y especialistas | 🔴 Muy Alta |
| **`Servicio`** | Tipos de servicios odontológicos | 🟡 Alta |
| **`Consultorio`** | Salas de atención | 🟡 Alta |
| **`Usuario`** | Usuarios del sistema | 🟡 Alta |

#### Tablas de Configuración
| Tabla | Propósito | Uso |
|-------|-----------|-----|
| **`Configuracion_General`** | Horarios, duración de citas, días de atención | 🟢 Media |
| **`Estado_Cita`** | Catálogo de estados (Agendada, Confirmada, etc.) | 🟢 Media |

#### Tablas de Validación y Disponibilidad
| Tabla | Propósito | Uso |
|-------|-----------|-----|
| **`Horario_Profesional`** | Horarios semanales de cada profesional | 🟡 Alta |
| **`Ausencia_Profesional`** | Vacaciones, incapacidades, permisos | 🟡 Alta |
| **`Bloqueo_Profesional`** | Bloqueos puntuales (juntas, emergencias) | 🟡 Alta |

#### Tablas de Notificaciones
| Tabla | Propósito | Uso |
|-------|-----------|-----|
| **`Notificacion`** | Notificaciones push para pacientes | 🟢 Media |
| **`Recordatorio_Cita`** | Sistema de recordatorios automatizados | 🟢 Media |

#### Tablas de Auditoría
| Tabla | Propósito | Uso |
|-------|-----------|-----|
| **`Cita_Historial_Estado`** | Historial de cambios de estado de citas | 🟢 Media |
| **`Auditoria`** | Registro de operaciones CRUD | 🟢 Media |

---

### 1.2 Servicios y Consultas Principales

#### `CitaService.cs` - Métodos Principales

```csharp
// 1. Validación de Disponibilidad
ValidarDisponibilidadProfesionalAsync()
  ├─ Horario_Profesional (horarios semanales)
  ├─ Ausencia_Profesional (vacaciones/permisos)
  ├─ Bloqueo_Profesional (bloqueos puntuales)
  └─ Configuracion_General (horario_apertura, horario_cierre)

// 2. Verificación de Conflictos
VerificarConflictoCompletoAsync()
  ├─ Cita (conflictos de profesional)
  ├─ Cita (conflictos de paciente)
  └─ Cita (conflictos de consultorio)

// 3. Creación de Citas
CrearAsync()
  ├─ Paciente (validar existencia)
  ├─ Profesional (validar existencia y estado)
  ├─ Servicio (obtener duración)
  ├─ Consultorio (validar disponibilidad)
  ├─ Cita (INSERT)
  ├─ Cita_Historial_Estado (registrar estado inicial)
  └─ Notificacion (crear notificación)

// 4. KPIs y Dashboard
ObtenerKpisGestionAsync()
  └─ Cita (agregaciones por estado y fecha)

// 5. Recordatorios
EnviarRecordatoriosAsync()
  ├─ Cita (citas próximas)
  ├─ Paciente (datos de contacto)
  ├─ Recordatorio_Cita (registro de envíos)
  └─ Notificacion (envío de notificaciones)
```

---

## 📋 2. MÓDULO: GESTIÓN DE PROFESIONALES

### 2.1 Tablas Utilizadas Directamente

#### Tablas Core
| Tabla | Propósito | Frecuencia de Uso |
|-------|-----------|-------------------|
| **`Profesional`** | Datos de profesionales | 🔴 Muy Alta |
| **`Usuario`** | Cuentas de acceso | 🔴 Muy Alta |
| **`Especialidad`** | Especialidades médicas | 🟡 Alta |
| **`Profesional_Especialidad`** | Relación N:N profesional-especialidad | 🟡 Alta |
| **`Rol`** | Rol "Profesional" | 🟡 Alta |

#### Tablas de Disponibilidad
| Tabla | Propósito | Uso |
|-------|-----------|-----|
| **`Horario_Profesional`** | Gestión de horarios semanales | 🔴 Muy Alta |
| **`Ausencia_Profesional`** | Registro de ausencias | 🟡 Alta |
| **`Bloqueo_Profesional`** | Bloqueos por desactivación | 🟡 Alta |

#### Tablas de Servicios
| Tabla | Propósito | Uso |
|-------|-----------|-----|
| **`Profesional_Servicio`** | Servicios que ofrece cada profesional | 🟢 Media |
| **`Servicio`** | Catálogo de servicios | 🟢 Media |

#### Tablas de Control
| Tabla | Propósito | Uso |
|-------|-----------|-----|
| **`Cita`** | Validar citas activas antes de desactivar | 🟡 Alta |
| **`Auditoria`** | Registro de operaciones | 🟢 Media |

---

### 2.2 Servicios y Consultas Principales

#### `ProfesionalService.cs` - Métodos Principales

```csharp
// 1. CRUD Profesionales
ObtenerAsync() / ObtenerPorIdAsync()
  ├─ Profesional
  ├─ Usuario (datos de acceso)
  └─ Profesional_Especialidad (con navegación a Especialidad)

CrearAsync()
  ├─ Rol (obtener rol "Profesional")
  ├─ Usuario (INSERT cuenta de acceso)
  ├─ Profesional (INSERT)
  ├─ Profesional_Especialidad (asignar especialidad)
  └─ Auditoria (registrar operación)

ActualizarAsync()
  ├─ Profesional (UPDATE)
  ├─ Usuario (UPDATE)
  ├─ Profesional_Especialidad (sincronizar especialidad)
  └─ Auditoria (registrar cambios)

// 2. Cambio de Estado
CambiarEstadoAsync()
  ├─ Profesional (UPDATE estado)
  ├─ Usuario (sincronizar estado cuenta)
  ├─ Cita (validar citas activas)
  ├─ Bloqueo_Profesional (INSERT/DELETE según estado)
  └─ Auditoria (registrar operación)

DesactivarAsync()
  ├─ Profesional (estado = 'inactivo')
  ├─ Usuario (estado = 'inactivo')
  ├─ Cita (validar citas pendientes)
  ├─ Bloqueo_Profesional (INSERT bloqueo permanente)
  └─ Auditoria (registrar baja lógica)

// 3. Gestión de Horarios
ObtenerHorariosAsync()
  └─ Horario_Profesional (SELECT)

ActualizarHorariosAsync()
  └─ Horario_Profesional (DELETE + INSERT)

// 4. Gestión de Ausencias
ObtenerAusenciasAsync()
  └─ Ausencia_Profesional (SELECT)

CrearAusenciaAsync()
  └─ Ausencia_Profesional (INSERT)

ActualizarAusenciaAsync()
  └─ Ausencia_Profesional (UPDATE)

EliminarAusenciaAsync()
  └─ Ausencia_Profesional (DELETE)

// 5. Servicios del Profesional
ObtenerServiciosAsync()
  ├─ Profesional_Servicio
  └─ Servicio (navegación)
```

---

## ❌ 3. TABLAS NO UTILIZADAS POR ESTOS MÓDULOS

### 3.1 Módulo de Historia Clínica
| Tabla | Propósito |
|-------|-----------|
| `Historia_Clinica` | Expediente médico del paciente |
| `Registro_Odontograma` | Trazabilidad por diente |
| `Nota_Clinica` | Notas clínicas estructuradas |
| `Control_Postoperatorio` | Seguimiento post-procedimiento |
| `Documento_Clinico` | Archivos adjuntos (radiografías, PDF) |
| `Alergia_Paciente` | Alergias estructuradas |

### 3.2 Módulo de Facturación
| Tabla | Propósito |
|-------|-----------|
| `Factura` | Facturas emitidas |
| `Detalle_Factura` | Líneas de factura |

### 3.3 Módulo de PQR/Soporte
| Tabla | Propósito |
|-------|-----------|
| `PQR` | Peticiones, quejas y reclamos |
| `Ticket_Soporte` | Tickets de soporte técnico |

### 3.4 Módulo de Inventario
| Tabla | Propósito |
|-------|-----------|
| `Inventario` | Control de insumos |
| `Equipo` | Equipos médicos |

### 3.5 Consultorios (Tablas Operativas)
| Tabla | Propósito |
|-------|-----------|
| `Consultorio_Historial` | Historial de cambios de estado |
| `Asistencia_Procedimiento` | Preparación pre/post procedimiento |
| `Consultorio_Estado_Operativo` | Checklist de estado operativo |

### 3.6 Seguridad y Permisos
| Tabla | Propósito |
|-------|-----------|
| `Menu` | Estructura de menús |
| `Rol_Menu_Permiso` | Permisos granulares por rol |
| `CodigoRecuperacion` | Códigos de recuperación de contraseña |
| `AuditoriaRecuperacion` | Auditoría de recuperación de contraseñas |
| `Notificacion_Leida` | Control de notificaciones leídas |

---

## 🚀 4. VISTAS SQL PROPUESTAS PARA OPTIMIZACIÓN

### 4.1 Vista: `vw_Citas_Dashboard`

**Propósito**: Optimizar consultas del dashboard de citas que actualmente hace múltiples JOINs.

```sql
CREATE OR ALTER VIEW vw_Citas_Dashboard
AS
SELECT 
    c.id_cita,
    c.fecha_hora,
    c.estado,
    c.duracion_minutos,
    c.notas,
    c.motivo_consulta,
    
    -- Paciente
    pac.id_paciente,
    pac.nombres + ' ' + pac.apellidos AS nombre_paciente,
    pac.documento,
    pac.telefono AS telefono_paciente,
    pac.correo AS correo_paciente,
    
    -- Profesional
    prof.id_profesional,
    prof.nombres + ' ' + prof.apellidos AS nombre_profesional,
    prof.registro_medico,
    prof.estado AS estado_profesional,
    
    -- Especialidad principal
    (SELECT TOP 1 e.nombre 
     FROM Profesional_Especialidad pe
     INNER JOIN Especialidad e ON pe.id_especialidad = e.id_especialidad
     WHERE pe.id_profesional = prof.id_profesional AND pe.principal = 1
    ) AS especialidad,
    
    -- Servicio
    s.id_servicio,
    s.nombre AS nombre_servicio,
    s.precio,
    
    -- Consultorio
    cons.id_consultorio,
    cons.nombre AS nombre_consultorio,
    cons.ubicacion,
    cons.estado AS estado_consultorio,
    
    -- Estado catálogo
    ec.id_estado AS id_estado_catalogo,
    ec.nombre_estado AS nombre_estado_catalogo,
    
    -- Usuario que creó
    u.id_usuario AS creado_por_id,
    u.nombre + ' ' + u.apellidos AS creado_por_nombre,
    c.fecha_creacion

FROM Cita c
INNER JOIN Paciente pac ON c.id_paciente = pac.id_paciente
LEFT JOIN Profesional prof ON c.id_profesional = prof.id_profesional
LEFT JOIN Servicio s ON c.id_servicio = s.id_servicio
LEFT JOIN Consultorio cons ON c.id_consultorio = cons.id_consultorio
LEFT JOIN Estado_Cita ec ON c.id_estado = ec.id_estado
LEFT JOIN Usuario u ON c.creado_por = u.id_usuario;
GO
```

**Índices Recomendados:**
```sql
-- Índice compuesto para filtros frecuentes
CREATE INDEX IX_Cita_FechaHora_Estado_Profesional 
ON Cita(fecha_hora, estado, id_profesional)
INCLUDE (id_paciente, id_servicio, id_consultorio);

-- Índice para búsqueda de profesionales con especialidades
CREATE INDEX IX_ProfesionalEspecialidad_Principal 
ON Profesional_Especialidad(id_profesional, principal)
INCLUDE (id_especialidad);
```

---

### 4.2 Vista: `vw_Profesionales_Completo`

**Propósito**: Optimizar listados de profesionales con todas sus relaciones.

```sql
CREATE OR ALTER VIEW vw_Profesionales_Completo
AS
SELECT 
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
    u.ultimo_login,
    
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
    
    -- Contadores
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
    
    (SELECT COUNT(*) 
     FROM Cita c 
     WHERE c.id_profesional = p.id_profesional 
       AND c.fecha_hora >= CAST(GETDATE() AS DATE)
       AND c.estado NOT IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado')
    ) AS total_citas_pendientes

FROM Profesional p
LEFT JOIN Usuario u ON p.id_usuario = u.id_usuario
LEFT JOIN Rol r ON u.id_rol = r.id_rol;
GO
```

**Índices Recomendados:**
```sql
-- Índice para búsqueda por estado y especialidad
CREATE INDEX IX_Profesional_Estado 
ON Profesional(estado)
INCLUDE (nombres, apellidos, registro_medico);

-- Índice para búsqueda de usuario vinculado
CREATE INDEX IX_Profesional_Usuario 
ON Profesional(id_usuario)
INCLUDE (nombres, apellidos, estado);
```

---

### 4.3 Vista: `vw_Disponibilidad_Profesional`

**Propósito**: Consultar rápidamente la disponibilidad completa de un profesional.

```sql
CREATE OR ALTER VIEW vw_Disponibilidad_Profesional
AS
SELECT 
    p.id_profesional,
    p.nombres + ' ' + p.apellidos AS nombre_profesional,
    p.estado AS estado_profesional,
    
    -- Horarios semanales
    hp.id_horario,
    hp.dia_semana,
    hp.hora_inicio,
    hp.hora_fin,
    hp.activo AS horario_activo,
    
    -- Ausencias actuales
    (SELECT COUNT(*) 
     FROM Ausencia_Profesional ap 
     WHERE ap.id_profesional = p.id_profesional 
       AND CAST(GETDATE() AS DATE) BETWEEN ap.fecha_inicio AND ap.fecha_fin
    ) AS tiene_ausencia_hoy,
    
    -- Bloqueos actuales
    (SELECT COUNT(*) 
     FROM Bloqueo_Profesional bp 
     WHERE bp.id_profesional = p.id_profesional 
       AND GETDATE() BETWEEN bp.fecha_inicio AND bp.fecha_fin
    ) AS tiene_bloqueo_ahora,
    
    -- Próxima cita
    (SELECT MIN(c.fecha_hora)
     FROM Cita c
     WHERE c.id_profesional = p.id_profesional
       AND c.fecha_hora > GETDATE()
       AND c.estado NOT IN ('Cancelada', 'cancelada')
    ) AS proxima_cita

FROM Profesional p
LEFT JOIN Horario_Profesional hp ON p.id_profesional = hp.id_profesional
WHERE p.estado IN ('activo', 'vacaciones');
GO
```

---

### 4.4 Vista: `vw_Citas_Conflictos`

**Propósito**: Detectar conflictos potenciales de agenda (solapamientos).

```sql
CREATE OR ALTER VIEW vw_Citas_Conflictos
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
)
SELECT 
    c1.id_cita AS cita1_id,
    c2.id_cita AS cita2_id,
    c1.id_profesional,
    p.nombres + ' ' + p.apellidos AS nombre_profesional,
    c1.inicio AS cita1_inicio,
    c1.fin AS cita1_fin,
    c2.inicio AS cita2_inicio,
    c2.fin AS cita2_fin,
    CASE 
        WHEN c1.id_profesional = c2.id_profesional THEN 'Profesional'
        WHEN c1.id_paciente = c2.id_paciente THEN 'Paciente'
        WHEN c1.id_consultorio = c2.id_consultorio THEN 'Consultorio'
    END AS tipo_conflicto
FROM CitasConFin c1
INNER JOIN CitasConFin c2 
    ON c1.id_cita < c2.id_cita
   AND (
       c1.id_profesional = c2.id_profesional
       OR c1.id_paciente = c2.id_paciente
       OR c1.id_consultorio = c2.id_consultorio
   )
   AND c1.inicio < c2.fin
   AND c2.inicio < c1.fin
LEFT JOIN Profesional p ON c1.id_profesional = p.id_profesional;
GO
```

---

### 4.5 Vista: `vw_KPI_Citas_Resumen`

**Propósito**: Pre-calcular KPIs mensuales para el dashboard.

```sql
CREATE OR ALTER VIEW vw_KPI_Citas_Resumen
AS
SELECT 
    YEAR(c.fecha_hora) AS anio,
    MONTH(c.fecha_hora) AS mes,
    
    -- Totales
    COUNT(*) AS total_citas,
    COUNT(DISTINCT c.id_paciente) AS total_pacientes_atendidos,
    COUNT(DISTINCT c.id_profesional) AS total_profesionales_activos,
    
    -- Por Estado
    SUM(CASE WHEN c.estado IN ('Agendada', 'Confirmada', 'Programada') THEN 1 ELSE 0 END) AS total_programadas,
    SUM(CASE WHEN c.estado IN ('Atendida', 'Completada') THEN 1 ELSE 0 END) AS total_atendidas,
    SUM(CASE WHEN c.estado IN ('Cancelada', 'cancelada', 'Cancelado') THEN 1 ELSE 0 END) AS total_canceladas,
    SUM(CASE WHEN c.estado = 'No Asistió' THEN 1 ELSE 0 END) AS total_inasistencias,
    
    -- Tasas
    CAST(
        SUM(CASE WHEN c.estado IN ('Cancelada', 'cancelada') THEN 1 ELSE 0 END) * 100.0 
        / NULLIF(COUNT(*), 0)
    AS DECIMAL(5,2)) AS tasa_cancelacion,
    
    CAST(
        SUM(CASE WHEN c.estado IN ('Atendida', 'Completada') THEN 1 ELSE 0 END) * 100.0 
        / NULLIF(COUNT(*), 0)
    AS DECIMAL(5,2)) AS tasa_asistencia,
    
    -- Duración promedio
    AVG(ISNULL(c.duracion_minutos, 60)) AS duracion_promedio_minutos

FROM Cita c
GROUP BY YEAR(c.fecha_hora), MONTH(c.fecha_hora);
GO
```

---

## 📌 5. ÍNDICES ADICIONALES RECOMENDADOS

### 5.1 Para Citas

```sql
-- Búsqueda por rango de fechas y estado
CREATE INDEX IX_Cita_FechaHora_Estado 
ON Cita(fecha_hora, estado)
INCLUDE (id_paciente, id_profesional, id_servicio);

-- Búsqueda de citas activas por profesional
CREATE INDEX IX_Cita_Profesional_FechaHora_Estado 
ON Cita(id_profesional, fecha_hora, estado)
WHERE estado NOT IN ('Cancelada', 'cancelada');

-- Búsqueda de citas del día
CREATE NONCLUSTERED INDEX IX_Cita_FechaSola 
ON Cita(CAST(fecha_hora AS DATE))
INCLUDE (id_profesional, id_paciente, estado);
```

### 5.2 Para Disponibilidad de Profesionales

```sql
-- Horarios activos por profesional
CREATE INDEX IX_HorarioProfesional_IdProf_Activo 
ON Horario_Profesional(id_profesional, activo)
INCLUDE (dia_semana, hora_inicio, hora_fin);

-- Ausencias vigentes
CREATE INDEX IX_AusenciaProfesional_IdProf_Fechas 
ON Ausencia_Profesional(id_profesional, fecha_inicio, fecha_fin);

-- Bloqueos activos
CREATE INDEX IX_BloqueoProfesional_IdProf_Fechas 
ON Bloqueo_Profesional(id_profesional, fecha_inicio, fecha_fin)
WHERE fecha_fin > GETDATE();
```

### 5.3 Para Profesionales

```sql
-- Búsqueda por estado
CREATE INDEX IX_Profesional_Estado 
ON Profesional(estado)
INCLUDE (nombres, apellidos, registro_medico);

-- Búsqueda por registro médico
-- Ya existe: UQ_Profesional_RegistroMedico (UNIQUE)

-- Relación con Usuario
CREATE INDEX IX_Profesional_Usuario 
ON Profesional(id_usuario);
```

---

## 🎯 6. CONCLUSIONES Y RECOMENDACIONES

### 6.1 Estado Actual
✅ **Ambos módulos están correctamente conectados a la base de datos**  
✅ **Todas las tablas necesarias existen y están en uso**  
✅ **Las relaciones FK están correctamente definidas**

### 6.2 Oportunidades de Mejora

1. **Implementar las Vistas SQL propuestas** para:
   - Reducir la complejidad de las consultas en el código C#
   - Mejorar rendimiento en listados paginados
   - Centralizar lógica de negocio compleja en la BD

2. **Agregar los índices recomendados** para:
   - Optimizar búsquedas por rango de fechas
   - Acelerar validaciones de disponibilidad
   - Mejorar rendimiento de KPIs

3. **Considerar Materialized Views** (SQL Server 2016+) para:
   - `vw_KPI_Citas_Resumen` → Refresh diario a medianoche
   - `vw_Profesionales_Completo` → Refresh al cambiar profesionales

4. **Auditoría Completa**:
   - Todas las operaciones críticas ya registran en `Auditoria`
   - Considerar agregar auditoría de consultas en vistas sensibles

### 6.3 Tablas No Utilizadas en Estos Módulos

Las siguientes tablas están **correctamente separadas** y pertenecen a otros módulos:
- Historia Clínica (7 tablas)
- Facturación (2 tablas)
- PQR/Soporte (2 tablas)
- Inventario (2 tablas)
- Seguridad/Menús (6 tablas)

**No es necesario modificarlas** para los módulos de Citas y Profesionales.

---

## 📅 Próximos Pasos

1. ✅ **Revisar y aprobar** las vistas SQL propuestas
2. ⏳ **Ejecutar script** de creación de vistas en base de datos de desarrollo
3. ⏳ **Probar rendimiento** comparando consultas actuales vs vistas
4. ⏳ **Implementar índices** recomendados gradualmente
5. ⏳ **Actualizar servicios** para usar las vistas donde aplique
6. ⏳ **Documentar** cambios en manual técnico

---

**Generado por**: Kiro AI  
**Última actualización**: 2025-01-XX
