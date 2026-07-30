import { BadRequestException, ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { InvoiceStatus, Prisma } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { CreateInvoiceDto } from './dto/create-invoice.dto';
import { UpdateInvoiceDto } from './dto/update-invoice.dto';
import { QueryInvoicesDto } from './dto/query-invoices.dto';
import { CreatePaymentDto } from './dto/create-payment.dto';

const INVOICE_INCLUDE = { contact: true, deal: true, payments: true } as const;

@Injectable()
export class InvoicingService {
  constructor(private readonly prisma: PrismaService) {}

  async findAll(companyId: string, query: QueryInvoicesDto) {
    const page = query.page ?? 1;
    const limit = query.limit ?? 20;

    const where: Prisma.InvoiceWhereInput = { companyId };
    if (query.status) where.status = query.status;
    if (query.contactId) where.contactId = query.contactId;
    if (query.dealId) where.dealId = query.dealId;

    const [data, total] = await this.prisma.$transaction([
      this.prisma.invoice.findMany({
        where,
        skip: (page - 1) * limit,
        take: limit,
        orderBy: { createdAt: 'desc' },
      }),
      this.prisma.invoice.count({ where }),
    ]);

    return { data, meta: { total, page, limit } };
  }

  async findOne(companyId: string, id: string) {
    const invoice = await this.prisma.invoice.findFirst({
      where: { id, companyId },
      include: INVOICE_INCLUDE,
    });
    if (!invoice) {
      throw new NotFoundException('Invoice ёфт нашуд');
    }
    return invoice;
  }

  async create(companyId: string, dto: CreateInvoiceDto) {
    await this.assertContactInCompany(companyId, dto.contactId);
    if (dto.dealId) {
      await this.assertDealInCompany(companyId, dto.dealId);
    }

    for (let attempt = 0; attempt < 5; attempt++) {
      const invoiceNumber = await this.nextInvoiceNumber(companyId);
      try {
        return await this.prisma.invoice.create({
          data: {
            companyId,
            dealId: dto.dealId,
            contactId: dto.contactId,
            invoiceNumber,
            amount: dto.amount,
            currency: dto.currency ?? 'TJS',
            status: InvoiceStatus.DRAFT,
            dueDate: new Date(dto.dueDate),
            notes: dto.notes,
          },
          include: INVOICE_INCLUDE,
        });
      } catch (error) {
        if (error instanceof Prisma.PrismaClientKnownRequestError && error.code === 'P2002') {
          continue;
        }
        throw error;
      }
    }
    throw new ConflictException('Рақами invoice-ро тавлид кардан имконнопазир аст, дубора кӯшиш кунед');
  }

  async update(companyId: string, id: string, dto: UpdateInvoiceDto) {
    await this.ensureExists(companyId, id);
    if (dto.contactId) {
      await this.assertContactInCompany(companyId, dto.contactId);
    }
    if (dto.dealId) {
      await this.assertDealInCompany(companyId, dto.dealId);
    }

    const { dueDate, ...rest } = dto;

    return this.prisma.invoice.update({
      where: { id },
      data: {
        ...rest,
        ...(dueDate ? { dueDate: new Date(dueDate) } : {}),
      },
      include: INVOICE_INCLUDE,
    });
  }

  async updateStatus(companyId: string, id: string, status: InvoiceStatus) {
    await this.ensureExists(companyId, id);
    return this.prisma.invoice.update({ where: { id }, data: { status }, include: INVOICE_INCLUDE });
  }

  async remove(companyId: string, id: string) {
    const invoice = await this.ensureExists(companyId, id);
    if (invoice.status !== InvoiceStatus.DRAFT) {
      throw new ConflictException('Танҳо invoice-ҳои дар ҳолати DRAFT нест карда мешаванд');
    }
    await this.prisma.invoice.delete({ where: { id } });
    return { success: true };
  }

  async listPayments(companyId: string, invoiceId: string) {
    await this.ensureExists(companyId, invoiceId);
    return this.prisma.payment.findMany({ where: { invoiceId }, orderBy: { paidAt: 'desc' } });
  }

  /** Пас аз сабти пардохт, агар маблағи умумии пардохтшуда >= amount бошад, invoice ба таври худкор PAID мешавад. */
  async addPayment(companyId: string, invoiceId: string, dto: CreatePaymentDto) {
    const invoice = await this.ensureExists(companyId, invoiceId);

    const payment = await this.prisma.payment.create({
      data: {
        invoiceId,
        amount: dto.amount,
        method: dto.method,
        paidAt: dto.paidAt ? new Date(dto.paidAt) : new Date(),
        notes: dto.notes,
      },
    });

    const paidSum = await this.prisma.payment.aggregate({
      where: { invoiceId },
      _sum: { amount: true },
    });
    const totalPaid = Number(paidSum._sum.amount ?? 0);

    if (totalPaid >= Number(invoice.amount) && invoice.status !== InvoiceStatus.PAID) {
      await this.prisma.invoice.update({ where: { id: invoiceId }, data: { status: InvoiceStatus.PAID } });
    }

    return payment;
  }

  private async nextInvoiceNumber(companyId: string): Promise<string> {
    const count = await this.prisma.invoice.count({ where: { companyId } });
    return `INV-${String(count + 1).padStart(4, '0')}`;
  }

  private async ensureExists(companyId: string, id: string) {
    const invoice = await this.prisma.invoice.findFirst({ where: { id, companyId } });
    if (!invoice) {
      throw new NotFoundException('Invoice ёфт нашуд');
    }
    return invoice;
  }

  private async assertContactInCompany(companyId: string, contactId: string) {
    const contact = await this.prisma.contact.findFirst({ where: { id: contactId, companyId } });
    if (!contact) {
      throw new BadRequestException('contactId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }

  private async assertDealInCompany(companyId: string, dealId: string) {
    const deal = await this.prisma.deal.findFirst({ where: { id: dealId, companyId } });
    if (!deal) {
      throw new BadRequestException('dealId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }
}
