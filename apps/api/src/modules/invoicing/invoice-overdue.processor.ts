import { InjectQueue, Processor, WorkerHost } from '@nestjs/bullmq';
import { Logger, OnModuleInit } from '@nestjs/common';
import { InvoiceStatus } from '@prisma/client';
import type { Job, Queue } from 'bullmq';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';

const OVERDUE_CHECK_JOB = 'check-overdue';
const OVERDUE_CRON = '0 1 * * *';
const OVERDUE_REPEAT_JOB_ID = 'invoice-overdue-daily';

@Processor(QUEUES.INVOICE_OVERDUE_CHECK)
export class InvoiceOverdueProcessor extends WorkerHost implements OnModuleInit {
  private readonly logger = new Logger(InvoiceOverdueProcessor.name);

  constructor(
    private readonly prisma: PrismaService,
    @InjectQueue(QUEUES.INVOICE_OVERDUE_CHECK) private readonly queue: Queue,
  ) {
    super();
  }

  async onModuleInit() {
    try {
      await this.queue.add(
        OVERDUE_CHECK_JOB,
        {},
        { repeat: { pattern: OVERDUE_CRON }, jobId: OVERDUE_REPEAT_JOB_ID },
      );
    } catch (error) {
      this.logger.error(`Сабти repeatable job-и invoice-overdue-check ноком шуд: ${error}`);
    }
  }

  async process(_job: Job) {
    const { count } = await this.prisma.invoice.updateMany({
      where: { status: InvoiceStatus.SENT, dueDate: { lt: new Date() } },
      data: { status: InvoiceStatus.OVERDUE },
    });
    if (count > 0) {
      this.logger.log(`${count} invoice(s) ба OVERDUE гузаронида шуданд`);
    }
    return { flipped: count };
  }
}
