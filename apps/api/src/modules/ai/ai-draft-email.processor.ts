import { Processor, WorkerHost } from '@nestjs/bullmq';
import { Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { AiJobStatus } from '@prisma/client';
import { GoogleGenAI } from '@google/genai';
import type { Job } from 'bullmq';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';
import { NotificationsService } from '../notifications/notifications.service';
import { NOTIFICATION_EVENTS } from '../notifications/notifications.constants';

const MODEL = 'gemini-flash-latest';

export interface AiDraftEmailJobData {
  aiEmailDraftId: string;
  companyId: string;
  contactId: string;
  context?: string;
}

interface ParsedDraft {
  subject?: string;
  body?: string;
}

function extractJsonPayload(raw: string): string {
  const fenced = raw.match(/```(?:json)?\s*([\s\S]*?)```/i);
  return (fenced ? fenced[1] : raw).trim();
}

@Processor(QUEUES.AI_DRAFT_EMAIL)
export class AiDraftEmailProcessor extends WorkerHost {
  private readonly logger = new Logger(AiDraftEmailProcessor.name);
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

  async process(job: Job<AiDraftEmailJobData>) {
    const { aiEmailDraftId, companyId, contactId, context } = job.data;

    if (!this.gemini) {
      return this.fail(aiEmailDraftId, companyId, 'GEMINI_API_KEY танзим нашудааст');
    }

    const contact = await this.prisma.contact.findFirst({ where: { id: contactId, companyId } });
    if (!contact) {
      this.logger.warn(`Contact ${contactId} ёфт нашуд, job партофта шуд`);
      return this.fail(aiEmailDraftId, companyId, 'Contact ёфт нашуд');
    }

    const communications = await this.prisma.communication.findMany({
      where: { companyId, contactId },
      orderBy: { timestamp: 'desc' },
      take: 20,
    });

    const timeline = communications
      .slice()
      .reverse()
      .map((c) => `[${c.timestamp.toISOString()}] ${c.direction} (${c.channel}): ${c.content}`)
      .join('\n');

    try {
      const parsed = await this.draft(contact, context, timeline);
      const draft = await this.prisma.aiEmailDraft.update({
        where: { id: aiEmailDraftId },
        data: {
          status: AiJobStatus.COMPLETED,
          subject: parsed.subject ?? '',
          body: parsed.body ?? '',
          completedAt: new Date(),
        },
      });
      this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.AI_DRAFT_EMAIL_READY, draft);
      return draft;
    } catch {
      return this.fail(aiEmailDraftId, companyId, 'AI паёмро сохта натавонист');
    }
  }

  private async draft(
    contact: { firstName: string; lastName: string | null },
    context: string | undefined,
    timeline: string,
  ): Promise<ParsedDraft> {
    const response = await this.gemini!.models.generateContent({
      model: MODEL,
      contents: [
        `Contact: ${contact.firstName} ${contact.lastName ?? ''}`.trim(),
        context ? `Context/instructions: ${context}` : '',
        '',
        'Recent communication timeline:',
        timeline || '(no prior communications)',
        '',
        'Draft a short, professional follow-up email to this contact. Return ONLY a JSON object with fields "subject" and "body" (plain text body). No markdown fences.',
      ]
        .filter(Boolean)
        .join('\n'),
      config: {
        systemInstruction:
          'You are a CRM assistant drafting professional emails on behalf of a sales rep. Respond with ONLY valid JSON.',
        maxOutputTokens: 1024,
        responseMimeType: 'application/json',
      },
    });

    return JSON.parse(extractJsonPayload(response.text ?? '{}'));
  }

  private async fail(id: string, companyId: string, message: string) {
    const draft = await this.prisma.aiEmailDraft.update({
      where: { id },
      data: { status: AiJobStatus.FAILED, errorMessage: message, completedAt: new Date() },
    });
    this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.AI_DRAFT_EMAIL_READY, draft);
    return draft;
  }
}
