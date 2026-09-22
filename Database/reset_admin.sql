UPDATE Usuario 
SET contrasena = '$2a$11$CxlqXT8AY0OmVg5fY6b32.trpz8VVsRboiWbWc1HEvnTUS7CgLiYu', 
    intentos_fallidos = 0 
WHERE correo = 'admin@smiletrack.co';

SELECT LEN(contrasena) as largo_hash, LEFT(contrasena,10) as inicio, estado, intentos_fallidos 
FROM Usuario WHERE correo = 'admin@smiletrack.co';
