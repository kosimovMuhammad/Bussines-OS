import { AiJobStatus, QueryIntent } from '@prisma/client';
import type { Job } from 'bullmq';
import type { ConfigService } from '@nestjs/config';
import { AiQueryProcessor, UNCLASSIFIED_MESSAGE } from './ai-query.processor';
import type { PrismaService } from '../../prisma/prisma.service';
import type { NotificationsService } from '../notifications/notifications.service';

function makeDeps(apiKey: string | undefined) {
  const prisma = {
    aiQueryResult: {
      update: jest.fn((args: { where: { id: string }; data: unknown }) =>
        Promise.resolve({ id: args.where.id, ...(args.data as object) }),
      ),
    },
    contact: { findMany: jest.fn().mockResolvedValue([]) },
    deal: { groupBy: jest.fn().mockResolvedValue([]), findMany: jest.fn().mockResolvedValue([]) },
    user: { findMany: jest.fn().mockResolvedValue([]) },
  } as unknown as PrismaService;
  const config = { get: jest.fn().mockReturnValue(apiKey) } as unknown as ConfigService;
  const notifications = { emitToCompany: jest.fn() } as unknown as NotificationsService;
  return { prisma, config, notifications };
}

describe('AiQueryProcessor', () => {
  it('fails with a clear message when GEMINI_API_KEY is not configured', async () => {
    const { prisma, config, notifications } = makeDeps(undefined);
    const processor = new AiQueryProcessor(prisma, config, notifications);

    await processor.process({ data: { aiQueryResultId: 'q1', companyId: 'c1', question: 'x' } } as Job);

    expect(prisma.aiQueryResult.update).toHaveBeenCalledWith({
      where: { id: 'q1' },
      data: expect.objectContaining({ status: AiJobStatus.FAILED }),
    });
  });

  it('returns the couldn\'t-understand fallback when the LLM classifies as UNKNOWN', async () => {
    const { prisma, config, notifications } = makeDeps('fake-key');
    const processor = new AiQueryProcessor(prisma, config, notifications);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (processor as any).gemini.models.generateContent = jest.fn().mockResolvedValue({ text: '{"intent":"UNKNOWN"}' });

    await processor.process({ data: { aiQueryResultId: 'q2', companyId: 'c1', question: 'what is the weather' } } as Job);

    expect(prisma.aiQueryResult.update).toHaveBeenCalledWith({
      where: { id: 'q2' },
      data: expect.objectContaining({ status: AiJobStatus.FAILED, errorMessage: UNCLASSIFIED_MESSAGE }),
    });
  });

  it('fails gracefully (not crash) when the LLM returns unparsable JSON', async () => {
    const { prisma, config, notifications } = makeDeps('fake-key');
    const processor = new AiQueryProcessor(prisma, config, notifications);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (processor as any).gemini.models.generateContent = jest.fn().mockResolvedValue({ text: 'not json at all' });

    await processor.process({ data: { aiQueryResultId: 'q3', companyId: 'c1', question: 'garbage' } } as Job);

    expect(prisma.aiQueryResult.update).toHaveBeenCalledWith({
      where: { id: 'q3' },
      data: expect.objectContaining({ status: AiJobStatus.FAILED, errorMessage: UNCLASSIFIED_MESSAGE }),
    });
  });

  it('runs the matching safe query and completes when the question classifies cleanly', async () => {
    const { prisma, config, notifications } = makeDeps('fake-key');
    const processor = new AiQueryProcessor(prisma, config, notifications);
    const generateContent = jest
      .fn()
      .mockResolvedValueOnce({ text: '{"intent":"STALE_CONTACTS","days":30}' })
      .mockResolvedValueOnce({ text: 'Se muştariyon rūzho ovoz nadodaand.' });
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (processor as any).gemini.models.generateContent = generateContent;
    (prisma.contact.findMany as jest.Mock).mockResolvedValue([{ id: 'contact-1' }]);

    await processor.process({ data: { aiQueryResultId: 'q4', companyId: 'c1', question: 'stale contacts?' } } as Job);

    expect(prisma.contact.findMany).toHaveBeenCalled();
    expect(prisma.aiQueryResult.update).toHaveBeenCalledWith({
      where: { id: 'q4' },
      data: expect.objectContaining({ status: AiJobStatus.COMPLETED, intent: QueryIntent.STALE_CONTACTS }),
    });
  });
});
