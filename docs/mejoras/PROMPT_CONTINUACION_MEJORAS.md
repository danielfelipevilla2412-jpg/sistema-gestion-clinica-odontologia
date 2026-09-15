# 🤖 PROMPT PARA CONTINUAR CON LAS MEJORAS DEL SISTEMA

## 📋 CONTEXTO

Este documento contiene el prompt completo para que un agente de IA (o desarrollador) pueda continuar implementando las mejoras identificadas en la auditoría del sistema SmileTrack.

---

## 🎯 PROMPT PRINCIPAL

```
Eres un desarrollador senior full-stack especializado en ASP.NET Core MVC, Entity Framework Core y SQL Server. 
Estás trabajando en el proyecto SmileTrack, un sistema de gestión clínica odontológica para SENA.

CONTEXTO HISTÓRICO:
Ya se implementaron mejoras completas en 2 vistas del módulo de profesionales:
- st-odo-01-dashboard (8 mejoras de dashboard)
- st-odo-02-agenda (9 mejoras de agenda)

Estas mejoras incluyeron:
- Backend: 6 consultas SQL optimizadas en ProfesionalesController
- Backend: Estadísticas adicionales en GestionCitasController
- Frontend: 2 vistas HTML mejoradas con nuevos componentes
- CSS: 1,000+ líneas agregadas con animaciones y responsive
- JavaScript: 700+ líneas con funcionalidades avanzadas

DOCUMENTACIÓN DISPONIBLE:
En la carpeta `docs/mejoras/` encontrarás 4 documentos clave:

1. **README.md** - Índice general y guía de navegación
2. **AUDITORIA_APLICABILIDAD_MEJORAS.md** - Análisis detallado de aplicabilidad por vista
3. **MATRIZ_VISUAL_MEJORAS.md** - Tablas visuales y priorización ejecutiva
4. **PLAN_IMPLEMENTACION_COMPONENTES.md** - Arquitectura de componentes reutilizables

LEE PRIMERO estos documentos en orden para entender:
- Qué mejoras existen (D1-D8 dashboard, A1-A9 agenda)
- Qué vistas hay en el sistema (17 pendientes)
- Cuáles son aplicables a cada vista
- Cómo priorizarlas
- Cómo implementarlas con componentes reutilizables

TAREA PRINCIPAL:
Necesito que implementes las mejoras identificadas en la auditoría para las vistas restantes del sistema, 
siguiendo la priorización recomendada y usando la arquitectura de componentes reutilizables propuesta.

ENFOQUE:
1. Crea primero la biblioteca de componentes compartidos (ViewComponents)
2. Implementa por módulos completos, no vista por vista aislada
3. Comienza con el módulo de Recepción (mayor ROI)
4. Sigue con Admin Citas, Pacientes, Auxiliares
5. Mantén consistencia con lo ya implementado en st-odo-01 y st-odo-02

RESTRICCIONES:
- NO duplicar código, usa componentes reutilizables
- NO alterar la estructura de base de datos sin consultar
- SÍ mantener integración con SQL Server directo
- SÍ seguir patrones ya establecidos en el proyecto
- SÍ probar cada mejora antes de marcarla como completa

¿Estás listo para comenzar?
```

---

## 📝 PROMPTS ESPECÍFICOS POR FASE

### **FASE 1: Crear Biblioteca de Componentes**

