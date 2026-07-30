import { Processor, WorkerHost } from '@nestjs/bullmq';
import { Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { Prisma, Sentiment } from '@prisma/client';
import Anthropic from '@anthropic-ai/sdk';
import type { Job } from 'bullmq';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';
import { NotificationsService } from '../notifications/notifications.service';
import { NOTIFICATION_EVENTS } from '../notifications/notifications.constants';

const MODEL = 'claude-sonnet-4-6';

export interface AiSummarizeJobData {
  communicationId: string;
}

interface ParsedAiSummary {
  summary?: string;
  customer_sentiment?: string;
  important_dates?: string[];
  tasks?: string[];
  next_follow_up?: string | null;
  deal_stage?: string | null;
}

function extractJsonPayload(raw: string): string {
  const fenced = raw.match(/```(?:json)?\s*([\s\S]*?)```/i);
  return (fenced ? fenced[1] : raw).trim();
}

function mapSentiment(value: string | undefined): Sentiment | null {
  if (!value) return null;
  const normalized = value.trim().toUpperCase();
  return normalized in Sentiment ? (normalized as Sentiment) : null;
}

@Processor(QUEUES.AI_SUMMARIZE)
export class AiSummaryProcessor extends WorkerHost {
  private readonly logger = new Logger(AiSummaryProcessor.name);
  private readonly anthropic: Anthropic | null;

  constructor(
    private readonly prisma: PrismaService,
    private readonly config: ConfigService,
    private readonly notifications: NotificationsService,
  ) {
    super();
    const apiKey = this.config.get<string>('anthropic.apiKey');
    this.anthropic = apiKey ? new Anthropic({ apiKey }) : null;
  }

  async process(job: Job<AiSummarizeJobData>) {
    if (!this.anthropic) {
      throw new Error('ANTHROPIC_API_KEY танзим нашудааст — AI summarization имконнопазир аст');
    }

    const { communicationId } = job.data;

    const communication = await this.prisma.communication.findUnique({ where: { id: communicationId } });
    if (!communication) {
      this.logger.warn(`Communication ${communicationId} ёфт нашуд, job партофта шуд`);
      return;
    }

    const thread = await this.prisma.communication.findMany({
      where: { companyId: communication.companyId, contactId: communication.contactId },
      orderBy: { timestamp: 'asc' },
    });

    const transcript = thread
      .map((message) => `[${message.timestamp.toISOString()}] ${message.direction} (${message.channel}): ${message.content}`)
      .join('\n');

    const response = await this.anthropic.messages.create({
      model: MODEL,
      max_tokens: 1024,
      system:
        'You are a CRM assistant that analyzes customer communication threads. Respond with ONLY valid JSON, no markdown fences, no extra text.',
      messages: [
        {
          role: 'user',
          content: [
            'Analyze the following communication thread and return ONLY a JSON object with exactly these fields:',
            '- summary: string, a concise summary of the conversation',
            '- customer_sentiment: one of "positive", "neutral", "negative"',
            '- important_dates: array of strings, any dates/deadlines mentioned',
            '- tasks: array of strings, action items or follow-up tasks',
            '- next_follow_up: ISO 8601 date string or null, when to follow up next',
            '- deal_stage: one of "lead", "qualified", "proposal", "negotiation", "won", "lost", or null if unclear',
            '',
            'Thread:',
            transcript,
          ].join('\n'),
        },
      ],
    });

    const rawText = response.content
      .filter((block): block is Anthropic.TextBlock => block.type === 'text')
      .map((block) => block.text)
      .join('\n');

    let parsed: ParsedAiSummary;
    try {
      parsed = JSON.parse(extractJsonPayload(rawText));
    } catch {
      this.logger.error(`AI барои ${communicationId} JSON-и нодуруст баргардонд: ${rawText}`);
      throw new Error('AI аз Anthropic JSON-и нодуруст баргардонд');
    }

    const actionItems: Prisma.InputJsonValue = {
      tasks: parsed.tasks ?? [],
      importantDates: parsed.important_dates ?? [],
      dealStage: parsed.deal_stage ?? null,
    };

    const summaryRecord = await this.prisma.aiSummary.upsert({
      where: { communicationId },
      create: {
        communicationId,
        summary: parsed.summary ?? '',
        sentiment: mapSentiment(parsed.customer_sentiment),
        actionItems,
        nextFollowUp: parsed.next_follow_up ? new Date(parsed.next_follow_up) : null,
      },
      update: {
        summary: parsed.summary ?? '',
        sentiment: mapSentiment(parsed.customer_sentiment),
        actionItems,
        nextFollowUp: parsed.next_follow_up ? new Date(parsed.next_follow_up) : null,
        generatedAt: new Date(),
      },
    });

    this.notifications.emitToCompany(communication.companyId, NOTIFICATION_EVENTS.AI_SUMMARY_READY, summaryRecord);

    return summaryRecord;
  }
}
