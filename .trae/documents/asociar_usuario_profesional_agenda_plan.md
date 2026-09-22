# Asociación Usuario-Profesional y Visualización Completa en Módulo de Agenda — Plan de Implementación

## 1. Análisis de Viabilidad y Conclusiones de la Investigación

### 1.1 Estado Actual (Hallazgos con Evidencia)

**✅ Estructura de datos y relación YA EXISTEN (Infraestructura preexistente):**

| Componente | Ubicación | Estado |
|---|---|---|
| Columna `Profesional.id_usuario INT NULL` | [SCRIPT_SQL_UNICO_SMILETRACK.sql](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Database/SCRIPT_SQL_UNICO_SMILETRACK.sql#L237-L249) L237-L249 | ✅ Existe en `CREATE TABLE`, FK `FK_Profesional_Usuario` ON DELETE SET NULL |
| Bloque ALTER TABLE idempotente para `id_usuario` | Mismo archivo, después de L275 | ❌ **FALTA**: No existe `IF COL_LENGTH(... 'id_usuario')` para parches en BD ya creadas |
| Entidad C# `Profesional.IdUsuario` + `Usuario` nav | [Profesional.cs](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Models/Entities/GestionDeProfesionales/Profesional.cs#L13-L52) L13-L52 | ✅ Definida con `[ForeignKey]` |
| Configuración Fluent API relación | [AppDbContext.cs](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Data/AppDbContext.cs#L158-L182) L158-L182 | ✅ `DeleteBehavior.Restrict` |
| Creación automática Usuario + vinculación | [ProfesionalService.CrearAsync](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Services/GestionDeProfesionales/ProfesionalService.cs#L226-L357) L226-L357 | ✅ Transaccional: crea Usuario rol "Profesional", luego Profesional con `IdUsuario` |
| Sincronización actualización Usuario↔Profesional | [ProfesionalService.ActualizarAsync](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Services/GestionDeProfesionales/ProfesionalService.cs#L383-L575) L383-L575 | ✅ Sincroniza nombres, correo, password, estado |
| Sincronización estado (inactivación) | [ProfesionalService.CambiarEstadoAsync](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Services/GestionDeProfesionales/ProfesionalService.cs#L579-L701) L579-L701 | ✅ Inactivar profesional → inactiva la cuenta Usuario |

**⚠️ Módulo de Agenda: Include parcial y visualización incompleta:**

| Componente | Ubicación | Problema |
|---|---|---|
| Include `.ThenInclude(p => p.Usuario)` | [AgendaService.cs](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Services/GestionDeCitas/AgendaService.cs#L38-L46) L38-L46 | ✅ Ya existe para la consulta principal de citas |
| `ObtenerProfesionalesAsync()` (filtros dropdown) | [AgendaService.cs](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Services/GestionDeCitas/AgendaService.cs#L127-L132) L127-L132 | ❌ **No incluye Usuario**: nombres vienen SOLO de `Profesional.Nombres/Apellidos`, no de `Usuario` |
| `AgendaCitaViewModel` | [ReportesYAgendaViewModels.cs](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Models/ViewModels/ReportesYAgendaViewModels.cs#L29-L47) L29-L47 | ❌ Solo tiene `NombreProfesional`; sin `CorreoProfesional`, `TelefonoProfesional`, `RegistroMedico`, `EstadoUsuario` |
| `AgendaService.MapearCita()` | [AgendaService.cs](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Services/GestionDeCitas/AgendaService.cs#L91-L118) L91-L118 | ❌ **Prioridad invertida**: usa `Profesional.Nombres` primero y `Usuario` como fallback. Debería priorizar `Usuario` (fuente canónica de acceso) |
| Modal detalle cita (JS) | [agendageneral.js](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/wwwroot/js/Gestion_De_Citas/st-adm-08-agenda/agendageneral.js#L898-L946) L898-L946 | ❌ Solo muestra `Profesional: {nombre}`, sin correo, teléfono, registro médico |
| Atributos data-* en tarjetas de cita | [index.cshtml](file:///C:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml#L127-L144) L127-L144 | ❌ Solo `data-professional-name`, sin correo/teléfono |

### 1.2 Viabilidad Técnica: **ALTA**

**Justificación:**
- La relación FK `Profesional → Usuario` ya existe en BD y EF Core; no requiere cambio de esquema estructural, solo un parche idempotente de seguridad.
- `ProfesionalService` ya gestiona transaccionalmente la creación/actualización sincronizada.
- Los cambios son **aditivos** (nuevos campos en ViewModel, nuevos data-*, renderizado en modal): no rompen APIs existentes.
- No hay impacto en `Cita`, `Paciente`, `Factura` ni tablas transaccionales.

### 1.3 Riesgo de Datos Existentes: **BAJO (con mitigación)**

Riesgo único: Profesionales históricos creados antes de `ProfesionalService` pueden tener `IdUsuario = NULL`. La implementación debe usar `??` y `?.` en todos los puntos para no romper la agenda.

---

## 2. Integridad Referencial y Validaciones Requeridas

### 2.1 Restricciones a Nivel BD (ya existentes + parche)

| Restricción | Tipo | Tabla | Observación |
|---|---|---|---|
| `FK_Profesional_Usuario` | FOREIGN KEY | `Profesional.id_usuario → Usuario.id_usuario` | ✅ Existe en CREATE; **falta ALTER TABLE idempotente** |
| `ON DELETE SET NULL` | Comportamiento FK | Eliminar Usuario → `Profesional.id_usuario = NULL` | ✅ Definido en SQL; ⚠️ difiere de EF Core `Restrict`. **Armonizar**: cambiar SQL a `ON DELETE NO ACTION` para coincidir con EF |
| `UNIQUE (correo)` | CONSTRAINT | `Usuario.correo` | ✅ Evita correos duplicados |
| `UQ_Profesional_RegistroMedico` | INDEX UNIQUE | `Profesional.registro_medico` | ✅ Evita duplicados |
| `CK_Profesional_Estado` | CHECK | `estado IN ('activo','vacaciones','inactivo')` | ✅ Existe |
| `CK_Usuario_Estado` | CHECK | `estado IN ('activo','inactivo')` | ✅ Existe en CREATE |

### 2.2 Validaciones a Nivel Aplicación (ya existentes + nuevas)

| Regla | Ubicación Actual | Acción Requerida |
|---|---|---|
| Correo único global | `ProfesionalService.CrearAsync` L266-L269 | ✅ Ya validado; ninguna acción |
| Registro médico único | `ProfesionalService.CrearAsync` L246-L250 | ✅ Ya validado |
| Política de contraseña | `ProfesionalEstadoHelper.EsPasswordValida()` | ✅ Ya centralizada |
| Inactivación con citas pendientes → bloqueo | `CambiarEstadoAsync` L610-L623 | ✅ Ya validado |
| Rol "Profesional" obligatorio en Usuario asociado | `ActualizarAsync` L443-L445 | ✅ Ya validado |
| **Prioridad fuente canónica nombres**: `Usuario.Nombre` > `Profesional.Nombres` | Ninguna (hoy al revés) | 🔧 Implementar en `MapearCita()` y `Profesional.NombreProfesional` |
| **Profesional sin IdUsuario**: mostrar fallback elegante | Parcial en `MapearCita` L95-L96 | 🔧 Extender a todos los campos nuevos (correo = "Sin cuenta asociada") |

---

## 3. Archivos y Módulos Afectados

| Archivo | Cambio Esperado |
|---|---|
| `Database/SCRIPT_SQL_UNICO_SMILETRACK.sql` | Agregar bloque `IF COL_LENGTH(... 'id_usuario')` idempotente; armonizar ON DELETE |
| `Models/ViewModels/ReportesYAgendaViewModels.cs` | Agregar campos a `AgendaCitaViewModel`: `CorreoProfesional`, `TelefonoProfesional`, `RegistroMedicoProfesional`, `EstadoUsuarioProfesional` |
| `Models/ViewModels/GestionDeCitas/AgendaViewModel.cs` | (Opcional) Extender `SelectOptionViewModel` o crear `ProfesionalAgendaViewModel` con correo/teléfono para filtros |
| `Services/GestionDeCitas/AgendaService.cs` | (1) Invertir prioridad en `MapearCita`; (2) Cargar nuevos campos; (3) `ObtenerProfesionalesAsync` debe incluir `.Include(p => p.Usuario)` y priorizar nombres de Usuario |
| `Models/Entities/GestionDeProfesionales/Profesional.cs` | Alinear `NombreProfesional` para priorizar `Usuario` (actualmente ya lo hace, confirmar) |
| `Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml` | Agregar nuevos `data-*` attributes a las tarjetas de cita (correo, teléfono, registro médico) |
| `Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml` | Mismos cambios que agenda admin |
| `wwwroot/js/Gestion_De_Citas/st-adm-08-agenda/agendageneral.js` | Renderizar correo/teléfono/registro médico en modal de detalle; opcionalmente tooltip en tarjeta |
| `wwwroot/js/Gestion_De_Citas/st-odo-02-agenda/agenda.js` | Mismos cambios en agenda de profesional |
| `Controllers/GestionDeProfesionales/ProfesionalesController.cs` | (Sin cambio, pero revisar que los endpoints MVC usen `Include(p => p.Usuario)` en listados) |

---

## 4. Pasos Técnicos de Implementación (Orden de Dependencias)

### Paso 1 — Parche idempotente en Script SQL (Alta Prioridad)
**Objetivo**: Garantizar que `id_usuario` y su FK existan en BD creadas antes del `CREATE TABLE` original.
1. Después del último `IF COL_LENGTH` de `Profesional` (actualmente L275: `fecha_ingreso`), insertar:
   ```sql
   IF COL_LENGTH(N'dbo.Profesional', N'id_usuario') IS NULL
   BEGIN
       ALTER TABLE Profesional ADD id_usuario INT NULL;
       
       IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Profesional_Usuario')
           ALTER TABLE Profesional ADD CONSTRAINT FK_Profesional_Usuario
               FOREIGN KEY (id_usuario) REFERENCES Usuario(id_usuario) ON DELETE NO ACTION;
   END
   GO
   ```
2. Si el constraint existente usa `ON DELETE SET NULL`, armonizar a `NO ACTION` para coincidir con `DeleteBehavior.Restrict` de EF Core (evitar desincronización silenciosa).

### Paso 2 — Extender `AgendaCitaViewModel`
**Objetivo**: Transportar datos completos del usuario profesional a la vista.
```csharp
public class AgendaCitaViewModel
{
    // ... campos existentes ...
    public string CorreoProfesional { get; set; } = string.Empty;
    public string TelefonoProfesional { get; set; } = string.Empty;
    public string RegistroMedicoProfesional { get; set; } = string.Empty;
    public string EstadoUsuarioProfesional { get; set; } = string.Empty;
}
```

### Paso 3 — Corregir prioridad y poblar campos nuevos en `AgendaService`
**Objetivo**: Usuario = fuente canónica; Profesional = fallback (hoy es al revés).
1. `MapearCita()`: reordenar la prioridad:
   ```csharp
   // PRIMERO Usuario (fuente canónica de acceso)
   var nombresProf = cita.Profesional?.Usuario?.Nombre?.Trim();
   var apellidosProf = cita.Profesional?.Usuario?.Apellidos?.Trim();
   if (string.IsNullOrWhiteSpace(nombresProf)) nombresProf = cita.Profesional?.Nombres?.Trim();
   if (string.IsNullOrWhiteSpace(apellidosProf)) apellidosProf = cita.Profesional?.Apellidos?.Trim();
   var nombreProfesional = $"{nombresProf} {apellidosProf}".Trim();
   
   // Nuevos campos:
   CorreoProfesional = cita.Profesional?.Usuario?.Correo ?? "Sin cuenta asociada",
   TelefonoProfesional = cita.Profesional?.Usuario?.Telefono  // ⚠️ Usuario NO tiene Telefono → usar Profesional.Telefono
                      ?? cita.Profesional?.Telefono ?? "Sin dato",
   RegistroMedicoProfesional = cita.Profesional?.RegistroMedico ?? string.Empty,
   EstadoUsuarioProfesional = cita.Profesional?.Usuario?.Estado ?? cita.Profesional?.Estado ?? "activo",
   ```
2. `ObtenerProfesionalesAsync()`: agregar `.Include(p => p.Usuario)` y usar la misma lógica de prioridad en el `Text` del SelectOption.

### Paso 4 — Alinear `Profesional.NombreProfesional`
**Objetivo**: Coherencia global. El cálculo actual ya prioriza Usuario, confirmar y comentar.

### Paso 5 — Extender atributos data-* en Vista Razor (Agenda Admin)
**Archivo**: `Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml`
- En el `<div class="appointment">` (L131-L143), agregar:
  ```
  data-professional-email="@Html.Encode(cita.CorreoProfesional)"
  data-professional-phone="@Html.Encode(cita.TelefonoProfesional)"
  data-professional-registry="@Html.Encode(cita.RegistroMedicoProfesional)"
  data-professional-user-status="@Html.Encode(cita.EstadoUsuarioProfesional)"
  ```

### Paso 6 — Renderizar datos nuevos en Modal JS
**Archivo**: `wwwroot/js/Gestion_De_Citas/st-adm-08-agenda/agendageneral.js`
- En `initAppointmentDetailModal`, agregar filas al grid de detalle después de "Profesional":
  ```js
  ['Correo profesional', selectedAppointment.professionalEmail],
  ['Teléfono profesional', selectedAppointment.professionalPhone],
  ['Registro médico', selectedAppointment.professionalRegistry],
  ['Estado cuenta', selectedAppointment.professionalUserStatus],
  ```
- Extraer nuevos atributos `data-professional-*` al seleccionar la cita.
- (Opcional) Mostrar tooltip `title` en la tarjeta con correo/teléfono.

### Paso 7 — Aplicar mismos cambios a Agenda del Profesional (st-odo-02)
- `Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml`: data-* attributes
- `wwwroot/js/Gestion_De_Citas/st-odo-02-agenda/agenda.js`: modal detalle

### Paso 8 — Regresión: Agenda Apoyo y Tabla Integral de Citas (verificación)
- Revisar si `st-aux-02-agenda-apoyo` y `st-adm-09-citas` consumen los mismos ViewModels; si sí, heredarán los campos nuevos automáticamente.

---

## 5. Validación y Pruebas

### 5.1 Pruebas Unitarias (dotnet test)
| Caso | Método a Probar | Resultado Esperado |
|---|---|---|
| `MapearCita` con Usuario completo | AgendaService.MapearCita | Nombre = Usuario.Nombre + Apellidos; Correo = Usuario.Correo |
| `MapearCita` sin Usuario (IdUsuario = NULL) | AgendaService.MapearCita | Nombre = Profesional.Nombres + Apellidos; Correo = "Sin cuenta asociada" |
| `MapearCita` con Usuario pero nombres vacíos | AgendaService.MapearCita | Fallback a Profesional.Nombres |
| `ObtenerProfesionalesAsync` incluye Usuario | AgendaService | Text del select usa prioridad Usuario > Profesional |
| Crear profesional → IdUsuario poblado | ProfesionalService.CrearAsync | `IdUsuario > 0` y `Usuario.Correo = request.CorreoAcceso` |
| Actualizar profesional → sincroniza Usuario | ProfesionalService.ActualizarAsync | `Usuario.Nombre` y `Usuario.Correo` reflejan los nuevos valores |
| Inactivar profesional → inactiva Usuario | ProfesionalService.CambiarEstadoAsync | `Usuario.Estado = "inactivo"` |

### 5.2 Pruebas de Integración (Navegador Manual / Playbook)
1. **Paso 1**: Ir a `st-adm-07-gestion-profesionales`, crear profesional nuevo con correo único.
2. **Paso 2**: Ir a `st-adm-08-agenda`, agendar cita con el profesional nuevo.
3. **Paso 3**: Hacer clic en la cita → abrir modal. Verificar:
   - Nombre del profesional coincide con el del alta
   - Se muestra correo, teléfono (de Profesional.Telefono), registro médico
   - Estado cuenta = "activo"
4. **Paso 4**: Repetir con un profesional histórico (`IdUsuario = NULL`).
   - Correo debe mostrar "Sin cuenta asociada" o vacío
   - Nombre debe venir de `Profesional.Nombres`
5. **Paso 5**: Inactivar profesional desde gestión → abrir agenda y filtrar.
   - El profesional NO debe aparecer en dropdown de filtros (`ObtenerProfesionalesAsync` filtra `estado = activo`)
6. **Paso 6**: Filtro por profesional → la selección muestra nombre completo (Usuario > Profesional).

### 5.3 Pruebas de Rendimiento
- Medir tiempo de carga de `AgendaService.ObtenerAgendaAsync` con 500 citas en semana.
- Confirmar que el `.ThenInclude(p => p.Usuario)` existente no agrega más de un JOIN adicional (ya estaba presente).
- Verificar en SQL Profiler / EF Core logging: solo una consulta con JOIN a Usuario, no N+1.

### 5.4 Pruebas de Consistencia de Datos
- Query de auditoría post-implementación:
  ```sql
  SELECT p.id_profesional, p.nombres, p.apellidos, p.id_usuario,
         u.nombre, u.apellidos, u.correo
  FROM Profesional p LEFT JOIN Usuario u ON p.id_usuario = u.id_usuario
  WHERE p.estado = 'activo'
    AND (p.id_usuario IS NULL OR u.estado <> p.estado);
  ```
  Esperado: **0 filas** (todos los profesionales activos tienen Usuario con estado sincronizado; o bien los huérfanos son aceptables y documentados).

---

## 6. Riesgos y Manejo

| Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|
| `Profesional.id_usuario` NULL en registros antiguos → `NullReferenceException` | Media | Alto | Usar `?.` y `??` en **todos** los accesos a `Profesional.Usuario`; strings fallback explícitos |
| Desincronización nombres: `Profesional.Nombres ≠ Usuario.Nombre` | Alta | Medio | Establecer como regla: `Usuario` es fuente canónica; `Profesional.Nombres` se actualiza desde el Usuario en `ActualizarAsync` (ya lo hace). Agregar job/migración de una sola vez para sincronizar históricos |
| FK ON DELETE desalineado (SQL SET NULL vs EF Restrict) → comportamiento diferente | Baja | Medio | Aplicar `ALTER` en script idempotente para cambiar a `NO ACTION` y alinear con EF |
| Dropdown filtros no muestra nombre correcto | Media | Bajo | Corregir `ObtenerProfesionalesAsync` para Include + prioridad Usuario |
| Campos nuevos no se muestran en st-odo-02 | Media | Bajo | Incluir en el alcance la agenda del profesional (Paso 7) |
| Impacto rendimiento por JOIN adicional | Baja | Bajo | El JOIN ya existe; solo se consumen más columnas SELECT. No hay N+1 |

---

## 7. Criterios de Aceptación

1. [ ] `dotnet build` sin errores ni warnings de nulabilidad.
2. [ ] `dotnet test` pasa todos los tests existentes + nuevos casos unitarios para `MapearCita`.
3. [ ] En agenda admin (st-adm-08): clic en cita → modal muestra **Nombre, Correo, Teléfono, Registro Médico, Estado de Cuenta** del profesional.
4. [ ] Dropdown "Profesional" en filtros muestra el nombre con prioridad Usuario > Profesional.
5. [ ] Profesional sin `IdUsuario` se muestra correctamente con fallback "Sin cuenta asociada".
6. [ ] Script SQL idempotente: ejecutar `SCRIPT_SQL_UNICO_SMILETRACK.sql` dos veces seguidas → sin errores.
7. [ ] `git diff` solo afecta los 9-10 archivos listados en §3; no hay cambios en esquema de tablas transaccionales (Cita, Paciente, Factura).
8. [ ] No hay regresión: crear/cancelar/editar cita funciona sin cambios de comportamiento.
