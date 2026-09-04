using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Application.Dashboards;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Repositories;

namespace Reporting.Infrastructure;

public static class ReportingInfrastructureExtensions
{
    public static IServiceCollection AddReportingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ReportingDbContext>(options =>
            options.UseMySql(configuration.GetConnectionString("DefaultConnection"), Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.32-mysql")));

        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IReportingUnitOfWork, ReportingModuleUnitOfWork>();

        services.AddScoped<IReportRepository, EfReportRepository>();
        services.AddScoped<Reporting.Application.Exportaciones.IRepositorioDeExportaciones,
                           Reporting.Infrastructure.Persistence.RepositorioDeExportaciones>();

        // Los tres escritores se registran juntos y el selector los recibe todos. Añadir un
        // formato nuevo es añadir una clase aquí; nadie más se entera.
        services.AddSingleton<Reporting.Application.Exportaciones.IEscritorDeInforme,
                              Reporting.Infrastructure.Exportaciones.EscritorCsv>();
        services.AddSingleton<Reporting.Application.Exportaciones.IEscritorDeInforme,
                              Reporting.Infrastructure.Exportaciones.EscritorExcel>();
        services.AddSingleton<Reporting.Application.Exportaciones.IEscritorDeInforme,
                              Reporting.Infrastructure.Exportaciones.EscritorPdf>();
        services.AddSingleton<Reporting.Infrastructure.Exportaciones.EscritoresDeInforme>();
        services.AddScoped<ICustomDashboardRepository, CustomDashboardRepository>();

        // `IDashboardRepository` NO se registra aquí. Sus consultas cruzan tres módulos, así que
        // su implementación vive en el host —ApiHost/Reporting/ConsultasDelPanel.cs— y allí se
        // registra. El módulo declara qué necesita; quién se lo da es cosa de quien compone.

        return services;
    }
}
