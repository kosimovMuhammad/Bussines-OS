import { Processor, WorkerHost } from '@nestjs/bullmq';
import { Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { AiJobStatus, Prisma, QueryIntent } from '@prisma/client';
import { GoogleGenAI } from '@google/genai';
import type { Job } from 'bullmq';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';
import { NotificationsService } from '../notifications/notifications.service';
import { NOTIFICATION_EVENTS } from '../notifications/notifications.constants';
import { runRevenueByPeriod, runStaleContacts, runTopSalesperson } from './ai-query.intents';

const MODEL = 'gemini-flash-latest';

export const UNCLASSIFIED_MESSAGE =
  'Ман ин саволро дарк карда натавонистам. Лутфан аниқтар нависед (масалан дар бораи муштариёни бетараф, беҳтарин фурӯшанда ё даромад).';

export interface AiQueryJobData {
  aiQueryResultId: string;
  companyId: string;
  question: string;
}

interface ClassifiedQuery {
  intent?: string;
  days?: number;
  months?: number;
}

function extractJsonPayload(raw: string): string {
  const fenced = raw.match(/```(?:json)?\s*([\s\S]*?)```/i);
  return (fenced ? fenced[1] : raw).trim();
}

@Processor(QUEUES.AI_QUERY)
export class AiQueryProcessor extends WorkerHost {
  private readonly logger = new Logger(AiQueryProcessor.name);
  private readonly gemini: GoogleGenAI | null;

  constructor(
    private readonly prisma: PrismaService,
    private readonly config: ConfigService,
    private readonly notifications: NotificationsService,
  ) {
    super();
    const apiKey = this.config.get<string>('gemini.apiKey');
    this.gemini = apiKey ? new GoogleGenAI({ apiKey }) : null;
  }

  async process(job: Job<AiQueryJobData>) {
    const { aiQueryResultId, companyId, question } = job.data;

    if (!this.gemini) {
      return this.fail(aiQueryResultId, companyId, 'GEMINI_API_KEY танзим нашудааст');
    }

    let classified: ClassifiedQuery;
    try {
      classified = await this.classify(question);
    } catch {
      return this.fail(aiQueryResultId, companyId, UNCLASSIFIED_MESSAGE);
    }

    const intent = this.normalizeIntent(classified.intent);
    if (!intent) {
      return this.fail(aiQueryResultId, companyId, UNCLASSIFIED_MESSAGE);
    }

    const rows = await this.runIntent(intent, companyId, classified);
    const answer = await this.summarize(question, intent, rows);

    const result = await this.prisma.aiQueryResult.update({
      where: { id: aiQueryResultId },
      data: {
        intent,
        status: AiJobStatus.COMPLETED,
        resultRows: rows as unknown as Prisma.InputJsonValue,
        answer,
        completedAt: new Date(),
      },
    });

    this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.AI_QUERY_READY, result);
    return result;
  }

  private normalizeIntent(raw: string | undefined): QueryIntent | null {
    const normalized = raw?.trim().toUpperCase();
    return normalized && normalized in QueryIntent ? (normalized as QueryIntent) : null;
  }

  private async runIntent(intent: QueryIntent, companyId: string, classified: ClassifiedQuery) {
    switch (intent) {
      case QueryIntent.STALE_CONTACTS:
        return runStaleContacts(this.prisma, companyId, classified.days ?? 14);
      case QueryIntent.TOP_SALESPERSON:
        return runTopSalesperson(this.prisma, companyId);
      case QueryIntent.REVENUE_BY_PERIOD:
        return runRevenueByPeriod(this.prisma, companyId, classified.months ?? 1);
    }
  }

  private async classify(question: string): Promise<ClassifiedQuery> {
    const response = await this.gemini!.models.generateContent({
      model: MODEL,
      contents: [
        'Classify the following CRM question into exactly one of these intents:',
        '- STALE_CONTACTS: about contacts/clients who have not replied/been contacted recently',
        '- TOP_SALESPERSON: about which salesperson/deal owner has the most won deals/revenue',
        '- REVENUE_BY_PERIOD: about total revenue/income over a period of time',
        '- UNKNOWN: if the question does not clearly match any of the above',
        '',
        'Return ONLY a JSON object with fields:',
        '- intent: one of "STALE_CONTACTS", "TOP_SALESPERSON", "REVENUE_BY_PERIOD", "UNKNOWN"',
        '- days: number of days of inactivity mentioned for STALE_CONTACTS (default 14 if not mentioned)',
        '- months: number of months mentioned for REVENUE_BY_PERIOD (default 1 if not mentioned)',
        '',
        `Question: "${question}"`,
      ].join('\n'),
      config: {
        systemInstruction:
          'You are a strict intent classifier for a CRM. Respond with ONLY valid JSON, no markdown fences, no extra text.',
        maxOutputTokens: 512,
        responseMimeType: 'application/json',
      },
    });

    const parsed = JSON.parse(extractJsonPayload(response.text ?? '{}'));
    return { intent: parsed.intent, days: parsed.days, months: parsed.months };
  }

  private async summarize(question: string, intent: QueryIntent, rows: unknown): Promise<string> {
    try {
      const response = await this.gemini!.models.generateContent({
        model: MODEL,
        contents: [
          `Question: "${question}"`,
          `Intent: ${intent}`,
          `Result data (JSON): ${JSON.stringify(rows).slice(0, 4000)}`,
          '',
          'Write ONE short, natural-language sentence answering the question using only the data above. No markdown.',
        ].join('\n'),
        config: { maxOutputTokens: 256 },
      });
      return (response.text ?? '').trim() || 'Натиҷа тайёр аст.';
    } catch {
      return 'Натиҷа тайёр аст, аммо хулосаи AI имконнопазир шуд.';
    }
  }

  private async fail(id: string, companyId: string, message: string) {
    const result = await this.prisma.aiQueryResult.update({
      where: { id },
      data: { status: AiJobStatus.FAILED, errorMessage: message, completedAt: new Date() },
    });
    this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.AI_QUERY_READY, result);
    return result;
  }
}
