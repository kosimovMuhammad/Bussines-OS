using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Tasks;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("tasks")]
[Authorize]
[RequireModule(ModuleName.Tasks)]
public class TasksController(
    ITasksService tasksService,
    ITimeEntriesService timeEntriesService,
    ICurrentUserContext currentUser,
    IValidator<CreateTaskRequest> createValidator,
    IValidator<UpdateTaskRequest> updateValidator,
    IValidator<UpdateTaskStatusRequest> updateStatusValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryTasksRequest query, CancellationToken ct) =>
        Ok(await tasksService.ListAsync(currentUser.CompanyId!, currentUser.UserId!, currentUser.Role!.Value, query, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await tasksService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateTaskRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await tasksService.CreateAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateTaskRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await tasksService.UpdateAsync(currentUser.CompanyId!, currentUser.UserId!, currentUser.Role!.Value, id, request, ct));
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, UpdateTaskStatusRequest request, CancellationToken ct)
    {
        await updateStatusValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await tasksService.UpdateStatusAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await tasksService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpGet("time/active")]
    public async Task<IActionResult> GetActiveTimer(CancellationToken ct) =>
        Ok(await timeEntriesService.GetActiveAsync(currentUser.CompanyId!, currentUser.UserId!, ct));

    [HttpPost("{id}/time/start")]
    public async Task<IActionResult> StartTimer(string id, [FromBody] StartTimeRequest? request, CancellationToken ct) =>
        Ok(await timeEntriesService.StartAsync(currentUser.CompanyId!, currentUser.UserId!, id, request ?? new StartTimeRequest(null), ct));

    [HttpPost("{id}/time/stop")]
    public async Task<IActionResult> StopTimer(string id, CancellationToken ct) =>
        Ok(await timeEntriesService.StopAsync(currentUser.CompanyId!, currentUser.UserId!, id, ct));

    [HttpGet("{id}/time")]
    public async Task<IActionResult> ListTimeEntries(string id, CancellationToken ct) =>
        Ok(await timeEntriesService.ListForTaskAsync(currentUser.CompanyId!, id, ct));

    [HttpDelete("{id}/time/{entryId}")]
    public async Task<IActionResult> RemoveTimeEntry(string id, string entryId, CancellationToken ct)
    {
        await timeEntriesService.RemoveAsync(currentUser.CompanyId!, id, entryId, ct);
        return NoContent();
    }
}
