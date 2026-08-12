-- ═══════════════════════════════════════════════════════════════════════════════
-- Sellado del historial de migraciones
-- ═══════════════════════════════════════════════════════════════════════════════
--
-- CUÁNDO EJECUTARLO
--
-- Una sola vez, y sólo sobre una base de datos creada por el mecanismo anterior
-- (`EnsureCreated()` + `CreateTables()`, retirado en agosto de 2026). Esas bases tienen
-- el esquema completo pero el historial de migraciones vacío, así que `Migrate()` intenta
-- crear tablas que ya existen y falla con el error 1050.
--
-- Sellar significa declarar como aplicadas las migraciones que describen el esquema que
-- la base ya tiene. A partir de ahí `Migrate()` sólo aplica lo nuevo.
--
-- NO lo ejecutes si la base está vacía o no existe: en ese caso arranca la aplicación y
-- `Migrate()` la construye entera desde cero, que es el camino normal.
--
-- Es idempotente: puede ejecutarse dos veces sin efecto adicional.
--
-- La lista de abajo es una foto del 12 de agosto de 2026 y **no hay que actualizarla**
-- cuando se añadan migraciones nuevas: sella el esquema que existía en la transición, y de
-- ahí en adelante `Migrate()` aplica el resto por su cuenta.
--
-- USO
--
--   mysql -u root -p crm_saas < scripts/db/sellar-historial-migraciones.sql
--
-- Sustituye `crm_saas` por el nombre real de tu base (el de la cadena
-- `ConnectionStrings:DefaultConnection`).
--
-- QUÉ HACE, EN ORDEN
--
--   1. Crea las dos tablas de historial si no existen.
--   2. Pone al día las columnas que antes se parcheaban con ALTER TABLE crudos al
--      arrancar. Sólo hacen falta en bases creadas antes de julio de 2026; en las
--      recientes `EnsureCreated` ya las creó y estos pasos no hacen nada.
--   3. Inserta como aplicadas todas las migraciones existentes.
--
-- ═══════════════════════════════════════════════════════════════════════════════

-- ── 1. Tablas de historial ──────────────────────────────────────────────────────
--
-- Los doce contextos de módulo usan la tabla por defecto de EF; `CrmDbContext` usa la
-- suya, configurada en DatabaseExtensions.cs. Son dos tablas en la misma base.

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    PRIMARY KEY (`MigrationId`)
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS `__ef_migrations_history` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    PRIMARY KEY (`MigrationId`)
) CHARACTER SET utf8mb4;

-- ── 2. Columnas que antes se parcheaban al arrancar ─────────────────────────────
--
-- MySQL no tiene `ADD COLUMN IF NOT EXISTS`, así que se consulta INFORMATION_SCHEMA y se
-- ejecuta el ALTER sólo si falta. `agregar_columna_si_falta` se elimina al final.

DROP PROCEDURE IF EXISTS agregar_columna_si_falta;

DELIMITER $$
CREATE PROCEDURE agregar_columna_si_falta(
    IN nombre_tabla   VARCHAR(64),
    IN nombre_columna VARCHAR(64),
    IN definicion     VARCHAR(255))
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.TABLES
               WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = nombre_tabla)
       AND NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS
                       WHERE TABLE_SCHEMA = DATABASE()
                         AND TABLE_NAME = nombre_tabla
                         AND COLUMN_NAME = nombre_columna) THEN
        SET @sentencia = CONCAT('ALTER TABLE `', nombre_tabla, '` ADD COLUMN `',
                                nombre_columna, '` ', definicion);
        PREPARE stmt FROM @sentencia;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END$$
DELIMITER ;

-- Permisos por entidad: la tabla nació fuera de las migraciones y creció por ALTER.
CALL agregar_columna_si_falta('EntityPermissions', 'TargetType', "varchar(20) NOT NULL DEFAULT 'User'");
CALL agregar_columna_si_falta('EntityPermissions', 'TeamId',     'char(36) NULL');
CALL agregar_columna_si_falta('EntityPermissions', 'RoleName',   'varchar(50) NULL');

DROP PROCEDURE agregar_columna_si_falta;

-- Etiquetas embebidas. Ojo aquí: el ALTER crudo creaba `TagIds` como NULL, pero el modelo
-- la declara NOT NULL (es una colección primitiva serializada a JSON, y una lista vacía es
-- '[]', no NULL). Las bases de julio quedaron con la columna nullable y con NULL en las
-- filas anteriores al parche; esas filas las descarta en silencio el JsonContains de los
-- filtros por etiqueta. Sellar sin corregirlo dejaría la desviación fija para siempre, así
-- que se normaliza el contenido y después se ajusta la nullabilidad al modelo.
DROP PROCEDURE IF EXISTS normalizar_tagids;

