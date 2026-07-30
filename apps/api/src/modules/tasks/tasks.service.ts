import { Injectable, NotFoundException } from '@nestjs/common';
import { Prisma, TaskStatus } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { AuditLogService } from '../audit-log/audit-log.service';
import { NotificationsService } from '../notifications/notifications.service';
import { NOTIFICATION_EVENTS } from '../notifications/notifications.constants';
import { CreateTaskDto } from './dto/create-task.dto';
import { UpdateTaskDto } from './dto/update-task.dto';
import { QueryTasksDto } from './dto/query-tasks.dto';

const ASSIGNEE_SELECT = { id: true, name: true, email: true, avatarUrl: true, role: true } as const;

@Injectable()
export class TasksService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly auditLog: AuditLogService,
    private readonly notifications: NotificationsService,
  ) {}

  async findAll(companyId: string, query: QueryTasksDto) {
    const where: Prisma.TaskWhereInput = { companyId };
    if (query.status) where.status = query.status;
    if (query.assignedTo) where.assignedTo = query.assignedTo;
    if (query.dealId) where.dealId = query.dealId;
    if (query.contactId) where.contactId = query.contactId;

    return this.prisma.task.findMany({
      where,
      include: { assignee: { select: ASSIGNEE_SELECT } },
      orderBy: { createdAt: 'desc' },
    });
  }

  async findOne(companyId: string, id: string) {
    const task = await this.prisma.task.findFirst({
      where: { id, companyId },
      include: {
        assignee: { select: ASSIGNEE_SELECT },
        deal: true,
        contact: true,
        timeEntries: true,
      },
    });
    if (!task) {
      throw new NotFoundException('Task ёфт нашуд');
    }
    const totalTimeSec = task.timeEntries.reduce((sum, entry) => sum + (entry.durationSec ?? 0), 0);
    return { ...task, totalTimeSec };
  }

  async create(companyId: string, dto: CreateTaskDto) {
    if (dto.assignedTo) {
      await this.assertUserInCompany(companyId, dto.assignedTo);
    }
    if (dto.dealId) {
      await this.assertDealInCompany(companyId, dto.dealId);
    }
    if (dto.contactId) {
      await this.assertContactInCompany(companyId, dto.contactId);
    }

    const task = await this.prisma.task.create({
      data: {
        companyId,
        title: dto.title,
        description: dto.description,
        priority: dto.priority,
        status: dto.status,
        assignedTo: dto.assignedTo,
        dueDate: dto.dueDate ? new Date(dto.dueDate) : undefined,
        dealId: dto.dealId,
        contactId: dto.contactId,
      },
      include: { assignee: { select: ASSIGNEE_SELECT } },
    });

    if (task.assignedTo) {
      this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.TASK_ASSIGNED, task);
    }

    return task;
  }

  async update(companyId: string, id: string, dto: UpdateTaskDto) {
    const previous = await this.ensureExists(companyId, id);
    if (dto.assignedTo) {
      await this.assertUserInCompany(companyId, dto.assignedTo);
    }
    if (dto.dealId) {
      await this.assertDealInCompany(companyId, dto.dealId);
    }
    if (dto.contactId) {
      await this.assertContactInCompany(companyId, dto.contactId);
    }

    const { dueDate, ...rest } = dto;

    const task = await this.prisma.task.update({
      where: { id },
      data: {
        ...rest,
        ...(dueDate ? { dueDate: new Date(dueDate) } : {}),
      },
      include: { assignee: { select: ASSIGNEE_SELECT } },
    });

    if (dto.assignedTo && dto.assignedTo !== previous.assignedTo) {
      this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.TASK_ASSIGNED, task);
    }

    return task;
  }

  async updateStatus(companyId: string, id: string, status: TaskStatus) {
    await this.ensureExists(companyId, id);
    return this.prisma.task.update({
      where: { id },
      data: { status },
      include: { assignee: { select: ASSIGNEE_SELECT } },
    });
  }

  async remove(companyId: string, id: string, actorId: string) {
    await this.ensureExists(companyId, id);
    await this.prisma.task.delete({ where: { id } });
    await this.auditLog.log({ companyId, userId: actorId, action: 'task.deleted', entity: 'Task', entityId: id });
    return { success: true };
  }

  private async ensureExists(companyId: string, id: string) {
    const task = await this.prisma.task.findFirst({ where: { id, companyId } });
    if (!task) {
      throw new NotFoundException('Task ёфт нашуд');
    }
    return task;
  }

  private async assertUserInCompany(companyId: string, userId: string) {
    const user = await this.prisma.user.findFirst({ where: { id: userId, companyId } });
    if (!user) {
      throw new NotFoundException('assignedTo корбари ин company нест');
    }
  }

  private async assertDealInCompany(companyId: string, dealId: string) {
    const deal = await this.prisma.deal.findFirst({ where: { id: dealId, companyId } });
    if (!deal) {
      throw new NotFoundException('dealId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }

  private async assertContactInCompany(companyId: string, contactId: string) {
    const contact = await this.prisma.contact.findFirst({ where: { id: contactId, companyId } });
    if (!contact) {
      throw new NotFoundException('contactId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }
}
