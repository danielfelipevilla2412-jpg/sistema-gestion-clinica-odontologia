# Lista de chequeo ISO/IEC 25010 actualizada

**Proyecto:** SmileTrack

**Versión de la lista:** 1.2

**Fecha:** 18 de noviembre de 2026

**Alcance:** exclusivamente los módulos 3 y 4:

- Módulo 3: Gestión de Profesionales
- Módulo 4: Gestión de Citas

**Escala:** 1 = Inadecuado, 2 = Mejorable, 3 = Aceptable, 4 = Bueno, 5 = Excelente.

---

## Módulo 3: Gestión de Profesionales

| N.º | Aspecto de calidad | Calificación | Nivel | Evidencia verificable actual |
|---|---|---:|---|---|
| 3.1 | Adecuación funcional | 4 | Bueno | Valida registro médico, teléfono, horarios y solapamientos. La reasignación comprueba servicio, especialidad, horario, ausencia, bloqueo, conflicto de profesional, paciente y consultorio. |
| 3.2 | Eficiencia de desempeño | 4 | Bueno | Los listados son paginados y tienen tope de 100 registros. Se añadieron índices SQL y la reasignación carga en lotes asignaciones, horarios, ausencias, bloqueos y conflictos, eliminando consultas repetidas por cita y candidato. |
| 3.3 | Compatibilidad | 4 | Bueno | Integración REST, generación de reportes PDF y exportación del módulo existente. |
| 3.4 | Usabilidad | 4 | Bueno | Ventanas de confirmación, mensajes tipo toast, estados de carga y traducción de textos en las vistas. |
| 3.5 | Fiabilidad | 4 | Bueno | Transacciones con reintento y manejo de duplicados. La reasignación mantiene una reserva temporal dentro de la ejecución y evita asignar dos citas al mismo hueco. |
| 3.6 | Seguridad | 4 | Bueno | Roles por endpoint, antifalsificación, BCrypt, política de contraseña y auditoría. Se añadió auditoría de reasignaciones por ausencia y envío seguro de contraseña temporal por correo. |
| 3.7 | Mantenibilidad | 4 | Bueno | `ProfesionalService` se organizó en archivos por CRUD, horarios/ausencias, servicios, comisiones y reasignación. La clase aún implementa una interfaz amplia; para Excelente conviene extraer servicios independientes. |
| 3.8 | Portabilidad | 4 | Bueno | Configuración por capas e infraestructura Docker. Falta completar scripts reproducibles y documentar la configuración de despliegue. |

**Promedio del módulo:** `(4 + 4 + 4 + 4 + 4 + 4 + 4 + 4) / 8 = 4.00`

**Nivel del módulo:** Bueno

**Cambio respecto a la lista inicial:** mantenibilidad sube a 4 por la organización por responsabilidad y el recorte de consultas repetidas. La extracción a servicios independientes y las pruebas SQL de desempeño siguen siendo candidatas para aspirar a Excelente.

---

## Módulo 4: Gestión de Citas

| N.º | Aspecto de calidad | Calificación | Nivel | Evidencia verificable actual |
|---|---|---:|---|---|
| 6.1 | Adecuación funcional | 4 | Bueno | Valida horarios clínicos y profesionales, ausencias, bloqueos, especialidad y duración del servicio. Falta relacionar formalmente cada servicio con el tipo de consultorio requerido y cerrar la confirmación con un clic en la lista de espera. |
| 6.2 | Eficiencia de desempeño | 4 | Bueno | Recordatorios automáticos cada cinco minutos, hasta 100 por ejecución, con control de duplicados e índices. La reasignación masiva precarga disponibilidad y conflictos por lote y ya no consulta repetidamente por cada cita y candidato. No hay mediciones P95 en SQL Server real para aspirar a Excelente. |
| 6.3 | Compatibilidad | 4 | Bueno | API REST con JWT, correo SMTP, integración móvil y exportación del dashboard a PDF. |
| 6.4 | Usabilidad | 4 | Bueno | Confirmaciones, avisos, estados de carga, mensajes de negocio y reprogramación desde la agenda. |
| 6.5 | Fiabilidad | 4 | Bueno | Conflictos revisados con transacciones y reintentos. Hay estados finales validados e historial de cambios. La reserva concurrente de reemplazos todavía necesita persistencia y prueba sobre SQL Server real. |
| 6.6 | Seguridad | 4 | Bueno | Roles por endpoint, dueño de profesional/paciente, antifalsificación y auditoría de datos anteriores y nuevos. |
| 6.7 | Mantenibilidad | 4 | Bueno | Recordatorios quedaron en servicios y trabajador independientes. `GestionCitasController` se reorganizó en archivos por agenda, citas administrativas, auxiliares, paciente/profesional, recepción y datos compartidos. Para Excelente se deben extraer controladores y servicios independientes por caso de uso. |
| 6.8 | Portabilidad | 4 | Bueno | Infraestructura Docker, configuración por capas y base para despliegue independiente. Continúa dependiendo de SQL Server. |

