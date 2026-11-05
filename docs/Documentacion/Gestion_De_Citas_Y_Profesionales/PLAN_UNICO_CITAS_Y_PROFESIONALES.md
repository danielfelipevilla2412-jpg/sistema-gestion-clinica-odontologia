# Master Plan Unificado - Gestión de Citas y Gestión de Profesionales

**Proyecto:** SmileTrack (ASP.NET Core MVC 9.0 + SQL Server LocalDB)  
**Fecha de consolidación:** 21 de Septiembre de 2026  
**Propósito:** Consolidar en un solo documento la auditoría de la documentación existente, el estado de lo que ya fue implementado y el plan de trabajo pendiente con sus respectivos prompts de ejecución.

---

## 📑 1. Matriz de Análisis de la Documentación Existente

| Documento | Enfoque Principal | Estado de los Contenidos | Acciones Realizadas |
| :--- | :--- | :--- | :--- |
| `FLUJOS_USUARIO_CITAS_Y_PROFESIONALES.md` | Mapeo de 19 vistas, roles, reglas de negocio y ciclo de vida de citas. | **Vigente como referencia de arquitectura y negocio.** | Mantenido como especificación funcional de los 4 roles (Paciente, Odontólogo, Recepcionista, Admin). |
| `AUDITORIA_ESTANDARIZACION_MODULOS.md` | Auditoría de componentes UI (stat-cards, fallbacks API, campos del modelo). | **Parcialmente superado.** Tareas 4, 6 y 7 completadas. | Se extraen las reglas técnicas y se eliminan del backlog de tareas activas. |
| `RESUMEN_TAREAS_1_Y_3.md` | Implementación de botones unificados y barra de filtros `.filtros-bar`. | **Completado.** | Se valida que las vistas principales ya usan `.filtros-bar` y `.filter-btn`. |
| `AUDITORIA_TAREA_8_LOCALSTORAGE.md` | Auditoría de `localStorage` vs estado servidor. | **Completado.** | Se confirma sincronización de tokens y preferencias. |
| `MEJORAS_PENDIENTES_CITAS_PROFESIONALES.md` | Diagnóstico de mejoras reales pendientes. | **Vigente.** Base de este plan unificado. | Incorporado directamente en las tareas activas de este documento. |
| `PROMPT_CONTINUACION_TAREAS_PENDIENTES.md` | Prompts y especificaciones para dropdowns y filtros. | **Completado en vistas clave.** | Se depura el contenido completado y se adapta para las vistas auxiliares pendientes. |
| `RESUMEN_IMPLEMENTACION_MEJORAS.md` | Mejoras en `st-odo-01` y `st-odo-02` (KPIs, banner urgente). | **Completado.** | Confirmado en controladores y vistas de profesionales. |
| `ANALISIS_TABLAS_Y_VISTAS_BD.md` | Optimización de consultas LINQ y vistas SQL Server. | **Vigente como guía de performance.** | Integrado en los criterios de validación de backend. |

---

## ✅ 2. Tareas Completadas (Eliminadas del Backlog Activo)

Las siguientes tareas **ya están codificadas, probadas y validadas en la solución**. **NO DEBEN VOLVER A IMPLEMENTARSE NI INCLUIRSE EN FUTUROS BACKLOGS**:

1. **✅ TAREA 1: Unificación de Botones:** Aplicación de clases estandarizadas (`.btn-primary`, `.btn-secondary`, `.btn-danger`, `.btn-outline-*`) en todos los módulos de Citas y Profesionales.
2. **✅ TAREA 2 & 3: Barra de Filtros y Dropdowns Custom:** Creación e integración de `wwwroot/js/shared/dropdown-filters.js` y `wwwroot/css/shared/filter-components.css` con la clase `.filtros-bar` en las vistas principales (`st-odo-02`, `st-adm-09`, `st-aux-05`, `st-pac-01`).
3. **✅ TAREA 4: Stat-Cards Estandarizadas:** Integración de la estructura unificada `<div class="stat-card"><span class="stat-number">...</span><span class="stat-label">...</span><span class="stat-sub">...</span></div>` en dashboards y listados.
4. **✅ TAREA 5: Agenda de Apoyo (`st-aux-02`):** Separación de controles de vista (`.vista-controls-bar`) y fecha dinámica renderizada en `#fechaActivaDisplay`.
5. **✅ TAREA 6: Carga Real de Citas con Fallback:** Implementación de `fetchAppointments()` consultando la API real `/api/citas` con manejo de estados vacíos en `st-adm-09`.
6. **✅ TAREA 7: Campo Medicamentos:** Adición de la columna y propiedad `Medicamentos` en la entidad `Paciente` y su despliegue en la vista de historial parcial (`st-aux-05`).
7. **✅ TAREA 8: Auditoría de `localStorage`:** Reemplazo de almacenamiento frágil por llamadas sincronizadas con cookies HTTP-Only y tokens CSRF.
8. **✅ Mejoras de Dashboard Profesional (`st-odo-01`):** Banner de próxima cita urgente, barra de progreso de atención diaria y widgets KPI.

