using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Commands;
using Docs.Domain.Entities;
using Docs.Domain.ValueObjects;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public class CreateFromTemplateHandler(IDocumentRepository repository) 
    : IRequestHandler<CreateFromTemplateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateFromTemplateCommand request, CancellationToken cancellationToken)
    {
        string docTitle;
        string docDescription = "";
        DocumentType docType = DocumentType.List;
        List<(string PageTitle, string Content)> templatePages = new();

        if (request.TemplateDocumentId.HasValue && request.TemplateDocumentId.Value != Guid.Empty)
        {
            var customTemplate = await repository.GetByIdAsync(request.TemplateDocumentId.Value, cancellationToken);
            if (customTemplate == null)
                return Result<Guid>.Failure("Custom template document not found");

            docTitle = !string.IsNullOrWhiteSpace(request.CustomTitle) ? request.CustomTitle : customTemplate.Title;
            docDescription = customTemplate.Description;
            docType = DocumentType.List;

            var pages = await repository.GetPagesByDocumentIdAsync(customTemplate.Id, cancellationToken);
            foreach (var p in pages.Where(page => !page.IsDeleted))
            {
                templatePages.Add((p.Title, p.Content));
            }
        }
        else
        {
            var key = (request.TemplateKey ?? "").ToLowerInvariant();
            switch (key)
            {
                case "project-overview":
                    docTitle = request.CustomTitle ?? "Project Overview";
                    docDescription = "Summarize goals, scope, and milestones";
                    docType = DocumentType.List;
                    templatePages.Add(("Overview & Scope", @"
                        <h1>📌 Project Overview</h1>
                        <p>Welcome to your new project workspace! Use this document to clarify objectives, scope, and key deliverables.</p>
                        <h2>🎯 Project Objectives</h2>
                        <ul>
                            <li><strong>Objective 1:</strong> Deliver core product features on schedule.</li>
                            <li><strong>Objective 2:</strong> Maintain high quality standards and user satisfaction.</li>
                        </ul>
                        <h2>📅 Key Milestones</h2>
                        <ul>
                            <li>[ ] Kickoff & Architecture Review</li>
                            <li>[ ] MVP Sprint Completion</li>
                            <li>[ ] QA & User Acceptance Testing</li>
                            <li>[ ] Production Release</li>
                        </ul>
                        <h2>⚠️ Risks & Dependencies</h2>
                        <p>Document any technical or timeline risks here...</p>
                    "));
                    break;

                case "meeting-notes":
                    docTitle = request.CustomTitle ?? "Meeting Notes";
                    docDescription = "Capture an agenda, notes, and action items";
                    docType = DocumentType.MeetingNote;
                    templatePages.Add(("Meeting Notes", $@"
                        <h1>📝 Meeting Notes - {DateTime.UtcNow:yyyy-MM-dd}</h1>
                        <p><strong>Attendees:</strong> @Team</p>
                        <p><strong>Facilitator:</strong> Workspace Owner</p>
                        <hr/>
                        <h2>📋 Agenda</h2>
                        <ol>
                            <li>Review sprint updates & roadmap</li>
                            <li>Discuss key technical roadblocks</li>
                            <li>Assign upcoming tasks and responsibilities</li>
                        </ol>
                        <h2>💡 Key Decisions</h2>
                        <ul>
                            <li>Decision 1: Approved architecture adjustments.</li>
                        </ul>
                        <h2>✅ Action Items</h2>
                        <ul>
                            <li>[ ] Follow up with client regarding integration specs</li>
                            <li>[ ] Schedule review meeting for next Tuesday</li>
                        </ul>
                    "));
                    break;

                case "wiki":
                    docTitle = request.CustomTitle ?? "Team Wiki";
                    docDescription = "Organize information in one place";
                    docType = DocumentType.Wiki;
                    templatePages.Add(("Getting Started", @"
                        <h1>📚 Team Knowledge Base</h1>
                        <p>Welcome to our workspace wiki! This central hub holds documentation, SOPs, and developer guidelines.</p>
                        <h2>🚀 Quick Links</h2>
                        <ul>
                            <li><a href='#'>Onboarding Guide</a></li>
                            <li><a href='#'>API Documentation</a></li>
                            <li><a href='#'>Design System Specs</a></li>
                        </ul>
                        <h2>📌 Coding Standards</h2>
                        <p>Ensure all code follows Clean Architecture, CQRS, and Angular best practices.</p>
                    "));
                    break;

                case "client-onboarding":
                    docTitle = request.CustomTitle ?? "Client Onboarding";
                    docDescription = "Client summary, requirements, and handover";
                    docType = DocumentType.List;
                    templatePages.Add(("Client Profile", @"
                        <h1>🏢 Client Onboarding Brief</h1>
                        <h2>👤 Client Information</h2>
                        <p><strong>Company Name:</strong> Enterprise Client</p>
                        <p><strong>Key Stakeholders:</strong> John Doe (Project Manager)</p>
                        <h2>📋 Onboarding Checklist</h2>
                        <ul>
                            <li>[ ] Account setup & permissions granted</li>
                            <li>[ ] Kickoff call completed</li>
                            <li>[ ] Requirements gather & signed off</li>
                            <li>[ ] Initial CRM integration live</li>
                        </ul>
                    "));
                    break;

                default:
                    docTitle = request.CustomTitle ?? "Untitled Document";
                    docDescription = "";
                    docType = DocumentType.List;
                    templatePages.Add(("Page 1", "<p>Start typing or use / for commands...</p>"));
                    break;
            }
        }

        var document = Document.Create(
            request.TenantId,
            docTitle,
            docDescription,
            docType,
            request.OwnerId,
            null,
            null);

        var permission = DocumentPermission.CreateForUser(document.Id, request.OwnerId, true, true, true);
        document.AddPermission(permission);

        await repository.AddAsync(document, cancellationToken);

        int order = 0;
        foreach (var pageData in templatePages)
        {
            var page = Page.Create(document.Id, null, pageData.PageTitle, pageData.Content, order++);
            await repository.AddPageAsync(page, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(document.Id);
    }
}
