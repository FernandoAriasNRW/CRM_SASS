-- ═══════════════════════════════════════════════════════════════════════════════════════════
-- Limpieza de datos huérfanos y duplicados (septiembre de 2026)
--
-- Lo que dejaron tres defectos ya arreglados:
--   · la siembra que corría en cada arranque y duplicaba lo sembrado (46 copias de cada cosa);
--   · los 695 «admin@acme.com» de cuando el filtro de inquilino no veía a nadie al arrancar: se
--     borraron los usuarios, pero no lo que apuntaba a ellos;
--   · la siembra que se llevaba a la demostración los datos de otras organizaciones
--     (docs/AUDITORIA.md §21), que dejó organizaciones con datos y sin usuarios.
--
-- No nombra ninguna organización: vale para cualquier base. Es idempotente: una segunda pasada
-- no borra nada. Borra sólo lo que no tiene dueño o es una copia exacta, y **reasigna** —no borra—
-- lo que tiene contenido propio y un dueño que ya no existe.
--
-- Uso (con copia antes, siempre):
--   mysqldump ... > copia.sql
--   mysql ... -e "START TRANSACTION; SOURCE limpiar-huerfanos-y-duplicados.sql; COMMIT;"
-- Para ver lo que haría sin hacerlo, cambiar COMMIT por ROLLBACK: las cuentas se imprimen igual.
-- ═══════════════════════════════════════════════════════════════════════════════════════════

-- La huella de un documento concatena todas sus páginas; con el límite por defecto (1024) se
-- cortaría y dos documentos distintos podrían parecer iguales.
SET SESSION group_concat_max_len = 1073741824;

-- ── A. Organizaciones sin ningún usuario ─────────────────────────────────────────────────
-- Nadie puede iniciar sesión en ellas, así que nadie vuelve a ver esas filas. Las tablas hijas
-- sin inquilino (páginas, miembros, responsables…) caen por sus claves ajenas en cascada.

CREATE TEMPORARY TABLE org_viva AS SELECT DISTINCT TenantId FROM `User`;

DELETE FROM AutomationExecutions    WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM AutomationRules         WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Comments                WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM ContenidosDeExportacion WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Exportaciones           WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Programaciones          WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Dashboards              WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Reports                 WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM CustomFieldValues       WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM CustomFieldDefinitions  WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM DocumentAnnotations     WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM DocumentMentions        WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM TemplateUsages          WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Documents               WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM EntityPermissions       WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Favorites               WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM SavedViews              WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM NotificationPreferences WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Notifications           WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Messages                WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Conversations           WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM TaskDependencies        WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Tasks                   WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Projects                WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Folders                 WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Spaces                  WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM TicketAttachments       WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Tickets                 WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM IntakeKeys              WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Tags                    WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM Teams                   WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM calendar_events         WHERE tenant_id NOT IN (SELECT TenantId FROM org_viva);
DELETE FROM webhook_subscriptions   WHERE TenantId NOT IN (SELECT TenantId FROM org_viva);

-- ── B. Copias de lo sembrado ──────────────────────────────────────────────────────────────
-- Cada pasada de la siembra repetía lo mismo. De cada grupo se queda la copia **viva**: la que
-- apunta a usuarios que existen o de la que cuelga algo. Las demás apuntan a los administradores
-- fantasma que ya se borraron. Se decide sobre una tabla temporal porque MySQL no deja borrar
-- de una tabla consultándola a la vez.

-- Equipos: se queda el que tiene miembros reales; si ninguno, el más antiguo.
CREATE TEMPORARY TABLE equipo AS
SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Name ORDER BY vivo DESC, CreatedAtUtc, Id) AS orden
FROM (SELECT t.Id, t.TenantId, t.Name, t.CreatedAtUtc,
             EXISTS(SELECT 1 FROM TeamMembers m JOIN `User` u ON u.Id = m.UserId WHERE m.TeamId = t.Id) AS vivo
      FROM Teams t) x;
DELETE FROM Teams WHERE Id IN (SELECT Id FROM equipo WHERE orden > 1);

-- Espacios: se queda el que tiene proyectos o carpetas; si ninguno, el primero. Uno que tenga
-- algo colgando no se borra nunca.
CREATE TEMPORARY TABLE espacio AS
SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Name ORDER BY usos DESC, Id) AS orden
FROM (SELECT s.Id, s.TenantId, s.Name,
             (SELECT COUNT(*) FROM Projects p WHERE p.SpaceId = s.Id)
             + (SELECT COUNT(*) FROM Folders f WHERE f.SpaceId = s.Id) AS usos
      FROM Spaces s) x;
DELETE FROM Spaces WHERE Id IN (SELECT Id FROM espacio WHERE orden > 1)
  AND Id NOT IN (SELECT SpaceId FROM Projects) AND Id NOT IN (SELECT SpaceId FROM Folders);

