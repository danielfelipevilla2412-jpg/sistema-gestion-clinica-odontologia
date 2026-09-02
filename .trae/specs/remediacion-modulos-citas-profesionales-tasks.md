# Tasks — Remediación Integral Módulos Citas / Profesionales

Derivado de `remediacion-modulos-citas-profesionales-spec.md`.

---

## Task 1: C-03 Declarar NONCLUSTERED HasIndex en Cita (Fluent API)

**Prioridad**: high  
**Dependencias**: (ninguna)  
**Status**: pending

### Descripción
En `AppDbContext.OnModelCreating` bloque `modelBuilder.Entity<Cita>`, agregar 4 `HasIndex` NONCLUSTERED con nombres `IX_Cita_Profesional_Fecha`, `IX_Cita_Paciente_Fecha`, `IX_Cita_Consultorio_Fecha`, `IX_Cita_Estado_Fecha` tal como están en el script SQL SCRIPT_SQL_UNICO_SMILETRACK.sql L556-586.

Archivo objetivo: [AppDbContext.cs](file:///c:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Data/AppDbContext.cs#L205-L244)

### Test Requirements (TR)
- **TR-1.1 (rule)**: `grep -c "IX_Cita_Profesional_Fecha\|IX_Cita_Paciente_Fecha\|IX_Cita_Consultorio_Fecha\|IX_Cita_Estado_Fecha" AppDbContext.cs` == 4.
- **TR-1.2 (rule)**: Los HasIndex NO duplican el PK ni crean nombres distintos a los del script (evita doble creación de índice en migraciones).
- **TR-1.3 (rubric, scale 0-2, aprobado ≥1)**: Consistencia Fluent API ↔ script SQL.
  - 2: 4/4 índices, misma firma (columnas) y mismo HasDatabaseName.
  - 1: 3/4 índices correctos.
  - 0: <3 o nombres no alineados.

---

## Task 2: C-04 Eliminar DuracionCitaMinutos hardcodeada del Controlador Citas

**Prioridad**: high  
**Dependencias**: Task 1 (indiferente)  
**Status**: pending

### Descripción
En [GestionCitasController.cs](file:///c:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Controllers/GestionCitasController.cs):
1. Eliminar línea `private const int DuracionCitaMinutos = 60;` (actual L68).
2. Reemplazar las 2 ocurrencias `AddMinutes(DuracionCitaMinutos)` en GuardarCita (L537-539 y L632-633) por una sola lectura de `await _citaService.ObtenerDuracionCitaMinutosAsync(ct)` al inicio del método y almacenar en variable local.
3. Comentar en el código XML que la duración ya NO es fuente de verdad del controller.

### TR
- **TR-2.1 (rule)**: `grep "DuracionCitaMinutos" GestionCitasController.cs` retorna 0 ocurrencias de la constante `const int`. Solo puede quedar en comentarios/documentación.
- **TR-2.2 (rule)**: `GuardarCita` llama a `_citaService.ObtenerDuracionCitaMinutosAsync(ct)` exactamente UNA vez y reutiliza la variable local en ambos bloques (fin/inicio rango). No se hacen múltiples lecturas de ConfigGeneral.
- **TR-2.3 (rule)**: `dotnet build` sin errores.

---

## Task 3: C-01 / C-02 Refactor GuardarCita MVC → delegar en CitaService

**Prioridad**: high  
**Dependencias**: Task 2 (ya se eliminó constante)  
**Status**: pending

### Descripción
Reescribir el cuerpo de GuardarCita en [GestionCitasController.cs](file:///c:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Controllers/GestionCitasController.cs#L362-L850) para:
1. Mantener: validación de ModelState, cálculo `model.FechaHora = model.Fecha.Date.Add(model.HoraInicio.Value)`, chequeo horario pasado `< DateTime.Now.AddMinutes(-5)`.
2. ELIMINAR: 5 bloques AnyAsync existencia Paciente/Profesional/Servicio/Consultorio/Estado (L442-529).
3. ELIMINAR: 2 bloques `_context.Citas.AnyAsync` conflicto (L566-584 y L623-642).
4. NUEVO: Construir `CitaApiRequest` (si IdCita == 0) o `CitaApiUpdateDto` (si IdCita > 0) desde el CitaViewModel.
5. NUEVO: Invocar `_citaService.CrearAsync(request, ct)` ó `_citaService.ActualizarAsync(model.IdCita!.Value, updateDto, ct)`, envolver en try/catch que capture `InvalidOperationException` y mapee su Message a `TempData["ErrorValidacion"]`.
6. Mantener intactos: bloques RegistrarAuditoriaAsync, EnviarNotificacionCitaAsync, logging, return Redirect(returnUrlSafe), TempData["MensajeExito"].
7. Si `ActualizarAsync` devuelve null → TempData error "cita no existe".

### TR
- **TR-3.1 (rule)**: Grep por `_context.Pacientes.AnyAsync\|_context.Profesionales.AnyAsync\|_context.Servicios.AnyAsync\|_context.Consultorios.AnyAsync\|_context.EstadosCita.AnyAsync` en el método GuardarCita → 0 matches.
- **TR-3.2 (rule)**: Grep por `_context.Citas.AnyAsync` en GuardarCita → 0 matches.
- **TR-3.3 (rule)**: GuardarCita llama a `CrearAsync` en path creación y `ActualizarAsync` en path actualización. Ambas rutas se alcanzan según model.IdCita.
- **TR-3.4 (rule)**: Los mensajes del servicio (conflicto Profesional / Paciente / Consultorio / Horario clínica / existencia entidades) fluyen intactos a TempData["ErrorValidacion"] cuando InvalidOperationException se lanza.
- **TR-3.5 (rubric, escala 0-2, aprobado ≥1)**: Fidelidad thin controller.
  - 2: todo controller = armar DTO + llamar service + mapear tempdata + redirect. 0 lógica de negocio inline.
  - 1: mínimo inline (horario pasado por UX) + todo el resto en service.
  - 0: quedan bloques AnyAsync propios.

---

## Task 4: C-06 Refactor EliminarCita MVC → CancelarAsync

**Prioridad**: medium  
**Dependencias**: Task 3 (ya existe integración service pattern en mismo controller)  
**Status**: pending

### Descripción
Refactor [EliminarCita](file:///c:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Controllers/GestionCitasController.cs#L856-L998):
1. Mantener validación IdCita > 0 y returnUrlSafe.
2. ELIMINAR: query inline `FirstOrDefaultAsync`, soft delete manual a EstadosCita, `SaveChangesAsync` inline.
3. NUEVO: `bool ok = await _citaService.CancelarAsync(IdCita, ct)`.
4. Si ok: ejecutar RegistrarAuditoriaAsync (igual que hoy, L925-935), EnviarNotificacionCitaAsync (L937-940), TempData["MensajeExito"] = hoy.
5. Si `!ok`: TempData error "cita no existe".
6. Si el servicio lanza InvalidOperationException "La cita ya está cancelada." → TempData con ese mensaje.

### TR
- **TR-4.1 (rule)**: EliminarCita invoca `_citaService.CancelarAsync` y NO llama `_context.SaveChangesAsync` propia.
- **TR-4.2 (rule)**: `_context.EstadosCita.FirstOrDefaultAsync` → 0 ocurrencias en EliminarCita.
- **TR-4.3 (rule)**: Auditoría y envío de email siguen ejecutándose en path de éxito. Se preservan las rutas.
- **TR-4.4 (rule)**: Ya cancelada → TempData["ErrorValidacion"] contiene el mensaje de la excepción InvalidOperationException del servicio.

---

## Task 5: P-04/P-05 Reemplazar regex y NormalizarEstado en GestionProfesionalesController por Helpers

**Prioridad**: medium  
**Dependencias**: (independiente)  
**Status**: pending

### Descripción
En [GestionProfesionalesController.cs](file:///c:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Controllers/ProfesionalesController.cs):
1. Eliminar `PasswordAccesoRegex` L24-27. Sustituir uso (si hubiera) por `ProfesionalEstadoHelper.EsPasswordValida()`.
2. Eliminar `RegistroMedicoRegex` L45-46 y método `EsRegistroMedicoValido` si tiene sentido; dejar una sola versión en el controller que NO use regex propio duplicado (puede usar directamente el del service o el helper; en esta tarea al menos SE ELIMINA la duplicidad del controller).
3. Eliminar `NormalizarEstado` L545-554 propio. Sustituir cada invocación local por `ProfesionalEstadoHelper.NormalizarEstado(estado)`.
4. Actualizar `using` para importar `SmileTrack_MVC.Helpers`.

### TR
- **TR-5.1 (rule)**: Grep `PasswordAccesoRegex\|private static partial Regex RegistroMedicoRegex\|private static string NormalizarEstado` en GestionProfesionalesController → 0.
- **TR-5.2 (rule)**: Todas las referencias al regex de password en el controller pasan a usar `ProfesionalEstadoHelper.EsPasswordValida()`.
- **TR-5.3 (rule)**: Todas las referencias a NormalizarEstado (dashboard L178-181 y similares) usan `ProfesionalEstadoHelper.NormalizarEstado`.

---

## Task 6: P-04/P-05 Centralizar PasswordRegex en ProfesionalService

**Prioridad**: medium  
**Dependencias**: Task 5 (misma familia de helpers)  
**Status**: pending

### Descripción
En [ProfesionalService.cs](file:///c:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Services/ProfesionalService.cs):
1. Buscar el uso de `PasswordRegex` en L381, L720, L726. Reemplazar `PasswordRegex.IsMatch(request.ContrasenaAcceso)` por `ProfesionalEstadoHelper.EsPasswordValida(request.ContrasenaAcceso)`.
2. Si hay definición duplicada (Regex propio) en el partial, eliminarla.
3. Actualizar `using SmileTrack_MVC.Helpers;`.

### TR
- **TR-6.1 (rule)**: Grep por `PasswordRegex` en ProfesionalService.cs → 0 ocurrencias fuera de comentarios.
- **TR-6.2 (rule)**: Se usa `ProfesionalEstadoHelper.EsPasswordValida` en los 3 sitios donde validaba contraseña (Actualizar cambio password + ValidarRequest crear + ValidarRequest edición).
- **TR-6.3 (rule)**: Build sin errores.

---

## Task 7: P-03 Agregar ObtenerVistaMVCAsync a IProfesionalService + Implementación

**Prioridad**: medium  
**Dependencias**: (ninguna, previa a Task 8)  
**Status**: pending

### Descripción
1. Agregar en [IProfesionalService.cs](file:///c:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Services/IProfesionalService.cs) el signature:
```csharp
Task<(List<Profesional> Items, PagedResult Paginacion, ProfesionalesStats Stats)>
    ObtenerVistaMVCAsync(PaginationQuery q, CancellationToken ct = default);
```
2. Definir DTO simple `ProfesionalesStats` en `Models/Shared/ProfesionalesStats.cs` con propiedades `int StatTotal`, `int StatActivos`, `int StatVacaciones`, `int StatInactivos`.
3. Implementar en ProfesionalService.cs el método, reutilizando la misma lógica de filtros que `ObtenerAsync` (search / especialidad / estado) y devolviendo los 4 contadores de stats en la misma ejecución (leer 4 CountAsync o usar un query agrupado).
4. Mantener el mismo mapeo de `Include(p => p.Usuario)`, `Include(p => p.Especialidades).ThenInclude(pe => pe.Especialidad)` que en CargarDatosProfesionales L440-444.

### TR
- **TR-7.1 (rule)**: IProfesionalService declara ObtenerVistaMVCAsync con tupla Items/Paginacion/Stats.
- **TR-7.2 (rule)**: ProfesionalService implementa el método y cumple filtros search/especialidad/estado.
- **TR-7.3 (rule)**: El método NO usa `_context` directo desde controller (la implementación encapsula todo en Service).
- **TR-7.4 (rule)**: Build sin errores.

---

## Task 8: P-03 CargarDatosProfesionales usa IProfesionalService (thin controller)

**Prioridad**: medium  
**Dependencias**: Task 7  
**Status**: pending

### Descripción
Refactor [CargarDatosProfesionales](file:///c:/Users/APRENDIZ/sistema-gestion-clinica-odontologia/Controllers/ProfesionalesController.cs#L427-L501) en GestionProfesionalesController:
1. Inyectar `IProfesionalService` por constructor (actual controller solo tiene AppDbContext + ILogger). Modificar constructor.
2. Eliminar los 4 CountAsync inline (L435-438).
3. Eliminar el query `profesionalesQuery` + filtros + `ToPagedResultAsync` inline (L440-472).
4. Llamar a `_profesionalService.ObtenerVistaMVCAsync(query, ct)`.
5. Asignar ViewData["Profesionales"], ViewData["ProfesionalesPage"], ViewBag.StatTotal / StatActivos / StatVacaciones / StatInactivos desde la tupla retornada.
6. Mantener intactos: ViewData["Especialidades"], ViewData["ReturnUrl"], ViewData["EditingProfesional"], InicializarViewDataProfesionalesVacio fallback en catch.

### TR
- **TR-8.1 (rule)**: `_context.Profesionales.CountAsync` → 0 ocurrencias en CargarDatosProfesionales.
- **TR-8.2 (rule)**: `profesionalesQuery = _context.Profesionales.Include(...)` desaparece; el controller NO construye query IQueryable propio.
- **TR-8.3 (rule)**: Constructor de GestionProfesionalesController incluye `IProfesionalService` y lo guarda en campo readonly `_profesionalService`.
- **TR-8.4 (rubric escala 0-2, aprobado ≥1)**: Calidad thin controller.
  - 2: 100% de los _context.* en CargarDatosProfesionales desaparecen salvo los que llenan ViewData["Especialidades"] (se puede migrar a `_profesionalService.ObtenerEspecialidadesAsync` ya existente).
  - 1: Solo queda ViewData["Especialidades"] con query inline; el resto todo service.
  - 0: Siguen quedando filtros propios inline.

---

## Task 9: Build + Tests + Validación regresión

**Prioridad**: high  
**Dependencias**: Tasks 1–8 completadas  
**Status**: pending

### Descripción
1. `dotnet clean` + `dotnet build SmileTrack_MVC.slnx --no-incremental -c Debug` y verificar exit 0.
2. `dotnet test SmileTrack_MVC.Tests/SmileTrack_MVC.Tests.csproj -c Debug --no-build` si existe el proyecto test. Exit 0.
3. Revisar manualmente: abrir archivos modificados y confirmar 0 warnings relacionados con los cambios.

### TR
- **TR-9.1 (rule)**: Build exit code 0; 0 errores; los warnings nuevos son ≤ que los de baseline.
- **TR-9.2 (rule)**: Tests pasan con exit code 0 (si el proyecto de test existe y puede correrse en este environment).
- **TR-9.3 (rubric escala 0-2, aprobado ≥1)**: Regresión.
  - 2: Todo AC rule pasa y build/tests clean.
  - 1: Build OK, algunas tests fallan por razones ajenas a estos módulos y se documentan.
  - 0: Build NO pasa.
