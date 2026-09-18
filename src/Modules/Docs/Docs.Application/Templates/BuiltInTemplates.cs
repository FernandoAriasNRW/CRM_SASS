using Docs.Domain.ValueObjects;

namespace Docs.Application.Templates;

/// <summary>
/// Las cuatro plantillas que trae el producto, en los dos idiomas de la aplicación.
///
/// <b>Estaban escritas sólo en inglés dentro del handler.</b> La galería ya anunciaba «Acta de
/// reunión» en español, y al pulsarla se creaba un documento titulado «Meeting Notes» con «Action
/// Items» dentro: la aplicación en un idioma y lo que genera en otro, que es justo lo que se pidió
/// que no pasara —«o es inglés o es español, todo lo generado por la aplicación al menos»—.
///
/// <b>El idioma lo manda la pantalla, y no se deduce aquí.</b> La traducción de la interfaz es en
/// tiempo de compilación, así que cada paquete sabe con certeza en qué idioma está. El servidor no
/// tiene esa certeza: la cabecera <c>Accept-Language</c> es la del navegador, no la que la persona
/// eligió en la aplicación, y usarla repetiría el fallo que ya tuvo el selector de idioma.
///
/// Las casillas son listas de tareas de verdad (<c>data-type="taskItem"</c>), no «[ ]» escrito como
/// texto: antes se veían corchetes que no se podían marcar.
/// </summary>
public static class BuiltInTemplates
{
    public static readonly IReadOnlyList<string> Keys =
        ["project-overview", "meeting-notes", "wiki", "client-onboarding"];

    /// <summary>
    /// El contenido de una plantilla, o <c>null</c> si la clave no existe.
    ///
    /// Una clave desconocida devuelve <c>null</c> y no un documento en blanco. Antes caía en un
    /// <c>default</c> que creaba «Untitled Document» y además le contaba un uso a una plantilla
    /// inexistente, así que la petición respondía bien haciendo otra cosa.
    /// </summary>
    public static TemplateContent? For(string key, string? language, DateTime nowUtc)
    {
        var inEnglish = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);