```
FASE 1: BIBLIOTECA DE COMPONENTES COMPARTIDOS

Lee el documento `PLAN_IMPLEMENTACION_COMPONENTES.md` que contiene:
- Estructura de carpetas completa
- Código de ejemplo para 8 componentes
- ViewModels necesarios
- ViewComponents con lógica de negocio
- JavaScript y CSS asociados

TAREA:
Crea la biblioteca de componentes reutilizables siguiendo EXACTAMENTE la arquitectura propuesta:

COMPONENTES A CREAR:
1. ProximaCitaWidget - Widget de próxima cita con countdown
2. BuscadorTiempoReal - Búsqueda con debounce y highlight
3. PanelEstadisticas - Panel de KPIs con animación
4. NotificacionesInteligentes - Notificaciones contextuales
5. ToggleVistas - Cambio entre vistas (Semana/Día/Lista)
6. PanelRendimiento - Círculo SVG animado con porcentaje
7. IndicadoresTiempo - Indicadores EN CURSO/RETRASADA
8. AccesosRapidos - Tarjetas de acceso rápido

ESTRUCTURA REQUERIDA:
Views/Shared/Components/
├── [NombreComponente]/
│   ├── Default.cshtml
│   └── [NombreComponente]ViewComponent.cs

wwwroot/css/shared/components/
├── [nombre-componente].css

wwwroot/js/shared/components/
├── [NombreComponente].js

Models/ViewModels/Components/
├── [NombreComponente]ViewModel.cs

IMPORTANTE:
- Usa el código de ejemplo del documento como base
- Adapta las consultas SQL al contexto de cada componente
- Asegúrate de que funcionen para múltiples roles (admin, recepcionista, paciente, etc.)
- Incluye parámetros opcionales para personalización (idProfesional, idPaciente, etc.)
- Agrega comentarios explicativos en el código

Una vez creados todos los componentes, confirma que:
✅ Todos compilan sin errores
✅ ViewComponents retornan vistas correctamente
✅ CSS está vinculado correctamente
✅ JavaScript se inicializa automáticamente
✅ ViewModels tienen propiedades necesarias

¿Comenzamos con el primer componente?
```

---

### **FASE 2: Módulo de Recepción (PRIORIDAD CRÍTICA)**

