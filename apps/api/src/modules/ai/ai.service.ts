import {
  Injectable,
  InternalServerErrorException,
  NotFoundException,
  UnprocessableEntityException,
} from '@nestjs/common';
import { InjectQueue } from '@nestjs/bullmq';
import { ConfigService } from '@nestjs/config';
import { AiJobStatus, Prisma } from '@prisma/client';
import type { Queue } from 'bullmq';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';
import { S3Service } from '../../common/s3/s3.service';
import { SummarizeDto } from './dto/summarize.dto';
import type { AiSummarizeJobData } from './ai-summary.processor';
import { AiQueryDto } from './dto/ai-query.dto';
import { AiDraftEmailDto } from './dto/ai-draft-email.dto';
import { AiGenerateQuotationDto } from './dto/ai-generate-quotation.dto';
import type { AiQueryJobData } from './ai-query.processor';
import type { AiDraftEmailJobData } from './ai-draft-email.processor';
import type { AiQuotationJobData } from './ai-quotation.processor';

@Injectable()
export class AiService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly config: ConfigService,
    private readonly s3: S3Service,
    @InjectQueue(QUEUES.AI_SUMMARIZE) private readonly queue: Queue<AiSummarizeJobData>,
    @InjectQueue(QUEUES.AI_QUERY) private readonly queryQueue: Queue<AiQueryJobData>,
    @InjectQueue(QUEUES.AI_DRAFT_EMAIL) private readonly draftEmailQueue: Queue<AiDraftEmailJobData>,
    @InjectQueue(QUEUES.AI_QUOTATION) private readonly quotationQueue: Queue<AiQuotationJobData>,
  ) {}

  async enqueueSummarize(companyId: string, dto: SummarizeDto) {
    if (!this.config.get<string>('gemini.apiKey')) {
      throw new InternalServerErrorException(
        'GEMINI_API_KEY танзим нашудааст — AI summarization имконнопазир аст',
      );
    }

    const communication = await this.prisma.communication.findFirst({
      where: { id: dto.communicationId, companyId },
    });
    if (!communication) {
      throw new NotFoundException('communicationId нодуруст аст ё ба ин company тааллуқ надорад');
    }

    await this.queue.add('summarize', { communicationId: dto.communicationId });
    return { queued: true, communicationId: dto.communicationId };
  }

  async getSummary(companyId: string, id: string) {
    const summary = await this.prisma.aiSummary.findFirst({
      where: { OR: [{ id }, { communicationId: id }], communication: { companyId } },
    });
    if (!summary) {
      throw new NotFoundException('AI summary ёфт нашуд');
    }
    return summary;
  }

  async enqueueQuery(companyId: string, dto: AiQueryDto) {
    if (!this.config.get<string>('gemini.apiKey')) {
      throw new InternalServerErrorException('GEMINI_API_KEY танзим нашудааст — AI query имконнопазир аст');
    }

    const record = await this.prisma.aiQueryResult.create({
      data: { companyId, question: dto.question, status: AiJobStatus.PENDING },
    });

    await this.queryQueue.add('query', { aiQueryResultId: record.id, companyId, question: dto.question });
    return { queued: true, id: record.id };
  }

  async getQueryResult(companyId: string, id: string) {
    const record = await this.prisma.aiQueryResult.findFirst({ where: { id, companyId } });
    if (!record) {
      throw new NotFoundException('Натиҷаи AI query ёфт нашуд');
    }
    if (record.status === AiJobStatus.FAILED) {
      throw new UnprocessableEntityException(record.errorMessage ?? 'Ин саволро дарк карда натавонист');
    }
    return record;
  }

  async enqueueDraftEmail(companyId: string, dto: AiDraftEmailDto) {
    if (!this.config.get<string>('gemini.apiKey')) {
      throw new InternalServerErrorException('GEMINI_API_KEY танзим нашудааст — AI draft email имконнопазир аст');
    }

    const contact = await this.prisma.contact.findFirst({ where: { id: dto.contactId, companyId } });
    if (!contact) {
      throw new NotFoundException('contactId нодуруст аст ё ба ин company тааллуқ надорад');
    }

    const record = await this.prisma.aiEmailDraft.create({
      data: { companyId, contactId: dto.contactId, context: dto.context, status: AiJobStatus.PENDING },
    });

    await this.draftEmailQueue.add('draft-email', {
      aiEmailDraftId: record.id,
      companyId,
      contactId: dto.contactId,
      context: dto.context,
    });
    return { queued: true, id: record.id };
  }

  async getDraftEmail(companyId: string, id: string) {
    const record = await this.prisma.aiEmailDraft.findFirst({ where: { id, companyId } });
    if (!record) {
      throw new NotFoundException('Draft ёфт нашуд');
    }
    if (record.status === AiJobStatus.FAILED) {
      throw new UnprocessableEntityException(record.errorMessage ?? 'AI паёмро сохта натавонист');
    }
    return record;
  }

  async enqueueQuotation(companyId: string, userId: string, dto: AiGenerateQuotationDto) {
    const deal = await this.prisma.deal.findFirst({ where: { id: dto.dealId, companyId } });
    if (!deal) {
      throw new NotFoundException('dealId нодуруст аст ё ба ин company тааллуқ надорад');
    }

    const record = await this.prisma.aiQuotation.create({
      data: {
        companyId,
        dealId: dto.dealId,
        items: dto.items as unknown as Prisma.InputJsonValue,
        status: AiJobStatus.PENDING,
      },
    });

    await this.quotationQueue.add('quotation', {
      aiQuotationId: record.id,
      companyId,
      userId,
      dealId: dto.dealId,
      items: dto.items,
    });
    return { queued: true, id: record.id };
  }

  async getQuotation(companyId: string, id: string) {
    const record = await this.prisma.aiQuotation.findFirst({ where: { id, companyId }, include: { fileAsset: true } });
    if (!record) {
      throw new NotFoundException('Quotation ёфт нашуд');
    }
    if (record.status === AiJobStatus.FAILED) {
      throw new UnprocessableEntityException(record.errorMessage ?? 'Тавлиди quotation ноком шуд');
    }
    if (record.status === AiJobStatus.COMPLETED && record.fileAsset) {
      const downloadUrl = await this.s3.getPresignedDownloadUrl(record.fileAsset.s3Key);
      return { ...record, downloadUrl };
    }
    return record;
  }
}
