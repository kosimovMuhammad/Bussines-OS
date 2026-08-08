using BusinessOS.Application.Abstraction;
using BusinessOS.Infrastructure.Auth;
using BusinessOS.Infrastructure.BackgroundJobs;
using BusinessOS.Infrastructure.Billing;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Ai;
using BusinessOS.Infrastructure.Calendar;
using BusinessOS.Infrastructure.Data.Interceptors;
using BusinessOS.Infrastructure.Email;
using BusinessOS.Infrastructure.Files;
using BusinessOS.Infrastructure.Mail;
using BusinessOS.Infrastructure.Payments;
using BusinessOS.Infrastructure.Search;
using BusinessOS.Infrastructure.Tenancy;
using BusinessOS.Infrastructure.WhatsApp;
using BusinessOS.Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BusinessOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICurrentTenantContext, CurrentTenantContext>();
        services.AddSingleton<CurrentUserContext>();
        services.AddSingleton<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserContext>());

        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddScoped<ImmutableRecordSaveChangesInterceptor>();
        services.AddScoped<ActivityLogSaveChangesInterceptor>();

        services.AddDbContext<DataContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured");
            options.UseNpgsql(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<TenantSaveChangesInterceptor>(),
                sp.GetRequiredService<ImmutableRecordSaveChangesInterceptor>(),
                sp.GetRequiredService<ActivityLogSaveChangesInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DataContext>());

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IMailService, SmtpMailService>();
        services.AddSingleton<IFileStorageService, S3FileStorageService>();
        services.AddSingleton<ITokenCipher, AesGcmTokenCipher>();
        services.AddSingleton<IOAuthStateService, HmacOAuthStateService>();

        services.AddHttpClient();
        services.AddSingleton<IWhatsAppClient, MetaWhatsAppClient>();
        services.AddSingleton<IGmailClient, GmailClient>();
        services.AddSingleton<IOutlookClient, OutlookClient>();
        services.AddSingleton<IGoogleCalendarClient, GoogleCalendarClient>();
        services.AddSingleton<IGeminiClient, GeminiClient>();
        services.AddSingleton<IQuotationPdfRenderer, QuestPdfQuotationRenderer>();
        services.AddSingleton<IInvoicePdfRenderer, QuestPdfInvoiceRenderer>();
        services.AddSingleton<IPaymentProvider, UnconfiguredPaymentProvider>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IWebhookDeliveryService, WebhookDeliveryService>();

        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        services.AddHostedService<QueuedHostedService>();
        services.AddHostedService<WorkflowSchedulerHostedService>();
        services.AddHostedService<SubscriptionInvoicingHostedService>();

        return services;
    }
}
