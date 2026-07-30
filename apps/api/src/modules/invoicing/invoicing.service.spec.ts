import { Test } from '@nestjs/testing';
import { ConflictException } from '@nestjs/common';
import { InvoiceStatus, PaymentMethod } from '@prisma/client';
import { InvoicingService } from './invoicing.service';
import { PrismaService } from '../../prisma/prisma.service';

describe('InvoicingService', () => {
  let service: InvoicingService;
  let prisma: {
    invoice: { findFirst: jest.Mock; update: jest.Mock; delete: jest.Mock };
    payment: { create: jest.Mock; aggregate: jest.Mock };
  };

  beforeEach(async () => {
    prisma = {
      invoice: {
        findFirst: jest.fn(),
        update: jest.fn(),
        delete: jest.fn(),
      },
      payment: {
        create: jest.fn(),
        aggregate: jest.fn(),
      },
    };

    const moduleRef = await Test.createTestingModule({
      providers: [InvoicingService, { provide: PrismaService, useValue: prisma }],
    }).compile();

    service = moduleRef.get(InvoicingService);
  });

  describe('addPayment', () => {
    const companyId = 'company-1';
    const invoiceId = 'invoice-1';

    it('marks the invoice PAID once payments cover the full amount', async () => {
      prisma.invoice.findFirst.mockResolvedValue({ id: invoiceId, amount: 1000, status: InvoiceStatus.SENT });
      prisma.payment.create.mockResolvedValue({ id: 'payment-1', amount: 1000 });
      prisma.payment.aggregate.mockResolvedValue({ _sum: { amount: 1000 } });

      await service.addPayment(companyId, invoiceId, { amount: 1000, method: PaymentMethod.BANK_TRANSFER });

      expect(prisma.invoice.update).toHaveBeenCalledWith({
        where: { id: invoiceId },
        data: { status: InvoiceStatus.PAID },
      });
    });

    it('leaves the invoice status untouched when only partially paid', async () => {
      prisma.invoice.findFirst.mockResolvedValue({ id: invoiceId, amount: 1000, status: InvoiceStatus.SENT });
      prisma.payment.create.mockResolvedValue({ id: 'payment-1', amount: 400 });
      prisma.payment.aggregate.mockResolvedValue({ _sum: { amount: 400 } });

      await service.addPayment(companyId, invoiceId, { amount: 400, method: PaymentMethod.CASH });

      expect(prisma.invoice.update).not.toHaveBeenCalled();
    });

    it('does not redundantly update an invoice that is already PAID', async () => {
      prisma.invoice.findFirst.mockResolvedValue({ id: invoiceId, amount: 1000, status: InvoiceStatus.PAID });
      prisma.payment.create.mockResolvedValue({ id: 'payment-2', amount: 100 });
      prisma.payment.aggregate.mockResolvedValue({ _sum: { amount: 1100 } });

      await service.addPayment(companyId, invoiceId, { amount: 100, method: PaymentMethod.CASH });

      expect(prisma.invoice.update).not.toHaveBeenCalled();
    });
  });

  describe('remove', () => {
    it('throws a 409 ConflictException when the invoice is not DRAFT', async () => {
      prisma.invoice.findFirst.mockResolvedValue({ id: 'invoice-1', status: InvoiceStatus.SENT });

      await expect(service.remove('company-1', 'invoice-1')).rejects.toThrow(ConflictException);
      expect(prisma.invoice.delete).not.toHaveBeenCalled();
    });

    it('deletes the invoice when it is DRAFT', async () => {
      prisma.invoice.findFirst.mockResolvedValue({ id: 'invoice-1', status: InvoiceStatus.DRAFT });

      const result = await service.remove('company-1', 'invoice-1');

      expect(prisma.invoice.delete).toHaveBeenCalledWith({ where: { id: 'invoice-1' } });
      expect(result).toEqual({ success: true });
    });
  });
});
