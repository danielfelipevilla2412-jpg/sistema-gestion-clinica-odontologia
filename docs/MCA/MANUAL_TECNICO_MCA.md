# Manual técnico — MCA

## Componentes

- `Views/shared/_Chatbot.cshtml`: contenedor visual y token antifalsificación.
- `wwwroot/js/shared/chatbot.js`: interacción, formularios y llamadas API.
- `wwwroot/css/shared/chatbot.css`: presentación responsive y z-index.
- `Controllers/Api/ChatbotApiController.cs`: autenticación, límites, creación y consulta.
- `Services/ChatbotService.cs`: normalización, relevancia, contexto y roles.
- `Services/ChatbotKnowledgeCatalog.cs`: conocimiento centralizado.
- `Services/CentroDeAyuda/CentroDeAyudaService.cs`: persistencia y adjuntos.

## Límites existentes

- 500 caracteres por mensaje.
- 15 mensajes por minuto por usuario por configuración, con valores por defecto.
- 120 solicitudes globales por minuto por defecto mediante `Chatbot:GlobalRequestsPerMinute`.
- 5 tickets por 15 minutos por usuario por configuración.
- Seis mensajes recientes por conversación.
- Retención de contexto configurada en 30 minutos.
- Máximo 5000 estados conversacionales por defecto.
- Adjuntos 5 MB.

## Adjuntos

Los nuevos adjuntos se almacenan en `private_uploads/centro-ayuda` y tienen nombres GUID. El contenido se valida por extensión, MIME declarado y firma real (cabeceras de bytes). La descarga pasa por la API y verifica usuario, referencia y ruta bajo el directorio privado.

Verificación de seguridad (inspección de código, `CentroDeAyudaService.GuardarAdjuntoAsync`, `ChatbotApiController.GetMyTicketAttachment`):

- Almacenamiento fuera de `wwwroot` (bajo `ContentRootPath`), por lo que no es servible como recurso estático público.
- Límite de 5 MB por archivo y lista cerrada de MIME permitidos.
- Filtro por extensión permitida (`.jpg .jpeg .png .webp .gif .pdf .doc .docx .txt`).
- Validación de firma real del contenido contra la extensión declarada.
- Nombre de archivo aleatorio (GUID); no se usa el nombre del cliente para la ruta.
- Descarga con autenticación obligatoria y pertenencia del ticket (`ObtenerTicketUsuarioAsync` filtra por `IdUsuario`); ajeno o inexistente devuelve 404.
- Guardas contra recorrido de rutas (prefijo raíz `private_uploads/centro-ayuda` + `GetFullPath`).
- Limpieza de adjuntos huérfanos si falla el guardado o se reemplaza el archivo.
- No se encontró carpeta pública `wwwroot/uploads` ni artefactos MCA antiguos expuestos en `wwwroot` en este repositorio; por tanto no hay evidencia de exposición pública de adjuntos del MCA.
- Pendiente: verificación HTTP real de descarga autorizada/rechazada y persistencia en publicación (Docker), fuera del alcance sin entorno autorizado.

## Contexto

La clave combina `IdUsuario` y `X-Chatbot-Session`. El cierre visual limpia el almacenamiento de sesión y el reinicio elimina el estado del servidor. La actualización del estado conversacional usa copia inmutable por escritura (`AddOrUpdate` con nueva `Queue`), lo que evita mutaciones concurrentes inseguras. El reloj se inyecta mediante `TimeProvider` (con valor por defecto `TimeProvider.System`), lo que permite verificar en pruebas la retención de 30 minutos y la expiración del contexto sin depender del reloj del sistema.

## Rendimiento

Medición del motor interno (no HTTP), reproducible con `McaDiagnosticoProbe`:

- Calentamiento 50 invocaciones; 1000 iteraciones secuenciales; 400 concurrentes.
- P95 interno observado en 5 corridas: 9,6 – 56,4 ms (siempre < 2000 ms de RNF-MCA-03).
- Alta varianza en un equipo compartido (máximos aislados hasta ~693 ms por CPU/GC); no se optimizó código al no existir problema medido.
- Evidencia: `TestResults/MCA-RENDIMIENTO.txt`.

## Verificación pendiente

- Docker aislado real (persistencia de adjuntos privados).
- Pruebas HTTP/browser con los cinco roles y antiforgery.
- Medición de rendimiento HTTP de extremo a extremo.
