import { Processor, WorkerHost } from '@nestjs/bullmq';
import { Logger } from '@nestjs/common';
import { AiJobStatus } from '@prisma/client';
import type { Job } from 'bullmq';
import PDFDocument from 'pdfkit';
import { PrismaService } from '../../prisma/prisma.service';
import { S3Service } from '../../common/s3/s3.service';
import { FilesService } from '../files/files.service';
import { QUEUES } from '../../queue/queue.constants';
import { NotificationsService } from '../notifications/notifications.service';
import { NOTIFICATION_EVENTS } from '../notifications/notifications.constants';

interface QuotationItem {
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface AiQuotationJobData {
  aiQuotationId: string;
  companyId: string;
  userId: string;
  dealId: string;
  items: QuotationItem[];
}

@Processor(QUEUES.AI_QUOTATION)
export class AiQuotationProcessor extends WorkerHost {
  private readonly logger = new Logger(AiQuotationProcessor.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly s3: S3Service,
    private readonly filesService: FilesService,
    private readonly notifications: NotificationsService,
  ) {
    super();
  }

  async process(job: Job<AiQuotationJobData>) {
    const { aiQuotationId, companyId, userId, dealId, items } = job.data;

    try {
      const deal = await this.prisma.deal.findFirst({ where: { id: dealId, companyId }, include: { contact: true } });
      if (!deal) {
        throw new Error('Deal ёфт нашуд');
      }

      const totalAmount = items.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0);
      const pdfBuffer = await this.renderPdf(deal, items, totalAmount);

      const filename = `quotation-${deal.id}.pdf`;
      const s3Key = this.s3.buildKey(companyId, filename);
      await this.s3.uploadBuffer(s3Key, pdfBuffer, 'application/pdf');

      const fileAsset = await this.filesService.confirm(companyId, userId, {
        s3Key,
        filename,
        mimeType: 'application/pdf',
        sizeBytes: pdfBuffer.length,
        dealId,
      });

      const quotation = await this.prisma.aiQuotation.update({
        where: { id: aiQuotationId },
        data: { status: AiJobStatus.COMPLETED, totalAmount, fileAssetId: fileAsset.id, completedAt: new Date() },
        include: { fileAsset: true },
      });

      this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.AI_QUOTATION_READY, quotation);
      return quotation;
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Тавлиди quotation ноком шуд';
      this.logger.error(`Quotation ${aiQuotationId} ноком шуд: ${message}`);
      const quotation = await this.prisma.aiQuotation.update({
        where: { id: aiQuotationId },
        data: { status: AiJobStatus.FAILED, errorMessage: message, completedAt: new Date() },
      });
      this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.AI_QUOTATION_READY, quotation);
      return quotation;
    }
  }

  private renderPdf(
    deal: { title: string; currency: string; contact: { firstName: string; lastName: string | null } },
    items: QuotationItem[],
    totalAmount: number,
  ): Promise<Buffer> {
    return new Promise((resolve, reject) => {
      const doc = new PDFDocument({ margin: 50 });
      const chunks: Buffer[] = [];
      doc.on('data', (chunk: Buffer) => chunks.push(chunk));
      doc.on('end', () => resolve(Buffer.concat(chunks)));
      doc.on('error', reject);

      doc.fontSize(20).text('Quotation', { align: 'center' }).moveDown();
      doc.fontSize(12).text(`Deal: ${deal.title}`);
      doc.text(`Client: ${deal.contact.firstName} ${deal.contact.lastName ?? ''}`.trim());
      doc.moveDown();

      for (const item of items) {
        doc.text(
          `${item.description}  x${item.quantity}  @ ${item.unitPrice.toFixed(2)} ${deal.currency} = ${(
            item.quantity * item.unitPrice
          ).toFixed(2)} ${deal.currency}`,
        );
      }

      doc.moveDown();
      doc.fontSize(14).text(`Total: ${totalAmount.toFixed(2)} ${deal.currency}`, { align: 'right' });
      doc.end();
    });
  }
}
