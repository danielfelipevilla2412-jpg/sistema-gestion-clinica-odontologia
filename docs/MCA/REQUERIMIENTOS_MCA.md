# Requerimientos formales — Módulo de Chatbot y Centro de Ayuda (MCA)

Conteo vigente (sin cambios artificiales):

- 12 requerimientos funcionales (RF-MCA-01 … RF-MCA-12).
- 10 requerimientos no funcionales (RNF-MCA-01 … RNF-MCA-10).
- 10 reglas de negocio (RN-MCA-01 … RN-MCA-10, ver `REGLAS_Y_TRAZABILIDAD_MCA.md`).

Estados: **Implementado y verificado** · **Implementado, pendiente de integración** ·
**Implementado parcialmente** · **Pendiente de implementación** · **Bloqueado**.

Pruebas MCA asociadas: `ChatbotServiceTests`, `ChatbotApiControllerTests`,
`CentroDeAyudaServiceTests`, `CentroDeAyudaControllerTests`,
`CentroDeAyudaApiControllerTests`, `ChatbotRateLimitConfigurationTests`,
`McaDiagnosticoProbe`.

---

## Requerimientos funcionales

### RF-MCA-01 Acceso autenticado
- Descripción: el chatbot solo está disponible en el layout compartido de vistas autenticadas.
- Criterios: sin autenticación no hay acceso al panel del chatbot; actor = usuario autenticado.
- Componentes: `_Layout.cshtml`, `_Chatbot.cshtml`, `ChatbotApiController.Message`.
- Pruebas: `ChatbotApiControllerTests`.
- Evidencia: `[Authorize]` en `ChatbotApiController`; pruebas API.
- Estado: **Implementado y verificado**.

### RF-MCA-02 Identidad y rol desde servidor
- Descripción: rol y usuario se leen de las claims; el cliente no decide permisos.
- Criterios: rol/ruta manipulados por el cliente no amplían el acceso.
- Componentes: `ChatbotApiController`, `ChatbotService.ResolveProfile`.
- Pruebas: `ChatbotApiControllerTests` (rol de servidor); evaluación casos 17–20.
- Evidencia: `User.FindFirstValue(ClaimTypes.NameIdentifier)`, `ResolveProfile`.
- Estado: **Implementado y verificado**.

### RF-MCA-03 Contexto de vista
- Descripción: el chat reconoce rutas conocidas y evita rutas externas/arbitrarias.
- Criterios: solo rutas normalizadas del catálogo aportan contexto; la raíz no atribuye una vista inexistente.
- Componentes: `ChatbotKnowledgeCatalog.NormalizePath/FindForRole`.
- Pruebas: `ChatbotServiceTests`; evaluación casos 21, 16, 20.
- Evidencia: guardas de `NormalizePath`; filtrado en `FindForRole`.
- Estado: **Implementado y verificado**.

### RF-MCA-04 Consultas locales
- Descripción: respuestas mediante reglas, normalización, sinónimos, Levenshtein y catálogo C#.
- Criterios: no se usan proveedores de IA externos.
- Componentes: `ChatbotService`.
- Pruebas: `ChatbotServiceTests`; evaluación casos 1–9.
- Evidencia: ausencia de llamadas de red en el motor.
- Estado: **Implementado y verificado**.

### RF-MCA-05 Conocimiento por rol
- Descripción: catálogo con entradas reales autorizadas por rol.
- Criterios: 75 entradas, 14 módulos, 53 rutas; sin duplicados ficticios.
- Componentes: `ChatbotKnowledgeCatalog`.
- Pruebas: `ChatbotServiceTests`; métricas en `McaDiagnosticoProbe`.
- Evidencia: `MATRIZ_PERMISOS_Y_CATALOGO.md`.
- Estado: **Implementado y verificado**.

### RF-MCA-06 Flujo de ticket
- Descripción: la conversación abre formulario, revisa datos, exige confirmación y permite cancelar antes del envío.
- Criterios: sin confirmación no se crea; se puede cancelar antes de enviar.
- Componentes: `chatbot.js`, `ChatbotApiController.CreateTicket`, `CentroDeAyudaService.CrearTicketAsync`.
- Pruebas: `ChatbotApiControllerTests`, `CentroDeAyudaServiceTests`.
- Evidencia: validaciones de `Confirmed` y `IdempotencyKey`.
- Estado: **Implementado, pendiente de integración** (verificación HTTP real).

### RF-MCA-07 Adjuntos
- Descripción: admite adjuntos de hasta 5 MB y valida longitud, extensión, MIME y firma real.
- Criterios: rechaza > 5 MB, MIME no permitido, extensión no permitida y firma inconsistente; guarda fuera de `wwwroot`.
- Componentes: `CentroDeAyudaService.GuardarAdjuntoAsync` y `ValidateAttachmentContentAsync`.
- Pruebas: `CentroDeAyudaServiceTests`.
- Evidencia: `MANUAL_TECNICO_MCA.md` (sección Adjuntos).
- Estado: **Implementado, pendiente de integración** (descarga/persistencia HTTP y Docker).

