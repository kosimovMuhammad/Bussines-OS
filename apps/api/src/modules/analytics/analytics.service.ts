import { Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { DealStage, InvoiceStatus, MeetingStatus, Prisma, TaskStatus } from '@prisma/client';
import { GoogleGenAI } from '@google/genai';
import { PrismaService } from '../../prisma/prisma.service';

/** Ин рақамҳо на эҳтимолияти хом, балки суръати мансуб ба stage мебошанд — эҳтимолияти пӯшидан дар 30 рӯзи оянда, новобаста аз probability-и дастӣ. */
const STAGE_FORECAST_MULTIPLIER: Record<DealStage, number> = {
  [DealStage.LEAD]: 0.1,
  [DealStage.QUALIFIED]: 0.25,
  [DealStage.PROPOSAL]: 0.5,
  [DealStage.NEGOTIATION]: 0.8,
  [DealStage.WON]: 0,
  [DealStage.LOST]: 0,
};

const OPEN_STAGES = [DealStage.LEAD, DealStage.QUALIFIED, DealStage.PROPOSAL, DealStage.NEGOTIATION];

@Injectable()
export class AnalyticsService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly config: ConfigService,
  ) {}

  async getOverview(companyId: string) {
    const startOfDay = new Date();
    startOfDay.setHours(0, 0, 0, 0);

    const [contactsCount, openTasksCount, dealsByStage, todayTimeEntries] = await Promise.all([
      this.prisma.contact.count({ where: { companyId } }),
      this.prisma.task.count({
        where: { companyId, status: { in: [TaskStatus.TODO, TaskStatus.IN_PROGRESS] } },
      }),
      this.prisma.deal.groupBy({ by: ['stage'], where: { companyId }, _count: { _all: true } }),
      this.prisma.timeEntry.aggregate({
        where: { companyId, startedAt: { gte: startOfDay } },
        _sum: { durationSec: true },
      }),
    ]);

    return {
      contactsCount,
      openTasksCount,
      dealsByStage: dealsByStage.map((d) => ({ stage: d.stage, count: d._count._all })),
      todayTimeTrackedSec: todayTimeEntries._sum.durationSec ?? 0,
    };
  }

  async getSales(companyId: string) {
    const byStage = await this.prisma.deal.groupBy({
      by: ['stage'],
      where: { companyId },
      _count: { _all: true },
      _sum: { value: true },
    });

    const won = byStage.find((s) => s.stage === DealStage.WON);
    const lost = byStage.find((s) => s.stage === DealStage.LOST);
    const wonCount = won?._count._all ?? 0;
    const lostCount = lost?._count._all ?? 0;
    const closedCount = wonCount + lostCount;

    return {
      byStage: byStage.map((s) => ({ stage: s.stage, count: s._count._all, totalValue: s._sum.value ?? 0 })),
      wonCount,
      wonValue: won?._sum.value ?? 0,
      lostCount,
      winRate: closedCount > 0 ? Math.round((wonCount / closedCount) * 100) : 0,
    };
  }

  async getRevenue(companyId: string) {
    const wonDeals = await this.prisma.deal.findMany({
      where: { companyId, stage: DealStage.WON },
      select: { value: true, updatedAt: true },
    });

    const totalWonValue = wonDeals.reduce((sum, deal) => sum + Number(deal.value), 0);

    const byMonth = new Map<string, number>();
    for (const deal of wonDeals) {
      const key = deal.updatedAt.toISOString().slice(0, 7);
      byMonth.set(key, (byMonth.get(key) ?? 0) + Number(deal.value));
    }

    return {
      totalWonValue,
      byMonth: Array.from(byMonth.entries())
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([month, value]) => ({ month, value })),
    };
  }

  async getTasksStats(companyId: string) {
    const [byStatus, byPriority, overdueCount] = await Promise.all([
      this.prisma.task.groupBy({ by: ['status'], where: { companyId }, _count: { _all: true } }),
      this.prisma.task.groupBy({ by: ['priority'], where: { companyId }, _count: { _all: true } }),
      this.prisma.task.count({
        where: {
          companyId,
          dueDate: { lt: new Date() },
          status: { notIn: [TaskStatus.COMPLETED, TaskStatus.ARCHIVED] },
        },
      }),
    ]);

    return {
      byStatus: byStatus.map((s) => ({ status: s.status, count: s._count._all })),
      byPriority: byPriority.map((p) => ({ priority: p.priority, count: p._count._all })),
      overdueCount,
    };
  }

  async getActivityFeed(companyId: string, limit = 20) {
    const [communications, deals, tasks] = await Promise.all([
      this.prisma.communication.findMany({
        where: { companyId },
        orderBy: { timestamp: 'desc' },
        take: limit,
        select: { id: true, channel: true, direction: true, content: true, timestamp: true, contactId: true },
      }),
      this.prisma.deal.findMany({
        where: { companyId },
        orderBy: { updatedAt: 'desc' },
        take: limit,
        select: { id: true, title: true, stage: true, updatedAt: true },
      }),
      this.prisma.task.findMany({
        where: { companyId },
        orderBy: { updatedAt: 'desc' },
        take: limit,
        select: { id: true, title: true, status: true, updatedAt: true },
      }),
    ]);

    return [
      ...communications.map((c) => ({ type: 'communication' as const, timestamp: c.timestamp, data: c })),
      ...deals.map((d) => ({ type: 'deal' as const, timestamp: d.updatedAt, data: d })),
      ...tasks.map((t) => ({ type: 'task' as const, timestamp: t.updatedAt, data: t })),
    ]
      .sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime())
      .slice(0, limit);
  }

  async getFinance(companyId: string) {
    const now = new Date();
    const startOfMonth = new Date(now.getFullYear(), now.getMonth(), 1);
    const sixMonthsAgo = new Date(now.getFullYear(), now.getMonth() - 5, 1);

    const [outstandingInvoices, overdueInvoices, paidThisMonth, recentPayments] = await Promise.all([
      this.prisma.invoice.findMany({
        where: { companyId, status: { in: [InvoiceStatus.SENT, InvoiceStatus.OVERDUE] } },
        include: { payments: { select: { amount: true } } },
      }),
      this.prisma.invoice.findMany({
        where: { companyId, status: InvoiceStatus.OVERDUE },
        include: { payments: { select: { amount: true } } },
      }),
      this.prisma.payment.aggregate({
        where: { invoice: { companyId }, paidAt: { gte: startOfMonth } },
        _sum: { amount: true },
      }),
      this.prisma.payment.findMany({
        where: { invoice: { companyId }, paidAt: { gte: sixMonthsAgo } },
        select: { amount: true, paidAt: true },
      }),
    ]);

    const remainingOf = (invoice: { amount: Prisma.Decimal; payments: { amount: Prisma.Decimal }[] }) =>
      Number(invoice.amount) - invoice.payments.reduce((sum, p) => sum + Number(p.amount), 0);

    const totalOutstanding = outstandingInvoices.reduce((sum, inv) => sum + Math.max(remainingOf(inv), 0), 0);
    const overdueAmount = overdueInvoices.reduce((sum, inv) => sum + Math.max(remainingOf(inv), 0), 0);

    const byMonth = new Map<string, number>();
    for (const payment of recentPayments) {
      const key = payment.paidAt.toISOString().slice(0, 7);
      byMonth.set(key, (byMonth.get(key) ?? 0) + Number(payment.amount));
    }

    return {
      totalOutstanding,
      totalPaid: Number(paidThisMonth._sum.amount ?? 0),
      overdueCount: overdueInvoices.length,
      overdueAmount,
      revenueByMonth: Array.from(byMonth.entries())
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([month, value]) => ({ month, value })),
    };
  }

  async getUpcomingMeetings(companyId: string, userId: string) {
    const startOfToday = new Date();
    startOfToday.setHours(0, 0, 0, 0);
    const in7Days = new Date(startOfToday);
    in7Days.setDate(in7Days.getDate() + 7);
    in7Days.setHours(23, 59, 59, 999);

    return this.prisma.meeting.findMany({
      where: {
        companyId,
        startAt: { gte: startOfToday, lte: in7Days },
        status: { not: MeetingStatus.CANCELLED },
        OR: [{ organizerId: userId }, { attendeeUserIds: { has: userId } }],
      },
      include: { contact: true, deal: true },
      orderBy: { startAt: 'asc' },
    });
  }

  async getForecast(companyId: string) {
    const in30Days = new Date();
    in30Days.setDate(in30Days.getDate() + 30);

    const [openDeals, dueSoonInvoices] = await Promise.all([
      this.prisma.deal.findMany({
        where: { companyId, stage: { in: OPEN_STAGES } },
        select: { value: true, stage: true, probability: true },
      }),
      this.prisma.invoice.findMany({
        where: { companyId, status: { in: [InvoiceStatus.SENT, InvoiceStatus.OVERDUE] }, dueDate: { lte: in30Days } },
        select: { amount: true, status: true, payments: { select: { amount: true } } },
      }),
    ]);

    let rawPipelineValue = 0;
    let weightedDealValue = 0;
    for (const deal of openDeals) {
      const value = Number(deal.value);
      rawPipelineValue += value;
      weightedDealValue += value * (deal.probability / 100) * STAGE_FORECAST_MULTIPLIER[deal.stage];
    }

    let weightedInvoiceValue = 0;
    for (const invoice of dueSoonInvoices) {
      const remaining = Number(invoice.amount) - invoice.payments.reduce((sum, p) => sum + Number(p.amount), 0);
      const weight = invoice.status === InvoiceStatus.OVERDUE ? 0.5 : 1;
      weightedInvoiceValue += Math.max(remaining, 0) * weight;
    }

    const estimatedRevenue = Math.round(weightedDealValue + weightedInvoiceValue);
    const confidencePercentage = rawPipelineValue > 0 ? Math.round((weightedDealValue / rawPipelineValue) * 100) : 0;

    const summary = await this.summarizeForecast(estimatedRevenue, confidencePercentage);

    return {
      estimatedRevenue,
      confidencePercentage,
      breakdown: {
        weightedDealValue: Math.round(weightedDealValue),
        weightedInvoiceValue: Math.round(weightedInvoiceValue),
        openDealsCount: openDeals.length,
        dueSoonInvoicesCount: dueSoonInvoices.length,
      },
      summary,
    };
  }

  private async summarizeForecast(estimatedRevenue: number, confidencePercentage: number): Promise<string> {
    const fallback = `Тахминан дар 30 рӯзи оянда даромади ${estimatedRevenue} интизор мешавад (эътимод ${confidencePercentage}%).`;

    const apiKey = this.config.get<string>('gemini.apiKey');
    if (!apiKey) {
      return fallback;
    }

    try {
      const gemini = new GoogleGenAI({ apiKey });
      const response = await gemini.models.generateContent({
        model: 'gemini-flash-latest',
        contents: `Write ONE short natural-language sentence summarizing this sales forecast: estimated next-30-days revenue is ${estimatedRevenue}, confidence ${confidencePercentage}%. No markdown.`,
        config: { maxOutputTokens: 128 },
      });
      return (response.text ?? '').trim() || fallback;
    } catch {
      return fallback;
    }
  }
}