**Promedio del módulo:** `(4 + 4 + 4 + 4 + 4 + 4 + 4 + 4) / 8 = 4.00`

**Nivel del módulo:** Bueno

**Cambio respecto a la lista inicial:** 6.2 y 6.7 suben a 4 por la optimización por lotes, el procesamiento automático y la reorganización por responsabilidades. La medición real de latencia y la extracción a componentes independientes quedan para aspirar a Excelente.

---

## Mejoras implementadas

- Servicio `IRecordatorioCitaService` independiente de `CitaService`.
- Trabajador automático de recordatorios cada cinco minutos.
- Procesamiento máximo de 100 citas por ejecución.
- Registro de recordatorios enviados, fallidos, intentos y errores.
- Prevención de recordatorios duplicados para la misma cita y fecha.
- Índices para horarios, ausencias, bloqueos y recordatorios.
- Reasignación masiva optimizada mediante precarga de asignaciones, horarios, ausencias, bloqueos y citas en conflicto.
- Archivos parciales de `ProfesionalService` organizados por CRUD, horarios/ausencias, servicios, comisiones y reasignación.
- Archivos parciales de `GestionCitasController` organizados por grupos funcionales.
- Reasignación por ausencia que verifica horarios, ausencias, bloqueos y conflictos.
- Protección temporal contra asignar dos citas al mismo reemplazo en el mismo intervalo.
- Auditoría de la operación de ausencia y reasignación.
- Envío de contraseña temporal por correo al crear profesionales.
- Pruebas unitarias y de carga en memoria para profesionales, citas y recordatorios.

---

## Estado de pruebas

| Verificación | Resultado |
|---|---|
| Pruebas focalizadas de profesionales y citas | 78 aprobadas, 0 fallidas |
| Suite completa posterior a las mejoras iniciales | 495 aprobadas, 0 fallidas |
| Suite completa posterior a índices, auditoría y contraseña temporal | 495 aprobadas, 0 fallidas |
| Pruebas focalizadas después de reorganización y consultas por lote | 75 aprobadas, 0 fallidas |
| Compilación de los módulos | Correcta |

No se encontró una instancia SQL Server local disponible y Docker Engine no estaba iniciado; por ello no se registran mediciones de carga en SQL Server real.

## Estado de los aspectos anteriormente Aceptables

Los tres aspectos pasan a **Bueno (4)**:

- **3.7 Mantenibilidad:** código organizado en archivos por responsabilidad.
- **6.2 Eficiencia:** reasignación por lotes, sin consultas repetidas dentro de las iteraciones.
- **6.7 Mantenibilidad:** recordatorios en servicio/trabajador independientes y controlador organizado por grupos funcionales.

La suite focalizada posterior a estos cambios aprobó 75 pruebas. Los cambios no justifican calificación Excelente por sí solos.

## Evidencia pendiente para aspirar a Excelente

Solo deben recalificarse aspectos cuando exista evidencia adicional, reproducible y medible.

### Adecuación funcional

- [ ] Relacionar cada servicio con el tipo de consultorio requerido.
- [ ] Validar el tipo de consultorio al crear y reagendar citas.
- [ ] Permitir confirmar una solicitud de lista de espera con un clic.
- [ ] Programar recordatorios automáticos configurables.

### Fiabilidad

- [ ] Mantener una reserva persistente de reemplazos en la base de datos.
- [ ] Probar concurrencia real con SQL Server.
- [ ] Verificar que no existan citas dobles en el mismo profesional, paciente y consultorio.

### Seguridad

- [ ] Limitar el cálculo y consulta de comisiones al rol correspondiente.
- [ ] Auditar horarios, ausencias, reasignaciones y comisiones.
- [ ] Validar que no se expongan mensajes internos ni datos sensibles en errores.
- [ ] Añadir pruebas negativas por rol y propiedad del recurso.

### Rendimiento

- [ ] Adjuntar mediciones P50/P95/P99.
- [ ] Ejecutar carga con datos reales y concurrencia.
- [ ] Medir memoria, CPU y consultas por operación.
- [ ] Aprobar umbrales documentados sin regresiones.
- [ ] Ejecutar las mediciones sobre una instancia SQL Server representativa; Docker Engine no estaba disponible durante esta revisión.

### Mantenibilidad

- [ ] Extraer clases de servicio y controladores independientes, no solo archivos parciales.
- [ ] Unificar la lógica de especialidades al crear y actualizar profesionales.
- [ ] Mantener cobertura focalizada por casos de uso.
- [ ] Eliminar constructores manuales y dependencias ocultas.
- [ ] Aprobar análisis estático sin errores críticos.

### Compatibilidad

- [ ] Exportar listados de profesionales y citas.
- [ ] Exportar reporte de comisiones.
- [ ] Enviar comisiones al destinatario correspondiente.
- [ ] Verificar integración REST y móvil en entorno representativo.

### Portabilidad

- [ ] Completar scripts reproducibles de SQL Server.
- [ ] Documentar variables de entorno obligatorias.
- [ ] Ejecutar despliegue limpio en Docker.
- [ ] Verificar migración o instalación desde cero.
