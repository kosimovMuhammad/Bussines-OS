using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations;
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
using BusinessOS.Infrastructure.Realtime;
using BusinessOS.Infrastructure.Search;
using BusinessOS.Infrastructure.Instagram;
using BusinessOS.Infrastructure.Telegram;
using BusinessOS.Infrastructure.Tenancy;
using BusinessOS.Infrastructure.WhatsApp;
using BusinessOS.Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BusinessOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
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
            // 10622: child rows (AiSummary, RefreshToken, WorkflowAction, ...) are reached through
            // a tenant-filtered parent and never queried across tenants, so the "required end of a
            // relationship has a query filter" warning is expected noise here.
            options.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
            options.AddInterceptors(
                sp.GetRequiredService<TenantSaveChangesInterceptor>(),
                sp.GetRequiredService<ImmutableRecordSaveChangesInterceptor>(),
                sp.GetRequiredService<ActivityLogSaveChangesInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DataContext>());

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IMailService, SmtpMailService>();
        // Development-only fallback so local dev works with zero external dependencies (no AWS
        // credentials, no MinIO/Docker) - see LocalDiskFileStorageService. Production/Staging
        // always get S3FileStorageService, unchanged, regardless of whether AWS_* is configured.
        if (environment.IsDevelopment())
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<IFileStorageService, LocalDiskFileStorageService>();
        }
        else
        {
            services.AddSingleton<IFileStorageService, S3FileStorageService>();
        }
        services.AddSingleton<ITokenCipher, AesGcmTokenCipher>();
        services.AddSingleton<IOAuthStateService, HmacOAuthStateService>();

        services.AddSignalR();
        services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

        services.AddHttpClient();
        services.Configure<WhatsAppPlatformOptions>(options =>
        {
            options.PhoneNumberId = configuration["WHATSAPP_PHONE_NUMBER_ID"] ?? string.Empty;
            options.ApiVersion = configuration["WHATSAPP_API_VERSION"] is { Length: > 0 } apiVersion ? apiVersion : options.ApiVersion;
            options.EmbeddedSignupConfigurationId = configuration["WHATSAPP_ES_CONFIG_ID"] ?? string.Empty;
            options.AppId = configuration["WHATSAPP_APP_ID"] ?? string.Empty;
            options.EmbeddedSignupEnabled = bool.TryParse(configuration["WHATSAPP_EMBEDDED_SIGNUP_ENABLED"], out var esEnabled) && esEnabled;
        });
        services.AddSingleton<IWhatsAppClient, MetaWhatsAppClient>();
        services.AddSingleton<ITelegramApiClient, TelegramApiClient>();
        services.AddSingleton<IInstagramApiClient, InstagramApiClient>();
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
        services.AddHostedService<InstagramTokenRefreshHostedService>();
        services.AddHostedService<WhatsAppTokenRefreshHostedService>();
        services.AddHostedService<SubscriptionInvoicingHostedService>();
        services.AddHostedService<TaskDueNotificationHostedService>();

        return services;
    }
}
