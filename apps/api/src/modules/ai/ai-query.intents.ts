import { DealStage } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';

export const QUERY_INTENTS = ['STALE_CONTACTS', 'TOP_SALESPERSON', 'REVENUE_BY_PERIOD'] as const;

/** Контактҳое, ки ҳеҷ гоҳ communication надоштаанд ё охиринаш аз cutoff кӯҳнатар аст (Prisma-и "every" барои рӯйхати холӣ ҳам рост баргардонида мешавад). */
export async function runStaleContacts(prisma: PrismaService, companyId: string, days = 14) {
  const cutoff = new Date();
  cutoff.setDate(cutoff.getDate() - days);

  const contacts = await prisma.contact.findMany({
    where: { companyId, communications: { every: { timestamp: { lt: cutoff } } } },
    select: { id: true, firstName: true, lastName: true, email: true, phone: true },
    orderBy: { createdAt: 'desc' },
    take: 50,
  });

  return contacts;
}

export async function runTopSalesperson(prisma: PrismaService, companyId: string) {
  const grouped = await prisma.deal.groupBy({
    by: ['ownerId'],
    where: { companyId, stage: DealStage.WON, ownerId: { not: null } },
    _sum: { value: true },
    _count: { _all: true },
    orderBy: { _sum: { value: 'desc' } },
    take: 5,
  });

  const ownerIds = grouped.map((g) => g.ownerId).filter((id): id is string => !!id);
  const owners = await prisma.user.findMany({ where: { id: { in: ownerIds } }, select: { id: true, name: true } });
  const ownerName = new Map(owners.map((o) => [o.id, o.name]));

  return grouped.map((g) => ({
    ownerId: g.ownerId,
    ownerName: g.ownerId ? (ownerName.get(g.ownerId) ?? null) : null,
    wonValue: Number(g._sum.value ?? 0),
    wonCount: g._count._all,
  }));
}

export async function runRevenueByPeriod(prisma: PrismaService, companyId: string, months = 1) {
  const since = new Date();
  since.setMonth(since.getMonth() - (months - 1), 1);
  since.setHours(0, 0, 0, 0);

  const deals = await prisma.deal.findMany({
    where: { companyId, stage: DealStage.WON, updatedAt: { gte: since } },
    select: { id: true, title: true, value: true, updatedAt: true },
    orderBy: { updatedAt: 'desc' },
    take: 100,
  });

  return {
    since: since.toISOString(),
    totalRevenue: deals.reduce((sum, d) => sum + Number(d.value), 0),
    deals: deals.map((d) => ({ id: d.id, title: d.title, value: Number(d.value), updatedAt: d.updatedAt.toISOString() })),
  };
}
