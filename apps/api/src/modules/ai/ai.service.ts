import { Injectable, InternalServerErrorException, NotFoundException } from '@nestjs/common';
import { InjectQueue } from '@nestjs/bullmq';
import { ConfigService } from '@nestjs/config';
import type { Queue } from 'bullmq';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';
import { SummarizeDto } from './dto/summarize.dto';
import type { AiSummarizeJobData } from './ai-summary.processor';

@Injectable()
export class AiService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly config: ConfigService,
    @InjectQueue(QUEUES.AI_SUMMARIZE) private readonly queue: Queue<AiSummarizeJobData>,
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
}