```
FASE 2: MEJORAS EN MÓDULO DE RECEPCIÓN

Consulta `AUDITORIA_APLICABILIDAD_MEJORAS.md` sección "ST-REC-01-DASHBOARD" y "ST-REC-03-GESTION-CITAS".

VISTAS A MEJORAR:
1. st-rec-01-dashboard (Dashboard Recepción)
2. st-rec-03-gestion-citas (Gestión de Citas Recepción)
3. st-rec-05-recordatorios (Recordatorios)

═══════════════════════════════════════════════════════════════
VISTA 1: ST-REC-01-DASHBOARD
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES (según auditoría):
✅ D4 - Notificaciones inteligentes (CRÍTICO)
✅ D5 - Barra de progreso con datos reales
✅ D2 - Accesos rápidos
✅ D7 - Auto-actualización (CRÍTICO)
✅ D8 - Responsive mejorado
✅ D6 - Tooltips en KPIs

PASO 1: BACKEND (Controller)
Ubicación: Controllers/GestionCitasController.cs (método RecepcionDashboard o similar)

Agregar consultas SQL para:
1. Pacientes en sala de espera (IdEstadoCita = 2, FechaHora <= ahora, hoy)
2. Citas sin confirmar próximas 24h (IdEstadoCita = 1, FechaHora entre ahora y +24h)
3. Facturas pendientes del día
4. Progreso del día (atendidas / total * 100)
5. Urgencias (citas retrasadas > 15 min)

Código de referencia:
var ahora = DateTime.Now;
var pacientesEspera = await _context.Citas
    .Where(c => c.IdEstadoCita == 2)
    .Where(c => c.FechaHora.Date == ahora.Date)
    .Where(c => c.FechaHora <= ahora)
    .CountAsync();

ViewData["PacientesEspera"] = pacientesEspera;
// ... más consultas

PASO 2: VISTA (Razor)
Ubicación: Views/Gestion_De_Citas/st-rec-01-dashboard/index.cshtml

Agregar componentes:
@* Panel de notificaciones inteligentes *@
@await Component.InvokeAsync("NotificacionesInteligentes", new { 
    tipo = "recepcion",
    pacientesEspera = ViewData["PacientesEspera"],
    citasSinConfirmar = ViewData["CitasSinConfirmar"],
    facturasPendientes = ViewData["FacturasPendientes"]
})

@* Accesos rápidos *@
@await Component.InvokeAsync("AccesosRapidos", new {
    accesos = new[] {
        new { titulo = "Nuevo Paciente", icono = "person_add", url = "/pacientes/nuevo", color = "green" },
        new { titulo = "Agendar Cita", icono = "event", url = "/citas/nueva", color = "blue" },
        new { titulo = "Generar Factura", icono = "receipt_long", url = "/facturacion/nueva", color = "purple" },
        new { titulo = "Sala de Espera", icono = "people", url = "/sala-espera", color = "orange" }
    }
})

@* Barra de progreso mejorada *@
@await Component.InvokeAsync("BarraProgreso", new {
    actual = ViewData["CitasAtendidas"],
    total = ViewData["CitasHoy"],
    etiqueta = "Atención del día"
})

PASO 3: CSS
Ubicación: wwwroot/css/Gestion_De_Citas/st-rec-01-dashboard/styles.css

Agregar imports de componentes:
@import url('/css/shared/components/notificaciones.css');
@import url('/css/shared/components/accesos-rapidos.css');
@import url('/css/shared/components/barra-progreso.css');

Agregar estilos específicos de recepción si es necesario.

PASO 4: JAVASCRIPT
Ubicación: wwwroot/js/Gestion_De_Citas/st-rec-01-dashboard/app.js

Agregar auto-actualización:
// Auto-actualizar cada 30 segundos (crítico para recepción)
setInterval(async () => {
    const response = await fetch('/api/recepcion/dashboard-stats');
    const data = await response.json();
    
    // Actualizar contadores
    document.getElementById('statPacientesEspera').textContent = data.pacientesEspera;
    document.getElementById('statCitasSinConfirmar').textContent = data.citasSinConfirmar;
    // ... más actualizaciones
    
    console.log('📊 Dashboard recepción actualizado');
}, 30000);

VERIFICACIÓN:
Una vez implementado st-rec-01-dashboard, verifica:
✅ Notificaciones muestran datos reales de BD
✅ Accesos rápidos navegan correctamente
✅ Barra de progreso calcula porcentaje correcto
✅ Auto-actualización funciona sin errores de consola
✅ Responsive funciona en móvil (recepcionistas usan tablets)
✅ Tooltips explican cada KPI claramente

═══════════════════════════════════════════════════════════════
VISTA 2: ST-REC-03-GESTION-CITAS
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES:
✅ A2 - Búsqueda en tiempo real (CRÍTICO)
✅ A3 - Filtro por estado con contadores
✅ A6 - Panel de estadísticas rápidas
✅ A7 - Atajos de teclado
✅ A8 - Exportación PDF
✅ A9 - Accesibilidad mejorada

PASO 1: BACKEND
Ya tiene stats-grid, mejorar con contadores dinámicos por estado.

PASO 2: VISTA
Agregar componentes:
@await Component.InvokeAsync("BuscadorTiempoReal", new {
    placeholder = "Buscar por paciente, profesional o fecha...",
    targetSelector = "tbody tr"
})

@await Component.InvokeAsync("FiltroEstado", new {
    estados = ViewData["EstadosCita"],
    contadores = new {
        programadas = ViewData["StatRecProgramadas"],
        confirmadas = ViewData["StatRecConfirmadas"],
        atendidas = ViewData["StatRecAtendidas"],
        canceladas = ViewData["StatRecCanceladas"]
    }
})

PASO 3: JAVASCRIPT
Agregar atajos de teclado:
- Ctrl+N: Nueva cita
- Ctrl+F: Enfocar búsqueda
- Ctrl+P: Imprimir/Exportar

═══════════════════════════════════════════════════════════════
VISTA 3: ST-REC-05-RECORDATORIOS
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES:
✅ A2 - Búsqueda (parcial)
✅ A6 - Panel de stats (recordatorios enviados/pendientes)

Implementar según patrón similar a las anteriores.

CUANDO TERMINES EL MÓDULO DE RECEPCIÓN:
Genera un reporte con:
- Archivos modificados
- Líneas de código agregadas
- Componentes reutilizados
- Testing realizado
- Screenshots (si es posible)

¿Comenzamos con st-rec-01-dashboard?
```

---

### **FASE 3: Módulo Admin Citas**

