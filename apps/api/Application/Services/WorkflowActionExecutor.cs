using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.ViewModels.Email;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public class WorkflowActionExecutor(
    IAppDbContext db,
    INotificationService notifications,
    IEmailService emailService,
    IWhatsAppService whatsAppService) : IWorkflowActionExecutor
{
    public Task<string?> ExecuteAsync(string companyId, string workflowCreatedByUserId, WorkflowAction action, object? record, IReadOnlyDictionary<string, string?> fieldValues, CancellationToken cancellationToken = default)
    {
        var config = ParseConfig(action.ConfigJson);

        return action.Type switch
        {
            WorkflowActionType.SendNotification => ExecuteSendNotificationAsync(companyId, config, fieldValues, cancellationToken),
            WorkflowActionType.SendEmail => ExecuteSendEmailAsync(companyId, workflowCreatedByUserId, config, fieldValues, cancellationToken),
            WorkflowActionType.SendWhatsAppMessage => ExecuteSendWhatsAppAsync(companyId, config, fieldValues, cancellationToken),
            WorkflowActionType.CreateTask => ExecuteCreateTaskAsync(companyId, config, fieldValues, cancellationToken),
            WorkflowActionType.UpdateField => ExecuteUpdateField(record, config),
            WorkflowActionType.AssignOwner => ExecuteAssignOwner(record, fieldValues, config),
            _ => throw new InvalidOperationException($"Unsupported action type {action.Type}"),
        };
    }

    private async Task<string?> ExecuteSendNotificationAsync(string companyId, Dictionary<string, string?> config, IReadOnlyDictionary<string, string?> fieldValues, CancellationToken cancellationToken)
    {
        var userId = ResolveUserId(config.GetValueOrDefault("userId"), fieldValues)
            ?? throw new InvalidOperationException("SendNotification action has no resolvable recipient user id");

        if (!Enum.TryParse<NotificationType>(config.GetValueOrDefault("type"), true, out var type))
        {
            throw new InvalidOperationException("SendNotification action config.type is missing or invalid");
        }

        var title = TemplateResolver.Resolve(config.GetValueOrDefault("title") ?? "Automation notification", fieldValues);
        var body = TemplateResolver.Resolve(config.GetValueOrDefault("body") ?? string.Empty, fieldValues);

        await notifications.NotifyAsync(companyId, userId, type, title, body, cancellationToken: cancellationToken);
        return JsonSerializer.Serialize(new { userId, title });
    }

    private async Task<string?> ExecuteSendEmailAsync(string companyId, string workflowCreatedByUserId, Dictionary<string, string?> config, IReadOnlyDictionary<string, string?> fieldValues, CancellationToken cancellationToken)
    {
        var contactId = ResolveContactId(fieldValues)
            ?? throw new InvalidOperationException("SendEmail action could not resolve a contact for this record");

        var subject = TemplateResolver.Resolve(config.GetValueOrDefault("subject") ?? string.Empty, fieldValues);
        var body = TemplateResolver.Resolve(config.GetValueOrDefault("body") ?? string.Empty, fieldValues);

        // No natural "acting user" exists for an automated send - the workflow's creator
        // is used as the sending mailbox, matching who set the automation up.
        await emailService.SendAsync(companyId, workflowCreatedByUserId, new SendEmailRequest(contactId, subject, body), cancellationToken);
        return JsonSerializer.Serialize(new { contactId, subject });
    }

    private async Task<string?> ExecuteSendWhatsAppAsync(string companyId, Dictionary<string, string?> config, IReadOnlyDictionary<string, string?> fieldValues, CancellationToken cancellationToken)
    {
        var contactId = ResolveContactId(fieldValues)
            ?? throw new InvalidOperationException("SendWhatsAppMessage action could not resolve a contact for this record");

        var message = TemplateResolver.Resolve(config.GetValueOrDefault("message") ?? string.Empty, fieldValues);

        await whatsAppService.SendAsync(companyId, new SendWhatsAppRequest(contactId, message), cancellationToken);
        return JsonSerializer.Serialize(new { contactId });
    }

    private async Task<string?> ExecuteCreateTaskAsync(string companyId, Dictionary<string, string?> config, IReadOnlyDictionary<string, string?> fieldValues, CancellationToken cancellationToken)
    {
        var title = TemplateResolver.Resolve(config.GetValueOrDefault("title") ?? "Automated task", fieldValues);
        var description = config.GetValueOrDefault("description") is { } d ? TemplateResolver.Resolve(d, fieldValues) : null;
        var assignToRaw = config.GetValueOrDefault("assignTo");
        var assignedTo = assignToRaw == "recordOwner" ? ResolveOwnerId(fieldValues) : assignToRaw;

        var task = new TaskItem { CompanyId = companyId, Title = title, Description = description, AssignedTo = assignedTo };
        db.Tasks.Add(task);

        await Task.CompletedTask;
        return JsonSerializer.Serialize(new { taskId = task.Id });
    }

    private static Task<string?> ExecuteUpdateField(object? record, Dictionary<string, string?> config)
    {
        if (record is null)
        {
            throw new InvalidOperationException("UpdateField requires a triggering record - this workflow's trigger has none");
        }

        var field = config.GetValueOrDefault("field")
            ?? throw new InvalidOperationException("UpdateField action config.field is required");
        var value = config.GetValueOrDefault("value");

        SetProperty(record, field, value);
        return Task.FromResult<string?>(JsonSerializer.Serialize(new { field, value }));
    }

    // Fixed-user assignment only for this MVP pass - round-robin/team-based rules are not
    // implemented (flagged as a known simplification, not silently dropped).
    private static Task<string?> ExecuteAssignOwner(object? record, IReadOnlyDictionary<string, string?> fieldValues, Dictionary<string, string?> config)
    {
        if (record is null)
        {
            throw new InvalidOperationException("AssignOwner requires a triggering record - this workflow's trigger has none");
        }

        var userId = config.GetValueOrDefault("userId")
            ?? throw new InvalidOperationException("AssignOwner action config.userId is required (fixed-user assignment only)");

        var ownerProperty = record.GetType().GetProperty("OwnerId") ?? record.GetType().GetProperty("AssignedTo")
            ?? throw new InvalidOperationException($"{record.GetType().Name} has no owner/assignee property to set");

        ownerProperty.SetValue(record, userId);
        return Task.FromResult<string?>(JsonSerializer.Serialize(new { property = ownerProperty.Name, userId }));
    }

    private static void SetProperty(object record, string propertyName, string? rawValue)
    {
        var property = record.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"{record.GetType().Name} has no property named '{propertyName}'");

        if (rawValue is null)
        {
            property.SetValue(record, null);
            return;
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        object converted = targetType switch
        {
            _ when targetType.IsEnum => Enum.Parse(targetType, rawValue, ignoreCase: true),
            _ when targetType == typeof(DateTime) => DateTime.Parse(rawValue),
            _ => Convert.ChangeType(rawValue, targetType),
        };

        property.SetValue(record, converted);
    }

    private static string? ResolveUserId(string? configValue, IReadOnlyDictionary<string, string?> fieldValues) =>
        configValue == "recordOwner" ? ResolveOwnerId(fieldValues) : configValue;

    private static string? ResolveOwnerId(IReadOnlyDictionary<string, string?> fieldValues) =>
        fieldValues.GetValueOrDefault("OwnerId") ?? fieldValues.GetValueOrDefault("AssignedTo");

    // "ContactId" key presence (not just value) distinguishes "Deal/TaskItem with no
    // linked contact" (key present, null value -> genuinely unresolvable) from "the
    // record IS a Contact" (key absent -> fall back to its own Id).
    private static string? ResolveContactId(IReadOnlyDictionary<string, string?> fieldValues) =>
        fieldValues.ContainsKey("ContactId") ? fieldValues.GetValueOrDefault("ContactId") : fieldValues.GetValueOrDefault("Id");

    private static Dictionary<string, string?> ParseConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(configJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
