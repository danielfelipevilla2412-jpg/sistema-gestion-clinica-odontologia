# 📚 Documentación del Proyecto SmileTrack

## Estructura de Documentación

Esta carpeta contiene la documentación técnica y funcional del Sistema de Gestión Clínica de Odontología SmileTrack.

---

## 📂 Contenido

### 📄 Archivos Principales

#### `analisis_completo_proyecto.md`
Análisis arquitectónico completo del proyecto, incluyendo:
- Estructura de carpetas
- Tecnologías utilizadas
- Arquitectura del sistema
- Patrones de diseño implementados

#### `README_VISTAS_SQL.md`
Documentación técnica sobre la implementación de vistas SQL y su integración con el sistema.

---

### 📁 Carpetas Organizadas

#### `Gestion_De_Citas_Y_Profesionales/`
Documentación específica de los módulos de Gestión de Citas y Gestión de Profesionales:

- **`AUDITORIA_BOTONES_SSR_CSR.md`**: Auditoría completa de inconsistencias en botones de tabla entre renderizado servidor (SSR) y cliente (CSR). Incluye correcciones aplicadas en 5 vistas diferentes.

- **`FLUJOS_USUARIO_CITAS_Y_PROFESIONALES.md`**: Documentación de flujos de usuario para los módulos de citas y profesionales.

- **`PLAN_UNICO_CITAS_Y_PROFESIONALES.md`**: Plan unificado de implementación para estos módulos.

#### `2026-09-16-Implementacion-Vistas-SQL/`
Documentación relacionada con la implementación de vistas SQL en fecha específica.

#### `qa-requisitos-final/`
Documentación de requisitos finales y control de calidad.

#### `docs/`
Subcarpeta con documentación adicional organizada por tema.

---

## 🗂️ Organización por Módulos

### Módulos de Gestión de Citas
- Dashboard de Recepción (st-rec-01)
- Gestión de Citas (st-rec-03)
- Mis Citas del Paciente (st-pac-01)
- Gestión Integral Administrativa (st-adm-09)

### Módulos de Gestión de Profesionales
- Gestión de Profesionales (st-adm-07)
- Perfil del Profesional (st-odo-09)

---

## 📝 Notas

### Archivos Eliminados (16-sep-2026)
Se eliminaron los siguientes archivos de diagnóstico temporal que ya no son necesarios:
- `DIAGNOSTICO_TAREA_*` (1, 2, 3)
- `RESUMEN_TAREAS_*` y `RESUMEN_IMPLEMENTACION_*`
- `PLAN_IMPLEMENTACION_TAREA_*`
- `AUDITORIA_TAREA_8_LOCALSTORAGE.md`
- `DIAGNOSTICO_BOTON_MI_PERFIL_INCORRECTO.md`
- `DIAGNOSTICO_CAMPO_MEDICAMENTOS.md`
- `CORRECCION_DIAGNOSTICO_HISTORIAL_PARCIAL.md`
- `ELIMINACION_BOTONES_MI_PERFIL_REDUNDANTES.md`
- `FIX_DISENO_TABLA_PROFESIONALES_FILTROS.md` (integrado en AUDITORIA_BOTONES_SSR_CSR.md)
- `PROMPT_CONTINUACION_TAREAS_PENDIENTES.md`
- `final_commit_analysis.md`
- `informe_cambios_sin_commit.md`
- `RESUMEN_ESTADO_ACTUAL.md`

**Motivo:** Eran archivos de diagnóstico temporal de tareas específicas ya completadas. Se mantiene solo documentación de valor permanente.

---

## 🔍 Cómo Usar Esta Documentación

1. **Para entender el proyecto globalmente**: Lee `analisis_completo_proyecto.md`
2. **Para trabajar con citas/profesionales**: Consulta la carpeta `Gestion_De_Citas_Y_Profesionales/`
3. **Para auditorías específicas**: Busca los archivos `AUDITORIA_*.md`
4. **Para implementaciones SQL**: Revisa `README_VISTAS_SQL.md`

---

## 📅 Última Actualización

**Fecha:** 16 de septiembre de 2026  
**Acción:** Limpieza de documentación temporal y reorganización por módulos
