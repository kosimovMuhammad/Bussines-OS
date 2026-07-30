import { DealStage, InvoiceStatus } from '@prisma/client';
import type { ConfigService } from '@nestjs/config';
import { AnalyticsService } from './analytics.service';
import type { PrismaService } from '../../prisma/prisma.service';

describe('AnalyticsService.getForecast', () => {
  it('computes a deterministic weighted forecast and falls back to static text without a Gemini key', async () => {
    const prisma = {
      deal: {
        findMany: jest.fn().mockResolvedValue([
          { value: 1000, stage: DealStage.NEGOTIATION, probability: 80 },
          { value: 2000, stage: DealStage.LEAD, probability: 50 },
        ]),
      },
      invoice: {
        findMany: jest.fn().mockResolvedValue([
          { amount: 500, status: InvoiceStatus.SENT, payments: [] },
          { amount: 300, status: InvoiceStatus.OVERDUE, payments: [{ amount: 100 }] },
        ]),
      },
    } as unknown as PrismaService;
    const config = { get: jest.fn().mockReturnValue(undefined) } as unknown as ConfigService;

    const service = new AnalyticsService(prisma, config);
    const result = await service.getForecast('company-1');

    // weightedDealValue = (1000*0.8*0.8) + (2000*0.5*0.1) = 640 + 100 = 740
    expect(result.breakdown.weightedDealValue).toBe(740);
    // weightedInvoiceValue = (500 remaining * 1.0) + ((300-100) remaining * 0.5) = 500 + 100 = 600
    expect(result.breakdown.weightedInvoiceValue).toBe(600);
    expect(result.estimatedRevenue).toBe(1340);
    // confidence = round(740 / 3000 * 100) = 25
    expect(result.confidencePercentage).toBe(25);
    expect(result.summary).toContain('1340');
  });
});
