/*
  Preflight de solo lectura para CIT-02. Ejecutar en una copia desechable o en modo
  lectura sobre el origen antes de planificar el despliegue. No cambia ni elimina datos.

  El esquema legado no guardaba la programación (fecha/hora) asociada a cada envío.
  Revisar especialmente citas con varios envíos históricos y cualquier pendiente.
  La migración propuesta conserva todas las filas sin modificarlas. Las filas legadas
  reciben identidad NULL y no se reclamarán automáticamente porque no es posible demostrar
  a qué programación pertenecen. El servicio evita crear un recordatorio inmediato si un
  envío legado cae en las 24 horas previas a la cita actual; el precheck permite revisar
  esos casos ambiguos antes del despliegue.
*/
SET NOCOUNT ON;

SELECT estado, COUNT_BIG(*) AS cantidad
FROM dbo.Recordatorio_Cita
GROUP BY estado
ORDER BY estado;

SELECT r.id_recordatorio, r.id_cita, r.canal, r.estado, r.programado_para,
       r.enviado_en, r.intentos, c.fecha_hora AS fecha_hora_actual,
       c.estado AS estado_actual
FROM dbo.Recordatorio_Cita AS r
JOIN dbo.Cita AS c ON c.id_cita = r.id_cita
WHERE r.estado = 'pendiente'
ORDER BY c.fecha_hora, r.id_cita, r.programado_para;

SELECT r.id_cita, r.canal, COUNT_BIG(*) AS envios_historicos,
       MAX(r.enviado_en) AS ultimo_envio,
       c.fecha_hora AS fecha_hora_actual,
       c.estado AS estado_actual,
       CASE WHEN MAX(r.enviado_en) >= DATEADD(hour, -24, c.fecha_hora)
                  AND MAX(r.enviado_en) < c.fecha_hora
            THEN 'ultimo envio dentro de las 24 horas previas'
            ELSE 'relacion con la programacion actual ambigua'
       END AS observacion
FROM dbo.Recordatorio_Cita AS r
JOIN dbo.Cita AS c ON c.id_cita = r.id_cita
WHERE r.estado = 'enviado'
  AND LOWER(LTRIM(RTRIM(c.estado))) IN ('programada', 'agendada', 'confirmada', 'confirmado')
GROUP BY r.id_cita, r.canal, c.fecha_hora, c.estado
HAVING COUNT_BIG(*) > 1
    OR MAX(r.enviado_en) < DATEADD(hour, -24, c.fecha_hora)
    OR MAX(r.enviado_en) >= c.fecha_hora
ORDER BY c.fecha_hora, r.id_cita, r.canal;