---

## 🎯 3. Plan Unificado de Mejoras Pendientes

### Bloque 1: Consolidación Visual de Vistas Auxiliares y Secundarias
**Ámbito:** Módulo de Citas y Notificaciones  
**Vistas a intervenir:**
- `Views/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.cshtml`
- `Views/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.cshtml`
- `Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml`
- `Views/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.cshtml`
- `Views/Gestion_De_Citas/st-pac-03-notificaciones/index.cshtml`
- `Views/Gestion_De_Citas/st-rec-05-recordatorios/recordatorios.cshtml`

**Acciones requeridas:**
- Homologar los encabezados de página (`page-header`, `page-title`, `page-subtitle`).
- Asegurar que todas las tablas utilicen la clase wrapper `.table-responsive` y botones de acción unificados.
- Eliminar estilos CSS inline remanentes y reemplazarlos por variables CSS globales (`var(--bg-secondary)`, `var(--border-color)`, `var(--blue-500)`).

---

### Bloque 2: Accesibilidad, Teclado y Semántica ARIA
**Ámbito:** Gestión de Citas y Gestión de Profesionales  
**Acciones requeridas:**
- Verificar que todos los dropdowns custom creados en `dropdown-filters.js` manejen correctamente `aria-expanded`, `aria-haspopup="listbox"` y navegación con flechas `Up`/`Down`, `Enter` y `Escape`.
- Garantizar foco visible (`outline: 2px solid var(--blue-500)`) en elementos interactivos al tabular.
- Agregar atributos `role="status"` o `aria-live="polite"` en contenedores de carga asíncrona de citas y tablas dinámicas.

---

### Bloque 3: Unificación de Mensajes y Estados Vacíos
**Ámbito:** Todas las vistas de Citas y Profesionales  
**Acciones requeridas:**
- Estandarizar la estructura HTML para componentes sin datos:
  ```html
  <div class="empty-state" role="status">
    <div class="empty-state-icon"><i class="bi bi-calendar-x"></i></div>
    <h4 class="empty-state-title">No se encontraron citas</h4>
    <p class="empty-state-desc">Intenta ajustar los filtros de búsqueda o la fecha seleccionada.</p>
  </div>
  ```
- Reemplazar mensajes inconsistentes ("Sin registros", "0 resultados", o tablas vacías sin aviso) por este componente unificado.

---

### Bloque 4: Responsividad Móvil y Tablas Adaptativas
**Ámbito:** Dispositivos móviles y tablets (pantallas < 768px)  
**Acciones requeridas:**
- Ajustar la barra `.filtros-bar` para apilar elementos verticalmente en móviles (`flex-direction: column; align-items: stretch;`).
- Permitir scroll horizontal suave en las grillas semanales de agenda (`st-odo-02`, `st-adm-08`).
- Ajustar modales de creación/edición de citas para no desbordar el viewport móvil.

---

### Bloque 5: Refuerzo de Reportes Clínicos y Gestión de Profesionales
**Ámbito:** Módulo de Gestión de Profesionales  
**Vistas a intervenir:**
- `Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/reportes-clinicos.cshtml`
- `Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/index.cshtml`

**Acciones requeridas:**
- Aplicar la barra de filtros `.filtros-bar` en la vista de reportes clínicos (`st-adm-14`).
- Mejorar la visualización de KPI cards con desglose de rendimiento por especialidad.
- Garantizar que las acciones de activación/desactivación de profesionales en `st-adm-07` soliciten confirmación modal con el nuevo diseño estándar.

---

### Bloque 6: Coherencia de Modales y Acciones Reutilizables
**Ámbito:** Gestión de Citas  
**Acciones requeridas:**
- Unificar las ventanas modales de **"Cancelar Cita"**, **"Reagendar Cita"** y **"Confirmar Asistencia"** en las vistas `st-odo-02`, `st-adm-08`, `st-rec-03` y `st-adm-09`.
- Asegurar que todas las peticiones modal envíen el encabezado `X-XSRF-TOKEN` o el token Antiforgery correspondiente.

---

