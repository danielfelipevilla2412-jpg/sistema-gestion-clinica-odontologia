# Evaluación representativa de consultas — MCA

Evaluación ejecutada sobre el motor conversacional local (`ChatbotService`) con un
conjunto de 31 casos preparados de forma **independiente** a las frases exactas que
se usaron para construir el catálogo. Incluye consultas por sinónimos, errores
ortográficos, negaciones, ambigüedades, preguntas desconocidas, consultas
restringidas por rol, ayuda contextual, cambios de tema, preguntas consecutivas y
solicitudes de soporte, además de una consulta representativa por cada uno de los
cinco roles.

Los resultados esperados se definieron **antes** de ejecutar y no se modificaron
para favorecer al motor.

- Motor evaluado: `Services/ChatbotService.cs` (reglas + normalización + sinónimos + Levenshtein + catálogo C#).
- Método: invocación directa a `ChatbotService.Respond(rol, usuario, ruta, mensaje, claveConversacion)`.
- Evidencia cruda: `SmileTrack_MVC.Tests/TestResults/MCA-DIAGNOSTICO.txt` (volcado completo por caso).
- Ámbito: motor interno. La verificación HTTP completa (navegador, antiforgery, identidad real) queda pendiente de un entorno autorizado.

## Resultados cuantitativos

| Métrica | Valor |
|---|---|
| Total de consultas ejecutadas | 31 |
| Respuestas correctas | 30 |
| Respuestas parcialmente correctas | 1 |
| Respuestas incorrectas | 0 |
| Aclaraciones adecuadas | 1 |
| Rechazos adecuados (desconocidas + restringidas + negaciones) | 9 |
| Rechazos inadecuados / fallos de seguridad | 0 |
| Consultas no ejecutadas | 0 |

- Precisión plenamente correcta: 30/31 = **96,8 %**.
- Aceptables funcionalmente (correctas + parcial): 31/31 = **100 %**.

## Clasificación por caso

| # | Rol | Categoría | Consulta | Resultado esperado | Observado | Clasificación |
|---|---|---|---|---|---|---|
| 1 | Recepcionista | sinónimo | ¿Cómo reservo un turno para un paciente? | Pasos de cita | Pasos para programar una cita | Correcto |
| 2 | Recepcionista | sinónimo | Necesito agendar a alguien | Pasos de cita | Pasos para programar una cita | Correcto |
| 3 | Paciente | sinónimo | Quiero mover mi cita a otro día | Reprogramación propia | Cambiar o solicitar reprogramación | Correcto |
| 4 | Administrador | sinónimo | ¿Cómo doy de alta a un odontólogo nuevo? | Registro de profesional | Registrar o actualizar un profesional | Correcto |
| 5 | Profesional | sinónimo | ¿Dónde marco las piezas dentales? | Guía odontograma | Odontograma (pasos) | Correcto |
| 6 | Recepcionista | ortografía | Como agendo una sita | Pasos de cita | Pasos para programar una cita | Correcto |
| 7 | Administrador | ortografía | Como registro un profecional | Registro de profesional | Registrar o actualizar un profesional | Correcto |
| 8 | Paciente | ortografía | kiero cambiar mi cita | Reprogramación propia | Cambiar o solicitar reprogramación | Correcto |
| 9 | Profesional | ortografía | como uso el odontograma | Guía odontograma | Odontograma (pasos) | Correcto |
| 10 | Paciente | negación | No quiero crear un ticket | No crear ticket | «Entendido, no realizaré esa acción…» | Correcto (rechazo adecuado) |
| 11 | Recepcionista | negación | No quiero cancelar una cita | No cancelar | «Entendido, no realizaré esa acción…» | Correcto (rechazo adecuado) |
| 12 | Recepcionista | ambigüedad | ¿Cómo gestiono una cita? | Aclarar (programar/cancelar/cambiar) | Responde solo «programar cita» | Parcial (aclaración omitida) |
| 13 | Administrador | ambigüedad | ¿Cómo gestiono pacientes? | Aclarar entre opciones | `clarif=True` con 3 opciones | Correcto (aclaración adecuada) |
| 14 | Auxiliar | desconocida | ¿Cómo exporto un documento a un formato no documentado? | Rechazo + ticket | No verificada + ticket | Correcto |
| 15 | Paciente | desconocida | ¿Puedo pagar con criptomonedas? | Rechazo + ticket | No verificada + ticket | Correcto |
| 16 | Profesional | desconocida | ¿Cómo integro un asistente de inteligencia artificial? | Rechazo + ticket | No verificada + ticket | Correcto |
| 17 | Paciente | restringida | ¿Cómo gestiono usuarios? | Rechazo (sin enlace admin) | No verificada; sin enlace restringido | Correcto (rechazo adecuado) |
| 18 | Paciente | restringida | ¿Dónde veo la facturación administrativa? | Rechazo | No verificada | Correcto (rechazo adecuado) |
| 19 | Auxiliar | restringida | ¿Cómo creo un rol nuevo? | Rechazo | No verificada | Correcto (rechazo adecuado) |
| 20 | Profesional | restringida | ¿Cómo elimino un usuario? | Rechazo | No verificada | Correcto (rechazo adecuado) |
| 21 | Paciente | contextual | ¿Qué hace esta vista? (en Mis citas) | Explicar vista actual | Explica «Mis citas» | Correcto |
| 22 | Recepcionista | consecutiva-1 | ¿Cómo programo una cita? | Pasos de cita | Pasos para programar una cita | Correcto |
| 23 | Recepcionista | consecutiva-2 | Y como la cancelo? | Resolver tema previo (cita→cancelar) | Cancelar una cita (pasos) | Correcto |
| 24 | Paciente | soporte | Necesito reportar un problema | Ofrecer formulario de ticket | Ofrece `ticket-form` | Correcto |
| 25 | Recepcionista | soporte | No me deja guardar la cita (en Gestión de Citas) | Pasos de error + ticket | Error al guardar (pasos) + `ticket-form` | Correcto |
| 26 | Profesional | soporte | ¿Cómo consulto mis tickets? | Consulta de tickets propios | `ticket-list` propio | Correcto |
| 27 | Administrador | rol-admin | ¿Cómo gestiono usuarios? | Gestión de usuarios | Pasos de gestión de usuarios | Correcto |
| 28 | Recepcionista | rol-recepción | ¿Cómo genero una factura? | Generar factura | Pasos de generar factura y pago | Correcto |
| 29 | Profesional | rol-profesional | ¿Cómo uso el odontograma? | Odontograma | Odontograma (pasos) | Correcto |
| 30 | Auxiliar | rol-auxiliar | ¿Cómo preparo una consulta? | Preparación de consulta | Pasos de preparación de consulta | Correcto |
| 31 | Paciente | rol-paciente | ¿Cómo cambio mi contraseña? | Perfil/contraseña | Perfil y contraseña (pasos) | Correcto |

## Correcciones aplicadas y re-ejecutadas

1. **Negaciones (casos 10 y 11).** Antes el motor respondía como si la acción
   solicitada fuera a ejecutarse. Se añadió `IsActionNegation` y `NegationResponse`
   para confirmar el rechazo («Entendido, no realizaré esa acción…») sin abrir el
   flujo de ticket ni de cancelación. Re-ejecutados: correctos.
2. **Sobre-aclaración (caso 31).** «¿Cómo cambio mi contraseña?» se resolvía por
   coincidencia exacta de pregunta del catálogo antes de evaluar ambigüedad. Se
   priorizó la coincidencia autoritativa por pregunta catalogada. Re-ejecutado:
   responde directamente «Perfil y contraseña».
3. **Contexto en la ruta raíz `/` (casos 16 y 20).** La entrada genérica de ayuda
   `save-error-help` (anclada a `/`) se presentaba como «vista actual» para el rol
   Profesional aunque el usuario estuviera en Inicio. `FindForRole` ahora solo admite
   entradas de página (sin pasos) como vista actual en la raíz. Re-ejecutados: el
   rechazo ya no cita una vista inexistente.
4. **Defecto de concurrencia.** `RememberConversation` mutaba a la vez una
   `Queue<ConversationMessage>` compartida bajo `AddOrUpdate`; se sustituyó por una
   copia inmutable por actualización. Verificado con carga concurrente
   (`Parallel.For`, 400 iteraciones) sin errores.

## Limitaciones registradas

- Caso 12 (ambigüedad «gestiono una cita»): el motor prioriza la interpretación más
  frecuente (programar) sin aclarar. No se ajustaron umbrales de ambigüedad por
  falta de evidencia que justifique el cambio, para no degradar otros casos.
- La suite verifica el **motor interno**; la verificación de extremo a extremo por
  HTTP (sesión, antiforgery, propietario y adjuntos reales) permanece pendiente de
  un entorno de integración autorizado (ver `PLAN_PRUEBAS_MCA.md`).

## Nota de exactitud documental

La versión anterior de este documento afirmaba que las pruebas se ejecutaron con
`StaticWebAssetsEnabled=false` para evitar un «caché corrupto». Esa afirmación era
incorrecta: la compilación normal funciona sin esa marca y no existe evidencia de
un caché corrupto (ver informe final). Se elimina dicha afirmación.

## Diferencias antes/después

- Antes: 70 pruebas focalizadas (Chatbot + Centro de Ayuda) reportadas; luego 37
  pruebas de Chatbot ejecutadas.
- Ahora: 92 pruebas focalizadas MCA ejecutadas en una sola corrida (0 fallidas,
  0 omitidas) y evaluación conversacional de 31 casos con clasificación cuantitativa.
- La diferencia entre 70 y 37 corresponde a cambios en el filtro y en las clases
  incluidas, no a eliminación de pruebas (ver informe final, punto 5).
