# Plan de pruebas — MCA

## Resultados vigentes

Ejecución más reciente con compilación normal (sin `StaticWebAssetsEnabled=false`):

| Conjunto | Descubiertas | Aprobadas | Fallidas | Omitidas | Evidencia |
|---|---|---|---|---|---|
| Focalizadas MCA | 92 | 92 | 0 | 0 | `TestResults/MCA-FOCALIZADAS.trx` |
| Suite completa | 633 | 628 | 1 | 4 | `TestResults/MCA-SUITE-COMPLETA.trx` |

- La única prueba fallida de la suite (`HistoriaClinicaStructuredDataTests.ObtenerOdontogramaHasta_DevuelveElMapeoFDIVigenteEnLaFecha`) es **ajena al MCA** (módulo Historia Clínica, sensible a la fecha) y no tiene relación con el chatbot ni el centro de ayuda.
- Las 4 pruebas omitidas dependen de SQL Server o SMTP reales.

El resultado anterior de 611 descubiertas / 607 aprobadas / 4 omitidas correspondía a un conjunto distinto de archivos de prueba; ver la sección «Diferencia entre 70 y 37 pruebas» del informe final.

## Cobertura ejecutable del MCA

- Cinco roles con consultas representativas.
- Restricción administrativa para pacientes.
- Generación de facturas para Recepcionista.
- Odontograma para Profesional.
- Preparación de consulta para Auxiliar.
- Perfil y contraseña para todos los actores.
- Error al guardar y creación de ticket.
- Sinónimos y ortografía.
- Negaciones e impugnaciones.
- Ambigüedad y sobre-aclaración.
- Preguntas consecutivas y reinicio.
- Aislamiento entre claves de sesión.
- Retención y expiración del contexto con reloj controlable (`TimeProvider`): retención vigente a los 29 min y expiración a los 31 min.
- Límite de seis mensajes recientes por conversación.
- Mensaje vacío y de más de 500 caracteres.
- Confirmación obligatoria e idempotencia.
- Propietario de tickets y descarga autorizada/ajena.
- Límites de tasa (15/min, 5/15min, 60/min, 120/min global).
- Integridad del catálogo (métricas y `EntriesSnapshot`).
- P95 interno menor a 2000 ms.

## Procedimiento de pruebas finales (Sección 12)

Requisitos previos:

- Base de datos desechable autorizada (no la principal).
- Navegador con sesión válida por rol (Administrador, Recepcionista, Profesional, Auxiliar, Paciente).
- Variables de entorno de adjuntos privados apuntando a almacenamiento temporal.

Datos ficticios:

- Un usuario por rol con credenciales de prueba.
- Un paciente ficticio y una cita ficticia.
- Un archivo de cada tipo permitido (`.png`, `.pdf`, `.txt`).

Pasos y resultados esperados:

1. **Verificación en navegador de los cinco roles.** Esperado: cada rol recibe ayuda contextual coherente con su ruta y permisos; sin enlaces a módulos ajenos.
2. **Creación real de ticket (base desechable).** Esperado: se crea el ticket, se genera referencia y se asocia al usuario autenticado.
3. **Cancelación de ticket.** Esperado: el ticket pasa a estado cancelado/cerrado y deja de listarse como abierto.
4. **Prevención de duplicados.** Esperado: envíos repetidos con la misma clave de idempotencia no crean tickets adicionales.
5. **Consulta de tickets propios.** Esperado: solo se listan los tickets del usuario autenticado.
6. **Descarga autorizada de adjunto.** Esperado: 200/206 con `Content-Disposition` y `Content-Type` correctos.
7. **Rechazo de descargas ajenas.** Esperado: 404 para tickets/adjuntos de otro usuario (aislamiento por `IdUsuario`).
8. **Medición de rendimiento HTTP.** Esperado: P95 de extremo a extremo registrado con entorno y configuración.
9. **Verificación responsive.** Esperado: widget usable en escritorio, tablet y móvil sin solapamientos.
10. **Verificación de accesibilidad.** Esperado: foco, etiquetas ARIA y contraste mínimos.
11. **Persistencia de adjuntos privados.** Esperado: el archivo sobrevive al reinicio del entorno de publicación.

Evidencias necesarias por paso: captura de pantalla, respuesta HTTP (código y encabezados) y registro de la base desechable. No se ejecutaron escrituras sobre la base principal.

## Pendiente de autorización

- Escrituras reales en base de datos desechable.
- Persistencia de adjuntos en Docker aislado.
- Medición de rendimiento HTTP de extremo a extremo.
- Verificación responsive y de accesibilidad en navegador real.
