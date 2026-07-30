import { Injectable } from '@nestjs/common';
import { DealStage, TaskStatus } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';

@Injectable()
export class AnalyticsService {
  constructor(private readonly prisma: PrismaService) {}

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
}
