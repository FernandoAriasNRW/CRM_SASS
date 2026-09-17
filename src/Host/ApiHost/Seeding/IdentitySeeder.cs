using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Seeding;

public sealed class IdentitySeeder(IdentityDbContext identityDb, ILogger<IdentitySeeder> logger) : IModuleSeeder
{
    public string Module => "Identity";
    public int Order => 10;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        // Se busca en una variable anulable y sólo al final se asigna al contexto.
        //
        // **Sin el filtro de inquilino, y esto es la causa de un fallo medido.** Identity es
        // el único contexto que no puede abrir `AsTenant` aquí: el inquilino sale del
        // administrador, así que todavía no se sabe cuál es. Con el filtro puesto,
        // `CurrentTenantId` valía `Guid.Empty`, la búsqueda no encontraba al administrador
        // **que sí estaba en la tabla**, y el sembrador lo creaba otra vez. En cada arranque.
        // La base de desarrollo acabó con 695 usuarios y 11 correos distintos —115 filas
        // «admin@acme.com»—, y como el login coge una fila cualquiera de las 115, quien
        // iniciaba sesión no era el administrador que posee los proyectos: «Mis proyectos»
        // enseñaba 0 teniendo cinco.
        //
        // Se apagan los filtros pero se descartan los borrados a mano: `IgnoreQueryFilters`
        // los apaga todos, y resucitar a un administrador que alguien mandó a la papelera
        // sería un fallo peor y más difícil de ver.
        //
        // Y se ordena por fecha: **el más antiguo**. No es un capricho — los proyectos, las
        // tareas y los tickets se sembraron en la primera pasada y apuntan a ese. Quedarse
        // con el último dejaría todo lo demás apuntando a un usuario que ya no existe.
        var existingAdmin = await identityDb.User
                .IgnoreQueryFilters()
                .Where(u => !u.IsDeleted && u.Email.Value == "admin@acme.com")
                .OrderBy(u => u.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
            ?? await identityDb.User
                .IgnoreQueryFilters()
                .Where(u => !u.IsDeleted)
                .OrderBy(u => u.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

        if (existingAdmin is null)
        {
            var email = Email.Create("admin@acme.com").Value!;
            var password = PasswordHash.Create("admin123");
            existingAdmin = User.Create(Guid.NewGuid(), "Admin Administrator", email, password, UserRole.Admin).Value
                ?? throw new InvalidOperationException("No se pudo crear el usuario administrador del seed.");
            identityDb.User.Add(existingAdmin);
            await SaveUsersAsync(cancellationToken);
        }

        // Todo el resto del seed cuelga de este usuario: es el propietario de los
        // proyectos y quien define el tenant.
        var admin = existingAdmin;
        var tenantId = admin.TenantId;

        // Ya se sabe de quién es esto, así que a partir de aquí Identity se comporta como los
        // otros contextos. Sin esta línea, todas las consultas de abajo se filtran contra
        // `Guid.Empty`, vuelven vacías, y el sembrador cree que no hay nada sembrado.
        using var _ = identityDb.AsTenant(tenantId);

        // Alinea usuarios, permisos y vistas huérfanos con el inquilino.
        try
        {
            await identityDb.Database.ExecuteSqlAsync($"UPDATE `User` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
            await identityDb.Database.ExecuteSqlAsync($"UPDATE `EntityPermissions` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
            await identityDb.Database.ExecuteSqlAsync($"UPDATE `SavedViews` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken);
        }
        catch { }

        var existingUsers = await identityDb.User.Where(u => u.TenantId == tenantId).ToListAsync(cancellationToken);
        if (existingUsers.Count < 5)
        {
            var demoUsers = new (string Name, string Email, UserRole Role)[]
            {
                ("Sofia Arismendi", "sofia.arismendi@acme.com", UserRole.Member),
                ("Carlos Mendoza", "carlos.mendoza@acme.com", UserRole.Member),
                ("Lucia Fernandez", "lucia.fernandez@acme.com", UserRole.Member),
                ("Mateo Gomez", "mateo.gomez@acme.com", UserRole.Member),
                ("Valentina Rios", "valentina.rios@acme.com", UserRole.Member),
                ("Alejandro Silva", "alejandro.silva@acme.com", UserRole.Member),
                ("Elena Torres", "elena.torres@acme.com", UserRole.Member),
                ("Diego Morales", "diego.guest@external.com", UserRole.Guest),
                ("Camila Navarro", "camila.guest@external.com", UserRole.Guest),
                ("Javier Roca", "javier.roca@acme.com", UserRole.Member)
            };

            foreach (var demoUser in demoUsers)
            {
                if (existingUsers.Any(u => u.Email.Value == demoUser.Email))
                    continue;

                // Password de los usuarios de demostración. Está en el repositorio a la
                // vista, así que no puede coincidir con ninguna credencial de
                // infraestructura: hasta agosto de 2026 era el mismo que el de MySQL en
                // desarrollo, y eso convertía un dato de demo en una credencial filtrada.
                var created = User.Create(tenantId, demoUser.Name, Email.Create(demoUser.Email).Value!,
                    PasswordHash.Create("DemoAcme2026!"), demoUser.Role);
                if (created.IsSuccess && created.Value != null)
                    identityDb.User.Add(created.Value);
            }

            await SaveUsersAsync(cancellationToken);
            existingUsers = await identityDb.User.Where(u => u.TenantId == tenantId).ToListAsync(cancellationToken);
        }

        await SeedPermissionsAsync(tenantId, cancellationToken);
        await SeedSavedViewsAsync(tenantId, admin, cancellationToken);

        context.TenantId = tenantId;
        context.Admin = admin;
        context.AllUsers = existingUsers;
        context.Members = existingUsers.Where(u => u.Id != admin.Id).ToList();
    }

    private async Task SeedPermissionsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (await identityDb.EntityPermissions.AnyAsync(p => p.TenantId == tenantId, cancellationToken))
            return;

        identityDb.EntityPermissions.AddRange(
            EntityPermission.CreateForRole(tenantId, "Member", "Project", Guid.Empty, "Edit"),
            EntityPermission.CreateForRole(tenantId, "Member", "Task", Guid.Empty, "Edit"),
            EntityPermission.CreateForRole(tenantId, "Member", "Document", Guid.Empty, "Edit"),
            EntityPermission.CreateForRole(tenantId, "Member", "Webhook", Guid.Empty, "View"),
            EntityPermission.CreateForRole(tenantId, "Member", "Ticket", Guid.Empty, "Edit"),
            EntityPermission.CreateForRole(tenantId, "Guest", "Project", Guid.Empty, "View"),
            EntityPermission.CreateForRole(tenantId, "Guest", "Task", Guid.Empty, "Edit"),
            EntityPermission.CreateForRole(tenantId, "Guest", "Ticket", Guid.Empty, "Edit"),
            EntityPermission.CreateForRole(tenantId, "Guest", "Document", Guid.Empty, "View"));
        await identityDb.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSavedViewsAsync(Guid tenantId, User admin, CancellationToken cancellationToken)
    {
        // Las vistas que se sembraron con el nombre interno del módulo se renombran al que
        // usa la pantalla. Sin esto seguirían guardadas y sin verse, que es peor que no
        // tenerlas: ocupan sitio y no aparecen.
        await identityDb.Database.ExecuteSqlAsync(
            $"UPDATE `SavedViews` SET `ModuleName` = 'Tasks' WHERE `ModuleName` = 'WorkItems'",
            cancellationToken);

        if (await identityDb.SavedViews.AnyAsync(v => v.TenantId == tenantId && v.UserId == admin.Id, cancellationToken))
            return;

        identityDb.SavedViews.AddRange(
            // «Tasks», no «WorkItems». El módulo se llama WorkItems por dentro, pero la
            // pantalla pide sus vistas por «Tasks» —como la ruta `/tasks`—, así que una
            // vista sembrada con el nombre interno **no la ve nadie**: la pantalla pedía
            // `/views/Tasks` y el servidor devolvía una lista vacía teniéndola guardada.
            // Es el mismo desajuste de vocabulario que el de `EntityType`.
            SavedView.Create(admin.Id, tenantId, "Tasks", "Mis Tareas Pendientes", "{\"page\":1,\"pageSize\":25,\"searchTerm\":\"\",\"filters\":{\"status\":\"To Do\"}}", true),
            SavedView.Create(admin.Id, tenantId, "Projects", "Proyectos Activos Q3", "{\"page\":1,\"pageSize\":25,\"searchTerm\":\"\",\"filters\":{}}", false),
            SavedView.Create(admin.Id, tenantId, "Tickets", "Tickets Prioritarios", "{\"page\":1,\"pageSize\":25,\"searchTerm\":\"\",\"filters\":{\"priority\":\"High\"}}", false));
        await identityDb.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Guarda usuarios sabiendo que el correo es único en la base, y trata el choque como lo que
    /// es: alguien ya lo sembró.
    ///
    /// <b>La comprobación en memoria no basta.</b> Se mira si el correo ya está antes de añadirlo,
    /// pero entre esa lectura y el guardado puede haber otra instancia sembrando —dos réplicas
    /// arrancando a la vez, que es lo normal en cuanto esto se despliegue más de una vez—. La
    /// única barrera de verdad es el índice único de la base; esto sólo decide qué hacer cuando
    /// salta.
    ///
    /// Se descartan las entidades que chocaron y se sigue: el objetivo de la siembra es dejar la
    /// base con esos usuarios dentro, y si ya están, está cumplido. Lo que **no** se hace es
    /// tragarse cualquier error: un fallo que no sea de duplicidad se relanza, porque una siembra
    /// que calla un error de esquema deja la aplicación a medias sin decirlo.
    /// </summary>
    private async Task<int> SaveUsersAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await identityDb.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateEmail(ex))
        {
            // Se sueltan los usuarios pendientes: reintentar con ellos dentro volvería a chocar.
            foreach (var entry in identityDb.ChangeTracker.Entries<User>()
                         .Where(e => e.State == EntityState.Added)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            logger.LogInformation(
                "El correo de algún usuario de demostración ya existía; se deja el que estaba. "
                + "Es lo esperado al sembrar sobre una base ya sembrada.");

            return 0;
        }
    }

    /// <summary>
    /// Si el error de la base es «esta clave ya existe» y no otra cosa.
    ///
    /// Se mira el código nativo de MySQL (1062, <c>ER_DUP_ENTRY</c>) en lugar de buscar texto en
    /// el mensaje: el mensaje cambia con el idioma del servidor y con la versión, y un filtro por
    /// texto acabaría dejando pasar errores que no son este.
    /// </summary>
    private static bool IsDuplicateEmail(DbUpdateException ex)
        => ex.InnerException is MySqlConnector.MySqlException { ErrorCode: MySqlConnector.MySqlErrorCode.DuplicateKeyEntry };
}
