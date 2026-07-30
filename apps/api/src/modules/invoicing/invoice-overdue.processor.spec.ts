import { InvoiceStatus } from '@prisma/client';
import type { Job, Queue } from 'bullmq';
import { InvoiceOverdueProcessor } from './invoice-overdue.processor';
import type { PrismaService } from '../../prisma/prisma.service';

describe('InvoiceOverdueProcessor', () => {
  it('flips SENT invoices past their due date to OVERDUE', async () => {
    const prisma = { invoice: { updateMany: jest.fn().mockResolvedValue({ count: 3 }) } } as unknown as PrismaService;
    const queue = { add: jest.fn() } as unknown as Queue;
    const processor = new InvoiceOverdueProcessor(prisma, queue);

    const result = await processor.process({} as Job);

    expect(prisma.invoice.updateMany).toHaveBeenCalledWith({
      where: { status: InvoiceStatus.SENT, dueDate: { lt: expect.any(Date) } },
      data: { status: InvoiceStatus.OVERDUE },
    });
    expect(result).toEqual({ flipped: 3 });
  });

  it('registers a daily repeatable job on module init', async () => {
    const prisma = {} as PrismaService;
    const queue = { add: jest.fn() } as unknown as Queue;
    const processor = new InvoiceOverdueProcessor(prisma, queue);

    await processor.onModuleInit();

    expect(queue.add).toHaveBeenCalledWith(
      'check-overdue',
      {},
      { repeat: { pattern: '0 1 * * *' }, jobId: 'invoice-overdue-daily' },
    );
  });
});