```
FASE 3: MEJORAS EN MÓDULO ADMIN CITAS

Consulta `AUDITORIA_APLICABILIDAD_MEJORAS.md` secciones relevantes.

VISTAS A MEJORAR:
1. st-adm-01-dashboard (Dashboard Admin Citas)
2. st-adm-08-agenda (Agenda General)
3. st-adm-09-citas (Vista Tabular Citas)

═══════════════════════════════════════════════════════════════
VISTA 1: ST-ADM-01-DASHBOARD
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES:
✅ D3 - Panel de rendimiento SVG (ocupación mensual)
✅ D5 - Barra de progreso datos reales
✅ D6 - KPIs con tooltips
✅ D7 - Auto-actualización
✅ D8 - Responsive mejorado
✅ D2 - Accesos rápidos (parcial)

BACKEND:
Controller: GestionCitasController o CitasDashboardController
Método: Index o Dashboard

Agregar:
- Ocupación real de agenda (no estimada)
- Tendencia de citas (comparado con mes anterior)
- Profesionales más solicitados
- Servicios más demandados

VISTA:
@await Component.InvokeAsync("PanelRendimiento", new {
    porcentaje = Model.OcupacionPorcentaje,
    etiqueta = "Ocupación mensual",
    meta = 85,
    tipo = "ocupacion"
})

@await Component.InvokeAsync("PanelEstadisticas", new {
    estadisticas = new[] {
        new EstadisticaItem { 
            Color = "blue", 
            Valor = Model.Kpis.PacientesActivos,
            Etiqueta = "Pacientes activos",
            Tooltip = "Total de pacientes con citas activas en el sistema"
        },
        // ... más stats
    }
})

═══════════════════════════════════════════════════════════════
VISTA 2: ST-ADM-08-AGENDA (PRIORIDAD ALTA)
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES:
✅ A1 - Toggle de vistas (CRÍTICO)
✅ A2 - Búsqueda tiempo real (CRÍTICO)
✅ A3 - Filtro por estado
✅ A4 - Indicadores tiempo real (CRÍTICO)
✅ A6 - Panel estadísticas rápidas
✅ A7 - Atajos de teclado
✅ A8 - Exportación PDF
✅ A9 - Accesibilidad

BACKEND:
Controller: GestionCitasController
Método: AgendaGeneral o similar

Agregar estadísticas:
- Citas por estado (programadas, confirmadas, en curso, atendidas, canceladas)
- Citas por profesional (top 5)
- Consultorios más ocupados
- Horas pico del día

VISTA:
@await Component.InvokeAsync("ToggleVistas", new {
    vistaActual = "semana",
    vistas = new[] { "semana", "dia", "lista" }
})

@await Component.InvokeAsync("BuscadorTiempoReal", new {
    placeholder = "Buscar paciente en toda la agenda...",
    targetSelector = ".appointment:not(.available)"
})

@await Component.InvokeAsync("PanelEstadisticas", new {
    estadisticas = new[] {
        new { Color = "blue", Valor = Model.TotalCitasSemana, Etiqueta = "Total semana" },
        new { Color = "green", Valor = Model.CitasConfirmadas, Etiqueta = "Confirmadas" },
        new { Color = "orange", Valor = Model.CitasProgramadas, Etiqueta = "Programadas" },
        new { Color = "red", Valor = Model.CitasAtendidas, Etiqueta = "Atendidas" }
    }
})

JAVASCRIPT:
// Indicadores de tiempo real
@await Component.InvokeAsync("IndicadoresTiempo", new {
    selector = ".appointment",
    actualizarCada = 60000 // 1 minuto
})

NOTA IMPORTANTE:
st-adm-08-agenda es similar a st-odo-02-agenda pero para TODOS los profesionales.
Reutiliza TODOS los componentes ya creados para st-odo-02.
La diferencia está en los filtros (por profesional y consultorio).

═══════════════════════════════════════════════════════════════
VISTA 3: ST-ADM-09-CITAS
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES:
✅ A2 - Búsqueda tiempo real
✅ A3 - Filtro por estado
✅ A7 - Atajos teclado
✅ A8 - Exportación PDF/Excel
✅ A9 - Accesibilidad

Similar a st-rec-03 pero con permisos de administrador.

¿Continuamos con st-adm-01-dashboard?
```

---

### **FASE 4: Módulo Pacientes**

