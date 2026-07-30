import { Body, Controller, Delete, Get, Param, Patch, Post, Query, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { InvoicingService } from './invoicing.service';
import { CreateInvoiceDto } from './dto/create-invoice.dto';
import { UpdateInvoiceDto } from './dto/update-invoice.dto';
import { UpdateInvoiceStatusDto } from './dto/update-invoice-status.dto';
import { QueryInvoicesDto } from './dto/query-invoices.dto';
import { CreatePaymentDto } from './dto/create-payment.dto';

@ApiTags('Invoicing')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('invoices')
export class InvoicingController {
  constructor(private readonly invoicingService: InvoicingService) {}

  @Get()
  @ApiOperation({ summary: 'List invoices, with filter/pagination' })
  @ApiResponse({ status: 200, description: 'Paginated list of invoices' })
  findAll(@CompanyId() companyId: string, @Query() query: QueryInvoicesDto) {
    return this.invoicingService.findAll(companyId, query);
  }

  @Post()
  @ApiOperation({ summary: 'Create a new invoice (auto-generates invoiceNumber, status DRAFT)' })
  @ApiResponse({ status: 201, description: 'Created invoice record' })
  @ApiResponse({ status: 400, description: 'contactId/dealId invalid or not in this company' })
  create(@CompanyId() companyId: string, @Body() dto: CreateInvoiceDto) {
    return this.invoicingService.create(companyId, dto);
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get a single invoice, with contact, deal, and payments' })
  @ApiResponse({ status: 200, description: 'Invoice record' })
  @ApiResponse({ status: 404, description: 'Invoice not found' })
  findOne(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.invoicingService.findOne(companyId, id);
  }

  @Patch(':id')
  @ApiOperation({ summary: 'Update invoice fields (not status — use PATCH /invoices/:id/status)' })
  @ApiResponse({ status: 200, description: 'Updated invoice record' })
  @ApiResponse({ status: 404, description: 'Invoice not found' })
  update(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateInvoiceDto) {
    return this.invoicingService.update(companyId, id, dto);
  }

  @Patch(':id/status')
  @ApiOperation({ summary: 'Change an invoice status directly' })
  @ApiResponse({ status: 200, description: 'Updated invoice record' })
  @ApiResponse({ status: 404, description: 'Invoice not found' })
  updateStatus(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateInvoiceStatusDto) {
    return this.invoicingService.updateStatus(companyId, id, dto.status);
  }

  @Delete(':id')
  @ApiOperation({ summary: 'Delete an invoice (only if status is DRAFT)' })
  @ApiResponse({ status: 200, description: 'Invoice deleted' })
  @ApiResponse({ status: 404, description: 'Invoice not found' })
  @ApiResponse({ status: 409, description: 'Invoice is not in DRAFT status' })
  remove(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.invoicingService.remove(companyId, id);
  }

  @Get(':id/payments')
  @ApiOperation({ summary: 'List payments recorded against an invoice' })
  @ApiResponse({ status: 200, description: 'List of payments' })
  @ApiResponse({ status: 404, description: 'Invoice not found' })
  listPayments(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.invoicingService.listPayments(companyId, id);
  }

  @Post(':id/payments')
  @ApiOperation({ summary: 'Record a payment against an invoice (auto-marks PAID once fully paid)' })
  @ApiResponse({ status: 201, description: 'Created payment record' })
  @ApiResponse({ status: 404, description: 'Invoice not found' })
  addPayment(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: CreatePaymentDto) {
    return this.invoicingService.addPayment(companyId, id, dto);
  }
}