        return key.ToLowerInvariant() switch
        {
            "project-overview" => inEnglish ? ProjectOverviewEn() : ProjectOverviewEs(),
            "meeting-notes" => inEnglish ? MeetingNotesEn(nowUtc) : MeetingNotesEs(nowUtc),
            "wiki" => inEnglish ? WikiEn() : WikiEs(),
            "client-onboarding" => inEnglish ? ClientOnboardingEn() : ClientOnboardingEs(),
            _ => null
        };
    }

    // ── Piezas comunes ──────────────────────────────────────────────────────────────────────

    /// <summary>Una lista de casillas que se pueden marcar, sin marcar.</summary>
    private static string Checklist(params string[] items)
        => "<ul data-type=\"taskList\">"
           + string.Concat(items.Select(e =>
               $"<li data-type=\"taskItem\" data-checked=\"false\"><p>{e}</p></li>"))
           + "</ul>";

    private static string Callout(string tone, string text)
        => $"<div data-tipo=\"aviso\" data-tono=\"{tone}\"><p>{text}</p></div>";

    // ── Resumen de proyecto ─────────────────────────────────────────────────────────────────

    private static TemplateContent ProjectOverviewEs() => new(
        "Resumen de proyecto",
        "Objetivos, alcance e hitos",
        DocumentType.List,
        [("Resumen y alcance",
            "<h1>Resumen del proyecto</h1>"
            + "<p>Deja claro aquí para qué existe el proyecto, qué entra y qué no, y cuándo se da por terminado.</p>"
            + "<h2>Objetivos</h2>"
            + "<ul><li><p><strong>Objetivo 1:</strong> qué tiene que ser verdad al terminar.</p></li>"
            + "<li><p><strong>Objetivo 2:</strong> cómo se va a medir.</p></li></ul>"
            + "<h2>Fuera del alcance</h2>"
            + "<p>Lo que se ha decidido no hacer. Escribirlo evita discutirlo otra vez a mitad de camino.</p>"
            + "<h2>Hitos</h2>"
            + Checklist("Arranque y revisión de la arquitectura", "Primera entrega usable", "Pruebas con usuarios", "Puesta en producción")
            + "<h2>Riesgos y dependencias</h2>"
            + Callout("ojo", "Anota los riesgos con nombre y fecha: un riesgo sin responsable no lo vigila nadie."))]);

    private static TemplateContent ProjectOverviewEn() => new(
        "Project overview",
        "Goals, scope and milestones",
        DocumentType.List,
        [("Overview and scope",
            "<h1>Project overview</h1>"
            + "<p>Make clear why the project exists, what is in and what is out, and when it counts as done.</p>"
            + "<h2>Goals</h2>"
            + "<ul><li><p><strong>Goal 1:</strong> what has to be true at the end.</p></li>"
            + "<li><p><strong>Goal 2:</strong> how it will be measured.</p></li></ul>"
            + "<h2>Out of scope</h2>"
            + "<p>What was decided not to do. Writing it down saves arguing about it again halfway through.</p>"
            + "<h2>Milestones</h2>"
            + Checklist("Kickoff and architecture review", "First usable release", "User testing", "Production release")
            + "<h2>Risks and dependencies</h2>"
            + Callout("ojo", "Give every risk an owner and a date: a risk nobody owns is a risk nobody watches."))]);

    // ── Acta de reunión ─────────────────────────────────────────────────────────────────────

    private static TemplateContent MeetingNotesEs(DateTime nowUtc) => new(
        "Acta de reunión",
        "Orden del día, notas y acuerdos",
        DocumentType.MeetingNote,
        [("Acta",
            $"<h1>Acta de reunión · {nowUtc:yyyy-MM-dd}</h1>"
            + "<p><strong>Asistentes:</strong> </p>"
            + "<p><strong>Modera:</strong> </p>"
            + "<h2>Orden del día</h2>"
            + "<ol><li><p>Punto 1</p></li><li><p>Punto 2</p></li></ol>"
            + "<h2>Notas</h2>"
            + "<p></p>"
            + "<h2>Acuerdos</h2>"
            + Callout("bien", "Un acuerdo por línea, con quién lo lleva. Lo que no tiene responsable no se hace.")
            + "<h2>Tareas</h2>"
            + Checklist("Tarea — responsable — fecha"))]);

    private static TemplateContent MeetingNotesEn(DateTime nowUtc) => new(
        "Meeting notes",
        "Agenda, notes and decisions",
        DocumentType.MeetingNote,
        [("Notes",
            $"<h1>Meeting notes · {nowUtc:yyyy-MM-dd}</h1>"
            + "<p><strong>Attendees:</strong> </p>"
            + "<p><strong>Facilitator:</strong> </p>"
            + "<h2>Agenda</h2>"
            + "<ol><li><p>Item 1</p></li><li><p>Item 2</p></li></ol>"
            + "<h2>Notes</h2>"
            + "<p></p>"
            + "<h2>Decisions</h2>"
            + Callout("bien", "One decision per line, with who owns it. What has no owner does not get done.")
            + "<h2>Action items</h2>"
            + Checklist("Task — owner — date"))]);

    // ── Wiki ────────────────────────────────────────────────────────────────────────────────

    private static TemplateContent WikiEs() => new(
        "Wiki del equipo",
        "Toda la información en un sitio",
        DocumentType.Wiki,
        [("Primeros pasos",
            "<h1>Wiki del equipo</h1>"
            + "<p>El sitio donde vive lo que el equipo necesita saber: cómo se trabaja, dónde está cada cosa y a quién preguntar.</p>"
            + Callout("nota", "Usa una página por tema y cuélgalas unas de otras desde el árbol de la izquierda.")
            + "<h2>Enlaces rápidos</h2>"
            + "<ul><li><p>Guía de incorporación</p></li><li><p>Documentación de la API</p></li><li><p>Sistema de diseño</p></li></ul>"
            + "<h2>Normas de trabajo</h2>"
            + "<p>Cómo se revisa el código, cómo se nombran las ramas, qué hace falta para dar algo por terminado.</p>")]);

    private static TemplateContent WikiEn() => new(
        "Team wiki",
        "Everything in one place",
        DocumentType.Wiki,
        [("Getting started",
            "<h1>Team wiki</h1>"
            + "<p>Where everything the team needs to know lives: how we work, where things are and who to ask.</p>"
            + Callout("nota", "Use one page per topic and nest them from the tree on the left.")
            + "<h2>Quick links</h2>"
            + "<ul><li><p>Onboarding guide</p></li><li><p>API documentation</p></li><li><p>Design system</p></li></ul>"
            + "<h2>Ways of working</h2>"
            + "<p>How code is reviewed, how branches are named, what it takes for something to count as done.</p>")]);

    // ── Alta de cliente ─────────────────────────────────────────────────────────────────────

    private static TemplateContent ClientOnboardingEs() => new(
        "Alta de cliente",
        "Ficha, requisitos y traspaso",
        DocumentType.List,
        [("Ficha del cliente",
            "<h1>Alta de cliente</h1>"
            + "<h2>Datos del cliente</h2>"
            + "<p><strong>Empresa:</strong> </p>"
            + "<p><strong>Contacto principal:</strong> </p>"
            + "<p><strong>Responsable interno:</strong> </p>"
            + "<h2>Requisitos</h2>"
            + "<p>Qué necesita el cliente con sus palabras, antes de traducirlo a tareas.</p>"
            + "<h2>Lista de alta</h2>"
            + Checklist("Cuenta creada y permisos dados", "Reunión de arranque hecha", "Requisitos revisados y aceptados", "Integración funcionando")
            + Callout("peligro", "No se da el alta por terminada sin la aceptación por escrito de los requisitos."))]);

    private static TemplateContent ClientOnboardingEn() => new(
        "Client onboarding",
        "Profile, requirements and handover",
        DocumentType.List,
        [("Client profile",
            "<h1>Client onboarding</h1>"
            + "<h2>Client details</h2>"
            + "<p><strong>Company:</strong> </p>"
            + "<p><strong>Main contact:</strong> </p>"
            + "<p><strong>Internal owner:</strong> </p>"
            + "<h2>Requirements</h2>"
            + "<p>What the client needs, in their own words, before turning it into tasks.</p>"
            + "<h2>Onboarding checklist</h2>"
            + Checklist("Account created and permissions granted", "Kickoff meeting held", "Requirements reviewed and signed off", "Integration working")
            + Callout("peligro", "Onboarding is not done until the requirements are signed off in writing."))]);
}