```
FASE 4: MEJORAS EN MÓDULO PACIENTES

VISTAS A MEJORAR:
1. st-pac-01-mis-citas (Mis Citas)
2. st-pac-03-notificaciones (Notificaciones)

═══════════════════════════════════════════════════════════════
VISTA 1: ST-PAC-01-MIS-CITAS (ALTA PRIORIDAD UX)
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES:
✅ D1 - Próxima cita urgente (CRÍTICO para paciente)
✅ A2 - Búsqueda tiempo real
✅ A3 - Filtro por estado
✅ D5 - Barra de progreso
✅ D6 - Tooltips
✅ A8 - Exportación PDF (historial)
✅ A9 - Accesibilidad (CRÍTICO)

BACKEND:
Controller: GestionCitasController
Método: CitasPaciente o MisCitas

Obtener IdPaciente desde Claims:
var idPaciente = int.Parse(User.FindFirst("IdPaciente").Value);

Agregar:
- Próxima cita del paciente
- Historial completo de citas
- Última atención
- Próximas citas programadas (top 3)

VISTA:
@* Banner de próxima cita - MUY IMPORTANTE PARA PACIENTE *@
@await Component.InvokeAsync("ProximaCitaWidget", new { 
    idPaciente = User.FindFirst("IdPaciente")?.Value 
})

@* Búsqueda en su historial *@
@await Component.InvokeAsync("BuscadorTiempoReal", new {
    placeholder = "Buscar en mis citas por doctor o servicio...",
    targetSelector = "#citasTable tbody tr"
})

@* Exportar historial *@
<button onclick="exportarHistorial()" class="btn-secondary">
    <span class="material-symbols-outlined">download</span>
    Descargar mi historial
</button>

ACCESIBILIDAD:
CRÍTICO: Los pacientes pueden tener discapacidades.
- Todos los botones con aria-label
- Tabla con headers correctos
- Navegación completa por teclado
- Alto contraste en estados de citas
- Screen reader friendly

═══════════════════════════════════════════════════════════════
VISTA 2: ST-PAC-03-NOTIFICACIONES
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES:
✅ D4 - Notificaciones inteligentes
✅ D7 - Auto-actualización (tiempo real)
✅ D8 - Responsive

BACKEND:
Notificaciones del paciente:
- Citas próximas (próximas 24h)
- Cambios en citas
- Recordatorios
- Resultados disponibles
- Facturas pendientes

JAVASCRIPT:
// Polling cada 60 segundos para nuevas notificaciones
setInterval(async () => {
    const response = await fetch('/api/paciente/notificaciones/nuevas');
    const data = await response.json();
    
    if (data.hayNuevas) {
        mostrarBadgeNotificaciones(data.cantidad);
        actualizarListaNotificaciones(data.notificaciones);
    }
}, 60000);

¿Comenzamos con st-pac-01-mis-citas?
```

---

### **FASE 5: Módulo Auxiliares**

```
FASE 5: MEJORAS EN MÓDULO AUXILIARES

VISTAS A MEJORAR:
1. st-aux-01-panel-operativo
2. st-aux-02-agenda-apoyo
3. st-aux-05-historial-parcial
4. st-aux-06-asistencia-procedi
5. st-aux-09-estado-consultorio
6. st-aux-10-citas-finalizadas

NOTA: Estas vistas requieren revisión detallada primero.

PASO PREVIO:
Lee cada vista auxiliar para entender:
- ¿Qué hace la vista?
- ¿Qué datos muestra?
- ¿Qué acciones permite?
- ¿Qué rol tiene el auxiliar en el flujo?

═══════════════════════════════════════════════════════════════
VISTA 1: ST-AUX-01-PANEL-OPERATIVO
═══════════════════════════════════════════════════════════════

MEJORAS ESTIMADAS:
✅ A4 - Indicadores tiempo real (CRÍTICO para apoyo)
✅ D4 - Notificaciones (pacientes listos, material faltante)
✅ D7 - Auto-actualización (CRÍTICO)
✅ A2 - Búsqueda paciente

Si es panel operativo en tiempo real:
- Auto-actualizar cada 30 segundos
- Mostrar estado de consultorios
- Alertas de pacientes listos
- Material/instrumental requerido

═══════════════════════════════════════════════════════════════
VISTA 2: ST-AUX-02-AGENDA-APOYO
═══════════════════════════════════════════════════════════════

MEJORAS ESTIMADAS:
✅ A1 - Toggle vistas
✅ A4 - Indicadores tiempo real
✅ A2 - Búsqueda
✅ A6 - Panel stats

Similar a st-odo-02-agenda pero para auxiliares.
Reutilizar componentes de agenda.

═══════════════════════════════════════════════════════════════
VISTAS 3-6: OTRAS VISTAS AUXILIARES
═══════════════════════════════════════════════════════════════

Revisar una por una y aplicar mejoras genéricas:
- Búsqueda si tiene tablas
- Accesibilidad siempre
- Responsive siempre
- Tooltips en métricas

¿Revisamos primero las vistas auxiliares antes de implementar?
```

