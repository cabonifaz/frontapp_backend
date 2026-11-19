-- Eliminación de cuenta iniciada desde la app (requisito de Google Play / App Store).
-- Ejecutar en cada base de datos (staging y producción) ANTES de desplegar el backend.
--
-- Qué hace:
--  * Anonimiza la cuenta (nombre, correo, teléfono, foto, etc.) y la desactiva: ya no puede iniciar sesión.
--  * Borra: cuestionario, perfil de profesor, tokens push, amistades, seguimientos y notificaciones.
--  * Cancela sus partidos y clases pendientes, y lo retira de las ligas.
--  * Conserva los resultados de partidos con otros jugadores, ya sin nombre ni foto (como indica la política).
--  * Los mensajes de chat (MongoDB) los borra el backend.

DROP PROCEDURE IF EXISTS sp_usuario_eliminar_cuenta;

DELIMITER $$
CREATE PROCEDURE sp_usuario_eliminar_cuenta(
    IN  p_id_usuario INT UNSIGNED,
    OUT p_exito      TINYINT,
    OUT p_mensaje    VARCHAR(255)
)
proc: BEGIN
    DECLARE v_existe     INT DEFAULT 0;
    DECLARE v_admin      TINYINT DEFAULT 0;
    DECLARE v_cancelado  INT UNSIGNED;
    DECLARE v_retirado   INT UNSIGNED;
    DECLARE v_rechazado  INT UNSIGNED;
    DECLARE v_clase_canc INT UNSIGNED;
    DECLARE v_insc_ret   INT UNSIGNED;
    DECLARE v_deporte    INT UNSIGNED;
    DECLARE v_fin        TINYINT DEFAULT 0;
    DECLARE v_reg        INT DEFAULT 0;
    DECLARE cur_dep CURSOR FOR
        SELECT DISTINCT id_deporte FROM usuario_deporte WHERE id_usuario = p_id_usuario;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_fin = 1;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SET p_exito = 0;
        SET p_mensaje = 'No se pudo eliminar la cuenta. Intenta nuevamente.';
    END;

    SET p_exito = 0;

    SELECT COUNT(*), IFNULL(MAX(es_admin), 0) INTO v_existe, v_admin
    FROM usuario WHERE id_usuario = p_id_usuario AND estado = 1;

    IF v_existe = 0 THEN
        SET p_mensaje = 'La cuenta no existe o ya fue eliminada.'; LEAVE proc;
    END IF;
    IF v_admin = 1 THEN
        SET p_mensaje = 'Las cuentas de administrador no se pueden eliminar desde la app.'; LEAVE proc;
    END IF;

    SELECT id_maestro INTO v_cancelado  FROM maestro WHERE codigo = 'ESTADO_CANCELADO' AND estado = 1 LIMIT 1;
    SELECT id_maestro INTO v_retirado   FROM maestro WHERE codigo = 'PART_RETIRADO'    AND estado = 1 LIMIT 1;
    SELECT id_maestro INTO v_rechazado  FROM maestro WHERE codigo = 'PART_RECHAZADO'   AND estado = 1 LIMIT 1;
    SELECT id_maestro INTO v_clase_canc FROM maestro WHERE codigo = 'CLASE_CANCELADA'  AND estado = 1 LIMIT 1;
    SELECT id_maestro INTO v_insc_ret   FROM maestro WHERE codigo = 'INSC_RETIRADA'    AND estado = 1 LIMIT 1;

    START TRANSACTION;

    -- 1) Partidos abiertos: se cancelan los suyos (y los singles donde juega); en dobles ajenos se retira
    UPDATE partido p
    INNER JOIN partido_participante pp ON pp.id_partido = p.id_partido AND pp.id_usuario = p_id_usuario AND pp.estado = 1
    INNER JOIN maestro mp ON mp.id_maestro = pp.id_estado_participante
    INNER JOIN maestro ep ON ep.id_maestro = p.id_estado
                         AND ep.codigo IN ('ESTADO_BUSCANDO', 'ESTADO_PENDIENTE', 'ESTADO_CONFIRMADO')
    INNER JOIN maestro tj ON tj.id_maestro = p.id_tipo_juego
    SET p.id_estado = v_cancelado, p.fecha_modificacion = NOW()
    WHERE p.id_creador = p_id_usuario
       OR (tj.codigo = 'JUEGO_1VS1' AND mp.codigo IN ('PART_CREADOR', 'PART_ACEPTADO'));

    UPDATE partido_participante pp
    INNER JOIN maestro mp ON mp.id_maestro = pp.id_estado_participante
                         AND mp.codigo IN ('PART_CREADOR', 'PART_ACEPTADO', 'PART_INVITADO', 'PART_POSTULANTE')
    INNER JOIN partido p  ON p.id_partido = pp.id_partido
    INNER JOIN maestro ep ON ep.id_maestro = p.id_estado
                         AND ep.codigo IN ('ESTADO_BUSCANDO', 'ESTADO_PENDIENTE', 'ESTADO_CONFIRMADO')
    SET pp.id_estado_participante = IF(mp.codigo IN ('PART_CREADOR', 'PART_ACEPTADO'), v_retirado, v_rechazado),
        pp.fecha_modificacion = NOW()
    WHERE pp.id_usuario = p_id_usuario AND pp.estado = 1;

    UPDATE partido_foto SET estado = 0, fecha_modificacion = NOW() WHERE id_usuario = p_id_usuario;
    DELETE FROM partido_en_vivo_delegado WHERE id_usuario = p_id_usuario OR id_otorgado_por = p_id_usuario;

    -- 2) Clases pendientes
    UPDATE clase c
    INNER JOIN maestro ec ON ec.id_maestro = c.id_estado AND ec.codigo IN ('CLASE_SOLICITADA', 'CLASE_ACEPTADA')
    SET c.id_estado = v_clase_canc, c.fecha_modificacion = NOW()
    WHERE c.id_alumno = p_id_usuario OR c.id_profesor = p_id_usuario;
    UPDATE clase SET comentario = NULL WHERE id_alumno = p_id_usuario;
    UPDATE resultado SET comentario_rival = NULL WHERE creado_por_usuario = p_id_usuario;

    -- 3) Ligas: se retira (el historial de partidos queda anónimo)
    UPDATE liga_inscripcion
    SET id_estado_inscripcion = v_insc_ret, fecha_modificacion = NOW()
    WHERE id_usuario = p_id_usuario OR id_companero = p_id_usuario;

    -- 4) Datos personales y relaciones
    DELETE FROM usuario_cuestionario
    WHERE id_usuario_deporte IN (SELECT id_usuario_deporte FROM usuario_deporte WHERE id_usuario = p_id_usuario);
    DELETE FROM usuario_profesor_info WHERE id_usuario = p_id_usuario;
    DELETE FROM usuario_push_token    WHERE id_usuario = p_id_usuario;
    DELETE FROM amistad               WHERE id_usuario_solicitante = p_id_usuario OR id_usuario_receptor = p_id_usuario;
    DELETE FROM seguimiento           WHERE id_seguidor = p_id_usuario OR id_seguido = p_id_usuario;
    DELETE FROM notificacion          WHERE id_usuario = p_id_usuario;
    UPDATE notificacion SET id_actor = NULL WHERE id_actor = p_id_usuario;

    -- 5) Perfil deportivo fuera del ranking
    UPDATE usuario_deporte SET estado = 0, fecha_modificacion = NOW() WHERE id_usuario = p_id_usuario;

    -- 6) Cuenta anonimizada y desactivada
    UPDATE usuario
    SET nombre = 'Usuario', apellidos = 'eliminado',
        correo = CONCAT('eliminado_', id_usuario, '@eliminado.invalid'),
        contrasena_hash = NULL, telefono = NULL, fecha_nacimiento = NULL, sobre_mi = NULL,
        direccion = NULL, foto_perfil_url = NULL, id_mano_dominante = NULL,
        es_profesor = 0, estado = 0, fecha_modificacion = NOW()
    WHERE id_usuario = p_id_usuario;

    COMMIT;

    -- 7) Recalcular posiciones del ranking en sus deportes
    OPEN cur_dep;
    dep: LOOP
        FETCH cur_dep INTO v_deporte;
        IF v_fin = 1 THEN LEAVE dep; END IF;
        CALL sp_ranking_recalcular(v_deporte, v_reg);
    END LOOP;
    CLOSE cur_dep;

    SET p_exito = 1;
    SET p_mensaje = 'Tu cuenta fue eliminada.';
END$$
DELIMITER ;