-- Mensajes de remitentes que no existen: son las copias de la siembra. Después, conversaciones
-- repetidas: se queda la que tiene mensajes; si ninguna, la más antigua.
DELETE FROM Messages WHERE SenderId NOT IN (SELECT Id FROM `User`);
CREATE TEMPORARY TABLE conversacion AS
SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Name ORDER BY mensajes DESC, CreatedAt, Id) AS orden
FROM (SELECT c.Id, c.TenantId, c.Name, c.CreatedAt,
             (SELECT COUNT(*) FROM Messages m WHERE m.ConversationId = c.Id) AS mensajes
      FROM Conversations c) x;
DELETE FROM Conversations WHERE Id IN (SELECT Id FROM conversacion WHERE orden > 1)
  AND Id NOT IN (SELECT ConversationId FROM Messages);

-- Suscripciones de webhook iguales (mismo evento y misma dirección): la más antigua.
CREATE TEMPORARY TABLE webhook AS
SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, EventName, TargetUrl ORDER BY CreatedAt, Id) AS orden
FROM webhook_subscriptions;
DELETE FROM webhook_subscriptions WHERE Id IN (SELECT Id FROM webhook WHERE orden > 1);

-- Tickets iguales (título y descripción): se queda el que tiene un agente que existe o algo
-- colgando (adjuntos, comentarios, eventos); si ninguno, el más antiguo. Uno con algo colgando
-- no se borra nunca.
CREATE TEMPORARY TABLE ticket AS
SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Title, Description ORDER BY vivo DESC, CreatedAt, Id) AS orden
FROM (SELECT t.Id, t.TenantId, t.Title, t.Description, t.CreatedAt,
             (t.AssignedAgentId IS NULL OR t.AssignedAgentId IN (SELECT Id FROM `User`))
             + EXISTS(SELECT 1 FROM TicketAttachments a WHERE a.TicketId = t.Id)
             + EXISTS(SELECT 1 FROM Comments c WHERE c.EntityType = 'Ticket' AND c.EntityId = t.Id)
             + EXISTS(SELECT 1 FROM calendar_events e WHERE e.ticket_id = t.Id) AS vivo
      FROM Tickets t) x;
DELETE FROM Tickets WHERE Id IN (SELECT Id FROM ticket WHERE orden > 1)
  AND Id NOT IN (SELECT TicketId FROM TicketAttachments)
  AND Id NOT IN (SELECT EntityId FROM Comments WHERE EntityType = 'Ticket')
  AND Id NOT IN (SELECT ticket_id FROM calendar_events WHERE ticket_id IS NOT NULL);

-- Eventos: cada siembra los creaba con fechas distintas, así que no son iguales, pero sí son
-- copias: mismo título y un organizador que ya no existe, habiendo otro con el mismo título y un
-- organizador vivo. Un evento sin copia viva se conserva (se reasigna en D).
CREATE TEMPORARY TABLE evento_vivo AS
SELECT DISTINCT tenant_id, title FROM calendar_events WHERE organizer_id IN (SELECT Id FROM `User`);
DELETE e FROM calendar_events e
JOIN evento_vivo v ON v.tenant_id = e.tenant_id AND v.title = e.title
WHERE e.organizer_id NOT IN (SELECT Id FROM `User`);