### Bloque 7: Validación de Flujo de Negocio End-to-End por Rol
**Ámbito:** Integración completa Backend + Frontend  
**Matriz de verificación por Rol:**
1. **Paciente (`st-pac-01`):** Reserva cita -> ve estado Programada -> recibe notificación (`st-pac-03`).
2. **Recepcionista (`st-rec-03`):** Visualiza la cita -> confirma asistencia -> asigna consultorio libre.
3. **Odontólogo (`st-odo-02`):** Visualiza la cita en su agenda -> presiona "Iniciar Atención" -> el estado cambia a `En consulta` -> presiona "Marcar Atendida" -> el estado pasa a `Atendida` y registra historial en `Cita_Historial_Estado`.
4. **Administrador (`st-adm-09` & `st-adm-07`):** Administra citas masivas, gestiona profesionales y genera reportes sin romper la integridad referencial.

---

## 🚀 4. Prompts de Ejecución Listos para Copiar y Pegar

### 📌 Prompt 1: Consolidación Visual de Vistas Auxiliares (Bloque 1)
```text
Por favor, revisa y consolida visualmente las vistas auxiliares de citas:
1. `Views/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.cshtml`
2. `Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml`
3. `Views/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.cshtml`
4. `Views/Gestion_De_Citas/st-pac-03-notificaciones/index.cshtml`
5. `Views/Gestion_De_Citas/st-rec-05-recordatorios/recordatorios.cshtml`

Requisitos:
- Aplica las clases de botones estandarizadas (`.btn-primary`, `.btn-secondary`, `.btn-outline-*`).
- Asegura que las tablas estén dentro de un div `.table-responsive`.
- Homologa los títulos y subtítulos con las clases `page-header`, `page-title` y `page-subtitle`.
- No alteres la lógica JavaScript ni los endpoints del controlador.
- Muestra el git diff al finalizar cada vista.
```

---

### 📌 Prompt 2: Estandarización de Estados Vacíos y Accesibilidad (Bloques 2 y 3)
```text
Implementa el componente unificado de estado vacío y mejoras de accesibilidad en los módulos de Citas y Profesionales:
1. Reemplaza cualquier contenedor de resultados vacíos por la estructura estándar:
   <div class="empty-state" role="status">
     <div class="empty-state-icon"><i class="bi bi-inbox"></i></div>
     <h4 class="empty-state-title">No hay datos disponibles</h4>
     <p class="empty-state-desc">No se encontraron registros con los filtros seleccionados.</p>
   </div>
2. Asegura que los dropdowns custom de `dropdown-filters.js` cuenten con los atributos `aria-expanded` y `aria-haspopup`.
3. Confirma la navegación por teclado (Tab, Space, Enter, Escape) en tablas y modales de las vistas `st-adm-09-citas` y `st-adm-07-gestion-profesionales`.
4. Verifica los cambios y muestra el git diff.
```

---

### 📌 Prompt 3: Refuerzo de Reportes Clínicos y Gestión de Profesionales (Bloque 5)
```text
Actualiza la vista de reportes clínicos y gestión de profesionales (`Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/reportes-clinicos.cshtml` y `st-adm-07-gestion-profesionales/index.cshtml`):
1. Integra la barra de filtros `.filtros-bar` usando el componente reutilizable `dropdown-filters.js`.
2. Estandariza las tarjetas de estadísticas KPI utilizando la clase `.stat-card` con su respectivo `<span class="stat-sub">`.
3. Valida la responsividad móvil en resoluciones menores a 768px.
4. Muestra los cambios realizados mediante git diff.
```

---

### 📌 Prompt 4: Verificación Final del Flujo de Negocio (Bloque 7)
```text
Realiza una verificación completa del flujo de citas y profesionales en el backend y frontend:
1. Comprueba que el cambio de estado de una cita en `GestionCitasController.cs` inserte correctamente en la tabla de auditoría `Cita_Historial_Estado`.
2. Verifica que las validaciones de traslape de horario y disponibilidad de consultorio impidan citas duplicadas.
3. Asegura que todas las llamadas `fetch` envíen las credenciales y el encabezado `X-XSRF-TOKEN`.
4. Ejecuta las pruebas unitarias del proyecto con `dotnet test` para confirmar la estabilidad del sistema.
```

---

## 🛠️ 5. Plan de Verificación y Control de Calidad

Para dar por completado este plan, se deberán superar las siguientes pruebas de verificación:

1. **Prueba de Compilación y Servidor:**
   ```bash
   dotnet build SmileTrack_MVC.slnx
   ```
   *Criterio de éxito:* 0 errores de compilación.

2. **Prueba de Pruebas Unitarias:**
   ```bash
   dotnet test SmileTrack_MVC.slnx
   ```
   *Criterio de éxito:* Todas las pruebas pasadas con éxito.

3. **Verificación Visual y Responsiva:**
   - Navegación móvil fluida sin desbordamientos en 320px, 375px y 768px.
   - Navegación por teclado 100% funcional en filtros, modales y tablas.

4. **Verificación de Base de Datos:**
   - Citas correctamente guardadas en SQL Server sin violar restricciones `FK` ni generar traslapes no autorizados.