### RF-MCA-08 Consulta propia
- Descripción: solo se consultan tickets cuyo `IdUsuario` coincide con la identidad autenticada.
- Criterios: un usuario no ve tickets ajenos; endpoints `tickets/mine` y `tickets/mine/{reference}`.
- Componentes: `CentroDeAyudaService.ObtenerTicketsUsuarioAsync/ObtenerTicketUsuarioAsync`.
- Pruebas: `CentroDeAyudaServiceTests`, `ChatbotApiControllerTests`.
- Evidencia: filtrado por `IdUsuario`.
- Estado: **Implementado y verificado**.

### RF-MCA-09 Descarga autorizada
- Descripción: la descarga exige autenticación y pertenencia del ticket.
- Criterios: ajeno o inexistente → 404; archivo resuelto bajo `private_uploads/centro-ayuda`.
- Componentes: `ChatbotApiController.GetMyTicketAttachment`.
- Pruebas: inspección de código; prueba HTTP real pendiente.
- Evidencia: guardas de ruta y `ObtenerTicketUsuarioAsync`.
- Estado: **Implementado, pendiente de integración** (verificación HTTP real).

### RF-MCA-10 Prevención de duplicados
- Descripción: la creación exige confirmación y clave de idempotencia; la API conserva temporalmente las claves por usuario.
- Criterios: clave repetida → 409; claves purgadas tras retención.
- Componentes: `ChatbotApiController.TicketRequests`, `PurgeTicketRequests`.
- Pruebas: `ChatbotApiControllerTests`.
- Evidencia: `TryAdd` sobre `{userId}:{idempotencyKey}`.
- Estado: **Implementado, pendiente de integración** (HTTP real).

### RF-MCA-11 Reinicio y aislamiento
- Descripción: la conversación usa usuario + clave de sesión, admite reinicio y expira por configuración.
- Criterios: aislamiento entre sesiones, expiración a 30 min, límite de 6 mensajes, tope de 5000 estados.
- Componentes: `ChatbotService` (`ConversationStates`, `ResetConversation`, `PurgeConversationStates`).
- Pruebas: `ChatbotServiceTests`.
- Evidencia: `BuildConversationKey = {userId}:{sessionId}`; copia inmutable por escritura.
- Estado: **Implementado y verificado**.

### RF-MCA-12 Manejo seguro de errores
- Descripción: las respuestas no exponen rutas físicas, credenciales, entidades ni información ajena.
- Criterios: mensajes genéricos ante fallos; sin trazas internas.
- Componentes: `ChatbotService`, `ChatbotApiController`.
- Pruebas: `ChatbotApiControllerTests`.
- Evidencia: mensajes sin datos sensibles; evaluación casos 12–20.
- Estado: **Implementado y verificado**.

---

## Requerimientos no funcionales

- **RNF-MCA-01 Seguridad:** autenticación, antifalsificación y autorización obligatorias. Pruebas: `ChatbotApiControllerTests`. Estado: Implementado y verificado (HTTP real pendiente).
- **RNF-MCA-02 Privacidad:** no solicitar ni registrar datos clínicos innecesarios. Estado: Implementado (diseño de mensajes y catálogo).
- **RNF-MCA-03 Rendimiento:** P95 interno < 2000 ms. Evidencia: `TestResults/MCA-RENDIMIENTO.txt` (P95 9,6–56,4 ms en 5 corridas). Estado: **Implementado y verificado (motor interno)**; medición HTTP pendiente.
- **RNF-MCA-04 Usabilidad:** respuestas breves, organizadas y accesibles. Estado: Implementado (formato de pasos y sugerencias).
- **RNF-MCA-05 Responsive:** escritorio, tablet y móvil mediante `chatbot.css`. Estado: Implementado; verificación visual en navegador pendiente.
- **RNF-MCA-06 Independencia:** sin proveedores externos ni APIs de IA. Estado: Implementado y verificado.
- **RNF-MCA-07 Escalabilidad:** catálogo centralizado y estados con límites máximos. Estado: Implementado (tope 5000 y purga).
- **RNF-MCA-08 Fiabilidad:** idempotencia, manejo de errores y limpieza de adjuntos huérfanos. Estado: Implementado.
- **RNF-MCA-09 Persistencia:** tickets permanecen en el modelo y servicio existentes. Estado: Implementado; persistencia Docker pendiente.
- **RNF-MCA-10 Trazabilidad:** respuestas basadas en entradas verificables del catálogo. Estado: Implementado y verificado.

---

## Estado de cumplimiento

- Implementado y verificado: RF-MCA-01, 02, 03, 04, 05, 08, 11, 12; RNF-MCA-03 (motor interno), RNF-MCA-06, RNF-MCA-10.
- Implementado, pendiente de integración (HTTP/Docker): RF-MCA-06, 07, 09, 10; RNF-MCA-01, 09.
- Pendiente de implementación: ninguno.
- Bloqueado: ninguno.

No se declaran cumplidos los requerimientos que dependen de verificaciones HTTP aún no
realizadas.
