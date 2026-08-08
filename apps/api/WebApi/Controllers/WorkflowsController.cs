using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Automation;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("workflows")]
[Authorize]
[RequireModule(ModuleName.Automation)]
public class WorkflowsController(
    IWorkflowService workflowService,
    ICurrentUserContext currentUser,
    IValidator<CreateWorkflowRequest> createValidator,
    IValidator<UpdateWorkflowRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await workflowService.ListAsync(currentUser.CompanyId!, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await workflowService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateWorkflowRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await workflowService.CreateAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateWorkflowRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await workflowService.UpdateAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await workflowService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> Toggle(string id, CancellationToken ct) =>
        Ok(await workflowService.ToggleAsync(currentUser.CompanyId!, id, ct));

    [HttpGet("{id}/executions")]
    public async Task<IActionResult> ListExecutions(string id, [FromQuery] QueryWorkflowExecutionsRequest query, CancellationToken ct) =>
        Ok(await workflowService.ListExecutionsAsync(currentUser.CompanyId!, id, query, ct));

    [HttpGet("{id}/executions/{executionId}")]
    public async Task<IActionResult> GetExecution(string id, string executionId, CancellationToken ct) =>
        Ok(await workflowService.GetExecutionAsync(currentUser.CompanyId!, id, executionId, ct));
}