-- Documentos idénticos: mismo título y mismas páginas. Se queda el que no está en la papelera y
-- es más antiguo. Las páginas y sus permisos caen en cascada.
CREATE TEMPORARY TABLE documento AS
SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId, Title, huella ORDER BY IsDeleted, CreatedAtUtc, Id) AS orden
FROM (SELECT d.Id, d.TenantId, d.Title, d.IsDeleted, d.CreatedAtUtc,
             SHA2(COALESCE((SELECT GROUP_CONCAT(p.Title, '
', p.Content ORDER BY p.`Order`, p.Id SEPARATOR '
--
')
                            FROM Pages p WHERE p.DocumentId = d.Id), ''), 256) AS huella
      FROM Documents d) x;
DELETE FROM DocumentMentions    WHERE DocumentId IN (SELECT Id FROM documento WHERE orden > 1);
DELETE FROM DocumentAnnotations WHERE DocumentId IN (SELECT Id FROM documento WHERE orden > 1);
DELETE FROM Favorites           WHERE EntityType = 'Document' AND EntityId IN (SELECT Id FROM documento WHERE orden > 1);
DELETE FROM Documents           WHERE Id IN (SELECT Id FROM documento WHERE orden > 1);

-- ── C. Lo que apunta a algo que ya no existe ─────────────────────────────────────────────
-- Sin aquello de lo que cuelga, nadie lo vuelve a ver.

DELETE FROM Notifications           WHERE RecipientUserId NOT IN (SELECT Id FROM `User`);
DELETE FROM TeamMembers             WHERE UserId NOT IN (SELECT Id FROM `User`);
DELETE FROM TaskAssignees           WHERE UserId NOT IN (SELECT Id FROM `User`);
DELETE FROM DocumentPermissions     WHERE UserId IS NOT NULL AND UserId NOT IN (SELECT Id FROM `User`);
DELETE FROM DocumentPermissions     WHERE TeamId IS NOT NULL AND TeamId NOT IN (SELECT Id FROM Teams);
DELETE FROM EntityPermissions       WHERE UserId IS NOT NULL AND UserId NOT IN (SELECT Id FROM `User`);
DELETE FROM EntityPermissions       WHERE TeamId IS NOT NULL AND TeamId NOT IN (SELECT Id FROM Teams);
DELETE FROM SavedViews              WHERE UserId NOT IN (SELECT Id FROM `User`);
DELETE FROM NotificationPreferences WHERE UserId NOT IN (SELECT Id FROM `User`);
DELETE FROM Favorites               WHERE UserId NOT IN (SELECT Id FROM `User`);
DELETE FROM Messages                WHERE ConversationId NOT IN (SELECT Id FROM Conversations);
DELETE FROM DocumentMentions        WHERE PageId NOT IN (SELECT Id FROM Pages);
DELETE FROM DocumentAnnotations     WHERE PageId NOT IN (SELECT Id FROM Pages);
DELETE FROM CustomFieldValues       WHERE DefinitionId NOT IN (SELECT Id FROM CustomFieldDefinitions);
DELETE FROM Comments WHERE EntityType = 'Task'       AND EntityId NOT IN (SELECT Id FROM Tasks);
DELETE FROM Comments WHERE EntityType = 'Project'    AND EntityId NOT IN (SELECT Id FROM Projects);
DELETE FROM Comments WHERE EntityType = 'Ticket'     AND EntityId NOT IN (SELECT Id FROM Tickets);
DELETE FROM Comments WHERE EntityType = 'Annotation' AND EntityId NOT IN (SELECT Id FROM DocumentAnnotations);
DELETE FROM TemplateUsages WHERE `Key` REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-' AND `Key` NOT IN (SELECT Id FROM Documents);
DELETE FROM ContenidosDeExportacion WHERE ExportacionId NOT IN (SELECT Id FROM Exportaciones);
DELETE FROM Exportaciones           WHERE ReportId NOT IN (SELECT Id FROM Reports);
DELETE FROM Programaciones          WHERE ReportId NOT IN (SELECT Id FROM Reports);

-- ── D. Lo que tiene contenido propio y un dueño que ya no existe: se reasigna ─────────────
-- Borrarlo perdería el único ejemplar («Arquitectura del Sistema», un acta…). Pasa al
-- administrador más antiguo de su organización. Los enlaces rotos se vacían.

CREATE TEMPORARY TABLE admin_de AS
SELECT TenantId, Id AS AdminId
FROM (SELECT TenantId, Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAtUtc, Id) AS orden
      FROM `User` WHERE RoleId = 1 AND IsDeleted = 0) x
WHERE orden = 1;

UPDATE Documents d JOIN admin_de a ON a.TenantId = d.TenantId
   SET d.OwnerId = a.AdminId WHERE d.OwnerId NOT IN (SELECT Id FROM `User`);
UPDATE calendar_events e JOIN admin_de a ON a.TenantId = e.tenant_id
   SET e.organizer_id = a.AdminId WHERE e.organizer_id NOT IN (SELECT Id FROM `User`);
UPDATE Projects p JOIN admin_de a ON a.TenantId = p.TenantId
   SET p.OwnerId = a.AdminId WHERE p.OwnerId NOT IN (SELECT Id FROM `User`);

UPDATE Tickets SET AssignedAgentId = NULL WHERE AssignedAgentId IS NOT NULL AND AssignedAgentId NOT IN (SELECT Id FROM `User`);
UPDATE Tickets SET TeamId = NULL          WHERE TeamId IS NOT NULL AND TeamId NOT IN (SELECT Id FROM Teams);
UPDATE Documents SET TeamId = NULL        WHERE TeamId IS NOT NULL AND TeamId NOT IN (SELECT Id FROM Teams);
UPDATE calendar_events SET project_id = NULL WHERE project_id IS NOT NULL AND project_id NOT IN (SELECT Id FROM Projects);
UPDATE calendar_events SET task_id = NULL    WHERE task_id IS NOT NULL AND task_id NOT IN (SELECT Id FROM Tasks);
UPDATE calendar_events SET ticket_id = NULL  WHERE ticket_id IS NOT NULL AND ticket_id NOT IN (SELECT Id FROM Tickets);
