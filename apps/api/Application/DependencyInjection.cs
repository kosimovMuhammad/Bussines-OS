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
        services.AddScoped<IWhatsAppTokenRefreshService, WhatsAppTokenRefreshService>();
        services.AddScoped<ITelegramService, TelegramService>();
        services.AddScoped<IInstagramService, InstagramService>();
        services.AddScoped<IInstagramTokenRefreshService, InstagramTokenRefreshService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<ICalendarConnectionService, CalendarConnectionService>();
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<IAiUsageService, AiUsageService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IRoleSeedingService, RoleSeedingService>();
        services.AddScoped<IRolesService, RolesService>();
        services.AddScoped<IPermissionsService, PermissionsService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ITaskDueNotificationService, TaskDueNotificationService>();
        services.AddScoped<ISessionsService, SessionsService>();
        services.AddScoped<IApiKeysService, ApiKeysService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IWorkflowActionExecutor, WorkflowActionExecutor>();
        services.AddScoped<IWorkflowExecutionService, WorkflowExecutionService>();
        services.AddScoped<IWorkflowSchedulerService, WorkflowSchedulerService>();
        services.AddSingleton<IWorkflowRateLimiter, InMemoryWorkflowRateLimiter>();
        services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ILimitEnforcementService, LimitEnforcementService>();
        services.AddScoped<IFeatureAccessService, FeatureAccessService>();
        services.AddScoped<IInvoiceGenerationService, InvoiceGenerationService>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<ISubscriptionInvoiceService, SubscriptionInvoiceService>();
        services.AddScoped<IReportQueryService, ReportQueryService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IEmployeePerformanceService, EmployeePerformanceService>();
        services.AddScoped<IRevenueReportService, RevenueReportService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ICategoriesService, CategoriesService>();
        services.AddScoped<IProductsService, ProductsService>();
        services.AddScoped<ISuppliersService, SuppliersService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IPurchaseOrdersService, PurchaseOrdersService>();
        services.AddScoped<IExpensesService, ExpensesService>();
        services.AddScoped<IContractsService, ContractsService>();
        services.AddScoped<ITemplatesService, TemplatesService>();
        services.AddScoped<IDashboardReportsService, DashboardReportsService>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IArchiveService, ArchiveService>();
        services.AddScoped<IWebhooksService, WebhooksService>();

        return services;
    }
}
