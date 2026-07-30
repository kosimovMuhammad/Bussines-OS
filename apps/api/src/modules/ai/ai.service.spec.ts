import { NotFoundException, UnprocessableEntityException } from '@nestjs/common';
import { AiJobStatus } from '@prisma/client';
import type { ConfigService } from '@nestjs/config';
import type { Queue } from 'bullmq';
import { AiService } from './ai.service';
import type { PrismaService } from '../../prisma/prisma.service';
import type { S3Service } from '../../common/s3/s3.service';

function makeService(prismaOverrides: Record<string, unknown>) {
  const prisma = {
    aiQueryResult: { findFirst: jest.fn(), create: jest.fn() },
    ...prismaOverrides,
  } as unknown as PrismaService;
  const config = { get: jest.fn() } as unknown as ConfigService;
  const s3 = {} as unknown as S3Service;
  const queue = { add: jest.fn() } as unknown as Queue;
  const service = new AiService(prisma, config, s3, queue, queue, queue, queue);
  return { service, prisma };
}

describe('AiService.getQueryResult', () => {
  it('throws 404 when the query result does not exist for this company', async () => {
    const { service, prisma } = makeService({});
    (prisma.aiQueryResult.findFirst as jest.Mock).mockResolvedValue(null);

    await expect(service.getQueryResult('company-1', 'missing-id')).rejects.toThrow(NotFoundException);
  });

  it('throws a 422 with the stored fallback message when classification failed', async () => {
    const { service, prisma } = makeService({});
    (prisma.aiQueryResult.findFirst as jest.Mock).mockResolvedValue({
      id: 'q1',
      status: AiJobStatus.FAILED,
      errorMessage: 'Ман ин саволро дарк карда натавонистам.',
    });

    await expect(service.getQueryResult('company-1', 'q1')).rejects.toThrow(UnprocessableEntityException);
  });

  it('returns the record when the query completed successfully', async () => {
    const { service, prisma } = makeService({});
    const record = { id: 'q1', status: AiJobStatus.COMPLETED, answer: 'done' };
    (prisma.aiQueryResult.findFirst as jest.Mock).mockResolvedValue(record);

    await expect(service.getQueryResult('company-1', 'q1')).resolves.toEqual(record);
  });
});
