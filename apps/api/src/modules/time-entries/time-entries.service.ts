import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { TaskStatus } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { AuditLogService } from '../audit-log/audit-log.service';

@Injectable()
export class TimeEntriesService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly auditLog: AuditLogService,
  ) {}

  async start(companyId: string, userId: string, taskId: string, note?: string) {
    const task = await this.assertTaskInCompany(companyId, taskId);

    const runningForUser = await this.prisma.timeEntry.findFirst({
      where: { userId, stoppedAt: null },
    });
    if (runningForUser) {
      throw new ConflictException('Аввал timer-и қаблиро stop кун');
    }

    return this.prisma.$transaction(async (tx) => {
      const entry = await tx.timeEntry.create({
        data: { companyId, taskId, userId, note },
      });
      if (task.status === TaskStatus.TODO) {
        await tx.task.update({ where: { id: taskId }, data: { status: TaskStatus.IN_PROGRESS } });
      }
      return entry;
    });
  }

  async stop(companyId: string, userId: string, taskId: string) {
    await this.assertTaskInCompany(companyId, taskId);

    const running = await this.prisma.timeEntry.findFirst({
      where: { taskId, userId, stoppedAt: null },
    });
    if (!running) {
      throw new NotFoundException('Timer running нест');
    }

    const stoppedAt = new Date();
    const durationSec = Math.round((stoppedAt.getTime() - running.startedAt.getTime()) / 1000);

    const updated = await this.prisma.timeEntry.update({
      where: { id: running.id },
      data: { stoppedAt, durationSec },
    });

    const totalTimeSec = await this.getTotalTimeSec(taskId);

    return { ...updated, totalTimeSec };
  }

  async findAllForTask(companyId: string, taskId: string) {
    await this.assertTaskInCompany(companyId, taskId);
    return this.prisma.timeEntry.findMany({
      where: { taskId, companyId },
      orderBy: { startedAt: 'desc' },
    });
  }

  async findActive(companyId: string, userId: string) {
    return this.prisma.timeEntry.findFirst({
      where: { userId, companyId, stoppedAt: null },
      include: { task: true },
    });
  }

  async remove(companyId: string, taskId: string, entryId: string, actorId: string) {
    await this.assertTaskInCompany(companyId, taskId);
    const entry = await this.prisma.timeEntry.findFirst({ where: { id: entryId, taskId, companyId } });
    if (!entry) {
      throw new NotFoundException('TimeEntry ёфт нашуд');
    }
    await this.prisma.timeEntry.delete({ where: { id: entryId } });
    await this.auditLog.log({
      companyId,
      userId: actorId,
      action: 'time_entry.deleted',
      entity: 'TimeEntry',
      entityId: entryId,
    });
    return { success: true };
  }

  private async getTotalTimeSec(taskId: string) {
    const result = await this.prisma.timeEntry.aggregate({
      where: { taskId, durationSec: { not: null } },
      _sum: { durationSec: true },
    });
    return result._sum.durationSec ?? 0;
  }

  private async assertTaskInCompany(companyId: string, taskId: string) {
    const task = await this.prisma.task.findFirst({ where: { id: taskId, companyId } });
    if (!task) {
      throw new NotFoundException('Task ёфт нашуд');
    }
    return task;
  }
}
