# Reglas de negocio y trazabilidad — MCA

Estados usados: **Implementado y verificado** · **Implementado parcialmente** ·
**Implementado, pendiente de integración** · **Pendiente de implementación** · **Bloqueado**.

## Reglas de negocio

| ID | Regla | Implementación | Evidencia | Estado |
|---|---|---|---|---|
| RN-MCA-01 | Las consultas solo ofrecen entradas autorizadas para el rol | `ChatbotKnowledgeCatalog.ForRole` y `ChatbotService.Respond` | Pruebas de rol y enlaces restringidos; evaluación casos 17–20 | Implementado y verificado |
| RN-MCA-02 | La ruta actual nunca otorga permisos adicionales | `NormalizePath` y filtrado por rol | Prueba `Message_UsesServerRole...` | Implementado y verificado |
| RN-MCA-03 | Un ticket requiere confirmación explícita | `ChatbotApiController.CreateTicket` | Prueba sin confirmación | Implementado y verificado |
| RN-MCA-04 | Un usuario solo consulta sus tickets | `ObtenerTicketsUsuarioAsync` / `ObtenerTicketUsuarioAsync` | Pruebas de propietario | Implementado y verificado |
| RN-MCA-05 | La clave de idempotencia impide reenvíos | `TicketRequests` en `ChatbotApiController` | Prueba de clave repetida | Implementado y verificado |
| RN-MCA-06 | Los adjuntos nuevos son privados | `GuardarAdjuntoAsync` usa `private_uploads/centro-ayuda` | Inspección técnica | Implementado y verificado |
| RN-MCA-07 | La descarga exige propietario | `GetMyTicketAttachment` filtra por usuario y referencia | Inspección; prueba HTTP real pendiente | Implementado, pendiente de integración |
| RN-MCA-08 | El contexto expira y tiene límite de estados | `ChatbotOptions` y `PurgeConversationStates` | Pruebas de reinicio y expiración; copia inmutable por escritura | Implementado y verificado |
| RN-MCA-09 | Los mensajes están limitados a 500 caracteres | API y DTO | Prueba de 501 caracteres | Implementado y verificado |
| RN-MCA-10 | Los adjuntos tienen máximo 5 MB | `GuardarAdjuntoAsync` | Inspección técnica y pruebas de servicio | Implementado y verificado |

## Matriz de trazabilidad funcional

| RF | RN | Componente | Archivo | Prueba/Evidencia | Estado |
|---|---|---|---|---|---|
| RF-MCA-01/02 | RN-MCA-01/02 | API y motor | `Controllers/Api/ChatbotApiController.cs`, `Services/ChatbotService.cs` | Pruebas Chatbot API/Service (92 focalizadas) | Implementado y verificado |
| RF-MCA-03/05 | RN-MCA-01 | Catálogo | `Services/ChatbotKnowledgeCatalog.cs` | Pruebas por rol; métricas de catálogo; evaluación casos 1–9, 27–31 | Implementado y verificado |
| RF-MCA-06/10 | RN-MCA-03/05 | Creación de tickets | `Controllers/Api/ChatbotApiController.cs`, `chatbot.js` | Pruebas API e idempotencia | Implementado, pendiente de integración |
| RF-MCA-07/09 | RN-MCA-06/07/10 | Adjuntos | `CentroDeAyudaService.cs`, `GetMyTicketAttachment` | Inspección técnica y pruebas de servicio | Implementado, pendiente de integración |
| RF-MCA-08 | RN-MCA-04 | Consulta propia | `CentroDeAyudaService.cs` | Prueba de ocultamiento | Implementado y verificado |
| RF-MCA-11 | RN-MCA-08 | Contexto | `ChatbotService.cs` | Pruebas de reinicio, aislamiento y expiración | Implementado y verificado |
| RF-MCA-12 | — | Errores | `ChatbotService.cs` | Respuestas sin rutas físicas ni datos ajenos | Implementado y verificado |
| RNF-MCA-03 | — | Rendimiento | `ChatbotService.cs` | `TestResults/MCA-RENDIMIENTO.txt` (P95 interno 9,6–56,4 ms) | Implementado y verificado (motor interno) |

## Estado global

- Implementado y verificado: RF-MCA-01, 02, 03, 04, 05, 08, 11, 12 y RNF-MCA-03 (motor interno).
- Implementado, pendiente de integración (HTTP/Docker): RF-MCA-06, 07, 09, 10.
- Pendiente de implementación: ninguno.
- Bloqueado: ninguno.

La verificación HTTP de creación/descarga de tickets y adjuntos, y la persistencia en
Docker, requieren un entorno de integración autorizado y permanecen pendientes.
