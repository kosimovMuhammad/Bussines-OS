import { Module } from '@nestjs/common';
import { BullModule } from '@nestjs/bullmq';
import { QUEUES } from '../../queue/queue.constants';
import { InvoicingController } from './invoicing.controller';
import { InvoicingService } from './invoicing.service';
import { InvoiceOverdueProcessor } from './invoice-overdue.processor';

@Module({
  imports: [BullModule.registerQueue({ name: QUEUES.INVOICE_OVERDUE_CHECK })],
  controllers: [InvoicingController],
  providers: [InvoicingService, InvoiceOverdueProcessor],
  exports: [InvoicingService],
})
export class InvoicingModule {}
