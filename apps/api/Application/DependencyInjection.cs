using System.Reflection;
using BusinessOS.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsersService, UsersService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IContactsService, ContactsService>();
        services.AddScoped<IDealsService, DealsService>();
        services.AddScoped<ITasksService, TasksService>();
        services.AddScoped<ITimeEntriesService, TimeEntriesService>();
        services.AddScoped<ICommunicationsService, CommunicationsService>();
        services.AddScoped<IInvoicingService, InvoicingService>();
        services.AddScoped<IFilesService, FilesService>();
        services.AddScoped<IWhatsAppService, WhatsAppService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<ICalendarConnectionService, CalendarConnectionService>();
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        return services;
    }
}