---

### **FASE 6: Módulo Admin Profesionales**

```
FASE 6: MEJORAS EN MÓDULO ADMIN PROFESIONALES

VISTAS A MEJORAR:
1. st-adm-07-gestion-profesionales
2. st-adm-14-reportes-clinicos
3. st-odo-09-perfil-profesional

═══════════════════════════════════════════════════════════════
VISTA 1: ST-ADM-07-GESTION-PROFESIONALES
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES:
✅ A2 - Búsqueda tiempo real
✅ D6 - Tooltips en stats
✅ A7 - Atajos teclado (Ctrl+N nuevo profesional)
✅ A8 - Exportación lista
✅ A9 - Accesibilidad

Ya tiene búsqueda, mejorar con:
- Debounce 300ms
- Highlight de resultados
- Contador de resultados

Tooltips en stats:
- Total: "Todos los profesionales registrados"
- Activos: "Profesionales en servicio activo"
- Vacaciones: "Profesionales con ausencia programada"
- Inactivos: "Profesionales dados de baja"

═══════════════════════════════════════════════════════════════
VISTA 2: ST-ADM-14-REPORTES-CLINICOS
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES:
✅ A2 - Búsqueda (por paciente, profesional, diagnóstico)
✅ A3 - Filtros mejorados
✅ D3 - Visualización gráfica (parcial)
✅ A8 - Exportación PDF
✅ D6 - Tooltips

Búsqueda en:
- Nombre paciente
- Nombre profesional
- Diagnóstico
- Procedimiento realizado

═══════════════════════════════════════════════════════════════
VISTA 3: ST-ODO-09-PERFIL-PROFESIONAL
═══════════════════════════════════════════════════════════════

MEJORAS APLICABLES:
✅ A9 - Accesibilidad (formularios)
✅ D8 - Responsive

Mejoras básicas:
- Labels correctos en formularios
- Validación client-side
- Mensajes de error claros
- Responsive para editar desde móvil

¿Comenzamos con st-adm-07-gestion-profesionales?
```

---

## 🎯 CHECKLIST GLOBAL POR VISTA

Usa este checklist para CADA vista que mejores:

```
VISTA: [nombre-vista]
FECHA: [fecha]

PREPARACIÓN:
☐ Leí la sección de la vista en AUDITORIA_APLICABILIDAD_MEJORAS.md
☐ Identifiqué las mejoras aplicables según la auditoría
☐ Revisé el código actual de la vista
☐ Identifiqué qué componentes puedo reutilizar

BACKEND:
☐ Agregué consultas SQL necesarias al controller
☐ Populé ViewData/ViewBag con datos reales
☐ Optimicé queries (Include, AsNoTracking, etc.)
☐ Probé que no hay errores de SQL

VISTA (RAZOR):
☐ Agregué componentes usando @await Component.InvokeAsync(...)
☐ Pasé parámetros correctos a cada componente
☐ Mantuve estructura HTML semántica
☐ Agregué data-attributes para JavaScript
☐ Verifiqué que no hay errores de sintaxis Razor

CSS:
☐ Importé CSS de componentes compartidos
☐ Agregué estilos específicos de la vista si es necesario
☐ Verifiqué responsive en móvil (320px, 768px, 1024px)
☐ Probé hover states y animaciones
☐ Validé contraste de colores (accesibilidad)

JAVASCRIPT:
☐ Importé scripts de componentes
☐ Agregué inicializadores si es necesario
☐ Implementé auto-actualización si aplica
☐ Agregué atajos de teclado si aplica
☐ Probé en consola que no hay errores

TESTING:
☐ Vista carga sin errores
☐ Componentes muestran datos correctos
☐ Búsqueda funciona (si aplica)
☐ Filtros funcionan (si aplica)
☐ Botones navegan correctamente
☐ Responsive funciona en móvil
☐ Accesibilidad: navegación por teclado OK
☐ Accesibilidad: screen reader OK
☐ Performance: carga < 2 segundos

DOCUMENTACIÓN:
☐ Comenté código complejo
☐ Actualicé README si es necesario
☐ Agregué screenshots (opcional)

COMMIT:
☐ Commit descriptivo: "feat: mejoras en [vista] - [lista de mejoras]"
☐ Push a branch específico de la fase

REPORTE:
☐ Agregué vista al reporte de progreso
☐ Documenté archivos modificados
☐ Registré líneas de código agregadas
☐ Noté componentes reutilizados
```