DELIMITER $$
CREATE PROCEDURE normalizar_tagids(IN nombre_tabla VARCHAR(64))
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.TABLES
               WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = nombre_tabla) THEN

        IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS
                       WHERE TABLE_SCHEMA = DATABASE()
                         AND TABLE_NAME = nombre_tabla
                         AND COLUMN_NAME = 'TagIds') THEN
            SET @sentencia = CONCAT('ALTER TABLE `', nombre_tabla, '` ADD COLUMN `TagIds` longtext NULL');
            PREPARE stmt FROM @sentencia; EXECUTE stmt; DEALLOCATE PREPARE stmt;
        END IF;

        SET @sentencia = CONCAT('UPDATE `', nombre_tabla,
                                "` SET `TagIds` = '[]' WHERE `TagIds` IS NULL OR `TagIds` = ''");
        PREPARE stmt FROM @sentencia; EXECUTE stmt; DEALLOCATE PREPARE stmt;

        SET @sentencia = CONCAT('ALTER TABLE `', nombre_tabla,
                                '` MODIFY COLUMN `TagIds` longtext NOT NULL');
        PREPARE stmt FROM @sentencia; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    END IF;
END$$
DELIMITER ;

CALL normalizar_tagids('Tasks');
CALL normalizar_tagids('Projects');
CALL normalizar_tagids('Tickets');

DROP PROCEDURE normalizar_tagids;

-- `UserId` pasó a admitir NULL cuando el permiso apunta a un equipo o a un rol. MODIFY es
-- idempotente por naturaleza: deja la columna en el estado deseado sin importar el actual.
-- Se envuelve en un procedimiento para no fallar si la tabla todavía no existe.
DROP PROCEDURE IF EXISTS relajar_userid_de_permisos;

DELIMITER $$
CREATE PROCEDURE relajar_userid_de_permisos()
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.COLUMNS
               WHERE TABLE_SCHEMA = DATABASE()
                 AND TABLE_NAME = 'EntityPermissions'
                 AND COLUMN_NAME = 'UserId') THEN
        ALTER TABLE `EntityPermissions` MODIFY COLUMN `UserId` char(36) NULL;
    END IF;
END$$
DELIMITER ;

CALL relajar_userid_de_permisos();
DROP PROCEDURE relajar_userid_de_permisos;

-- ── 3. Declarar las migraciones como aplicadas ──────────────────────────────────
--
-- El orden no importa: EF sólo comprueba si el identificador está presente.
-- `INSERT IGNORE` hace que repetir el script no rompa nada.

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES
    -- Identity
    ('20260701152556_InitialIdentity',            '9.0.0'),
    ('20260702004031_AddSavedViews',              '9.0.0'),
    ('20260702174718_AddAvatarUrlToUser',         '9.0.0'),
    ('20260703210956_AddSidebarPreferences',      '9.0.0'),
    ('20260708160128_AddUserBioAndPhone',         '9.0.0'),
    ('20260812140324_AddEntityPermissionTargets', '9.0.0'),
    -- Teams
    ('20260701200158_InitialTeams',               '9.0.0'),
    -- Projects
    ('20260701155133_InitialProjects',            '9.0.0'),
    ('20260703141347_AddHierarchySpacesFolders',  '9.0.0'),
    ('20260708141651_AddTagIdsToProject',         '9.0.0'),
    -- WorkItems
    ('20260701155154_InitialWorkItems',           '9.0.0'),
    ('20260708141636_AddTagIdsToWorkTask',        '9.0.0'),
    -- Ticketing
    ('20260701154103_InitialTicketing',           '9.0.0'),
    ('20260708141701_AddTagIdsToTicket',          '9.0.0'),
    -- Notifications
    ('20260701154042_InitialNotifications',       '9.0.0'),
    -- Calendar
    ('20260701154159_InitialCalendar',            '9.0.0'),
    -- Communication
    ('20260701154952_InitialCommunication',       '9.0.0'),
    -- Webhook
    ('20260701154143_InitialWebhook',             '9.0.0'),
    -- Reporting
    ('20260701200407_AddReadModels',              '9.0.0'),
    ('20260701204701_AddReportingReadModels',     '9.0.0'),
    ('20260708142150_AddDashboardEntity',         '9.0.0'),
    -- Tags
    ('20260708141121_InitialTagsMigration',       '9.0.0'),
    -- Docs
    ('20260708222954_InitialDocs',                '9.0.0'),
    ('20260710152714_InitialDocsSchema',          '9.0.0');

-- CrmDbContext (Outbox y tablas transversales), con su propia tabla de historial.
INSERT IGNORE INTO `__ef_migrations_history` (`MigrationId`, `ProductVersion`) VALUES
    ('20260630201141_InitialMySqlMigration',      '9.0.0');

-- ── Comprobación ────────────────────────────────────────────────────────────────
-- Deben salir 24 y 1.
SELECT 'modulos' AS tabla, COUNT(*) AS migraciones_selladas FROM `__EFMigrationsHistory`
UNION ALL
SELECT 'crm',              COUNT(*)                        FROM `__ef_migrations_history`;
