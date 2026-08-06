using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Invoicing;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("invoices")]
[Authorize]
[RequireModule(ModuleName.Invoices)]
public class InvoicingController(
    IInvoicingService invoicingService,
    ICurrentUserContext currentUser,
    IValidator<CreateInvoiceRequest> createValidator,
    IValidator<UpdateInvoiceRequest> updateValidator,
    IValidator<UpdateInvoiceStatusRequest> updateStatusValidator,
    IValidator<CreatePaymentRequest> createPaymentValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryInvoicesRequest query, CancellationToken ct) =>
        Ok(await invoicingService.ListAsync(currentUser.CompanyId!, query, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await invoicingService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateInvoiceRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await invoicingService.CreateAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateInvoiceRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await invoicingService.UpdateAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, UpdateInvoiceStatusRequest request, CancellationToken ct)
    {
        await updateStatusValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await invoicingService.UpdateStatusAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await invoicingService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpGet("{id}/payments")]
    public async Task<IActionResult> ListPayments(string id, CancellationToken ct) =>
        Ok(await invoicingService.ListPaymentsAsync(currentUser.CompanyId!, id, ct));

    [HttpPost("{id}/payments")]
    public async Task<IActionResult> AddPayment(string id, CreatePaymentRequest request, CancellationToken ct)
    {
        await createPaymentValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await invoicingService.AddPaymentAsync(currentUser.CompanyId!, id, request, ct));
    }
}