---

## 📊 REPORTE DE PROGRESO

Después de cada vista o fase completada, genera un reporte:

```
REPORTE DE PROGRESO - FASE [N]

FECHA: [fecha]
VISTAS COMPLETADAS: [N/17]

═══════════════════════════════════════════════════════════════
VISTA: [nombre-vista]
═══════════════════════════════════════════════════════════════

MEJORAS IMPLEMENTADAS:
✅ [ID] - [Nombre de mejora]
✅ [ID] - [Nombre de mejora]
...

ARCHIVOS MODIFICADOS:
- Controllers/[Controller].cs (+XX líneas)
- Views/[Modulo]/[vista]/index.cshtml (+XX líneas)
- wwwroot/css/[ruta]/styles.css (+XX líneas)
- wwwroot/js/[ruta]/app.js (+XX líneas)

COMPONENTES REUTILIZADOS:
- [NombreComponente]
- [NombreComponente]
...

TIEMPO ESTIMADO VS REAL:
Estimado: XX horas
Real: XX horas
Diferencia: +/- XX%

DESAFÍOS ENCONTRADOS:
- [Desafío 1]
- [Desafío 2]

PRÓXIMA VISTA:
[nombre-siguiente-vista]

═══════════════════════════════════════════════════════════════
ESTADÍSTICAS ACUMULADAS
═══════════════════════════════════════════════════════════════

Total vistas mejoradas: [N/17] ([XX]%)
Total mejoras aplicadas: [N]
Total líneas agregadas: [N]
Total componentes creados: [N]
Total componentes reutilizados: [N] veces

Fase actual: [N/6]
Progreso global: [XX]%
ETA finalización: [fecha estimada]
```

---

## ⚠️ REGLAS IMPORTANTES

```
NUNCA:
❌ Duplicar código de componentes
❌ Modificar base de datos sin consultar
❌ Saltar testing de una vista
❌ Implementar sin leer la auditoría primero
❌ Mezclar mejoras de diferentes fases
❌ Commit sin mensaje descriptivo

SIEMPRE:
✅ Reutilizar componentes existentes
✅ Seguir la priorización de la auditoría
✅ Probar en navegador antes de continuar
✅ Mantener consistencia con st-odo-01 y st-odo-02
✅ Comentar código complejo
✅ Verificar accesibilidad
✅ Generar reportes de progreso

SI TIENES DUDAS:
🤔 Consulta AUDITORIA_APLICABILIDAD_MEJORAS.md
🤔 Revisa código de st-odo-01 o st-odo-02 como referencia
🤔 Lee PLAN_IMPLEMENTACION_COMPONENTES.md
🤔 Pregunta antes de implementar algo diferente
```

---

## 🚀 COMANDO DE INICIO

Cuando estés listo para comenzar una fase, usa:

```
Estoy listo para comenzar la FASE [N]: [NOMBRE_FASE]

He leído:
✅ README.md
✅ AUDITORIA_APLICABILIDAD_MEJORAS.md (sección relevante)
✅ MATRIZ_VISUAL_MEJORAS.md
✅ PLAN_IMPLEMENTACION_COMPONENTES.md (si aplica)

Vistas objetivo:
1. [vista-1]
2. [vista-2]
3. [vista-3]

Componentes que necesito:
- [ComponenteA]
- [ComponenteB]

¿Comenzamos con [primera-vista]?
```

---

**FIN DEL PROMPT**

Este prompt garantiza que no se omita ninguna mejora identificada en la auditoría 
y que se siga la arquitectura de componentes propuesta.
