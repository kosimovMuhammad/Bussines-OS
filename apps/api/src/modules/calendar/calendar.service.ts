import { BadRequestException, Injectable, Logger, NotFoundException } from '@nestjs/common';
import { InjectQueue } from '@nestjs/bullmq';
import { MeetingStatus, Prisma } from '@prisma/client';
import type { Queue } from 'bullmq';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';
import { CreateMeetingDto } from './dto/create-meeting.dto';
import { UpdateMeetingDto } from './dto/update-meeting.dto';
import { QueryMeetingsDto } from './dto/query-meetings.dto';
import type { CalendarSyncOutboundJobData } from './calendar-sync-outbound.processor';

const MEETING_INCLUDE = { contact: true, deal: true } as const;

@Injectable()
export class CalendarService {
  private readonly logger = new Logger(CalendarService.name);

  constructor(
    private readonly prisma: PrismaService,
    @InjectQueue(QUEUES.CALENDAR_SYNC_OUTBOUND) private readonly outboundQueue: Queue<CalendarSyncOutboundJobData>,
  ) {}

  // The meeting itself is already committed to Postgres by the time we enqueue
  // outbound Google Calendar sync, so a Redis hiccup here must not turn into a
  // false-negative 500 for a mutation that actually succeeded - log and move on.
  private async safeEnqueueOutbound(name: string, data: CalendarSyncOutboundJobData) {
    try {
      await this.outboundQueue.add(name, data);
    } catch (error) {
      this.logger.error(`Сафи calendar-sync-outbound дастнорас аст: ${error}`);
    }
  }

  async findAll(companyId: string, query: QueryMeetingsDto) {
    const where: Prisma.MeetingWhereInput = { companyId };
    if (query.from || query.to) {
      where.startAt = {
        ...(query.from ? { gte: new Date(query.from) } : {}),
        ...(query.to ? { lte: new Date(query.to) } : {}),
      };
    }
    if (query.contactId) where.contactId = query.contactId;
    if (query.dealId) where.dealId = query.dealId;
    if (query.organizerId) where.organizerId = query.organizerId;

    return this.prisma.meeting.findMany({ where, include: MEETING_INCLUDE, orderBy: { startAt: 'asc' } });
  }

  async findOne(companyId: string, id: string) {
    const meeting = await this.prisma.meeting.findFirst({ where: { id, companyId }, include: MEETING_INCLUDE });
    if (!meeting) {
      throw new NotFoundException('Meeting ёфт нашуд');
    }
    return meeting;
  }

  async create(companyId: string, organizerId: string, dto: CreateMeetingDto) {
    this.assertValidTimeRange(dto.startAt, dto.endAt);
    if (dto.contactId) {
      await this.assertContactInCompany(companyId, dto.contactId);
    }
    if (dto.dealId) {
      await this.assertDealInCompany(companyId, dto.dealId);
    }

    const meeting = await this.prisma.meeting.create({
      data: {
        companyId,
        contactId: dto.contactId,
        dealId: dto.dealId,
        title: dto.title,
        description: dto.description,
        startAt: new Date(dto.startAt),
        endAt: new Date(dto.endAt),
        location: dto.location,
        organizerId,
        attendeeUserIds: dto.attendeeUserIds?.length ? dto.attendeeUserIds : [organizerId],
      },
      include: MEETING_INCLUDE,
    });

    await this.safeEnqueueOutbound('create', {
      action: 'create',
      companyId,
      meetingId: meeting.id,
      organizerId: meeting.organizerId,
    });

    return meeting;
  }

  async update(companyId: string, id: string, dto: UpdateMeetingDto) {
    const existing = await this.ensureExists(companyId, id);
    const effectiveStartAt = dto.startAt ?? existing.startAt.toISOString();
    const effectiveEndAt = dto.endAt ?? existing.endAt.toISOString();
    this.assertValidTimeRange(effectiveStartAt, effectiveEndAt);

    if (dto.contactId) {
      await this.assertContactInCompany(companyId, dto.contactId);
    }
    if (dto.dealId) {
      await this.assertDealInCompany(companyId, dto.dealId);
    }

    const { startAt, endAt, ...rest } = dto;

    const meeting = await this.prisma.meeting.update({
      where: { id },
      data: {
        ...rest,
        ...(startAt ? { startAt: new Date(startAt) } : {}),
        ...(endAt ? { endAt: new Date(endAt) } : {}),
      },
      include: MEETING_INCLUDE,
    });

    await this.enqueueOutboundUpdate(companyId, meeting.id, meeting.organizerId);

    return meeting;
  }

  async updateStatus(companyId: string, id: string, status: MeetingStatus) {
    await this.ensureExists(companyId, id);
    const meeting = await this.prisma.meeting.update({ where: { id }, data: { status }, include: MEETING_INCLUDE });
    await this.enqueueOutboundUpdate(companyId, meeting.id, meeting.organizerId);
    return meeting;
  }

  async remove(companyId: string, id: string) {
    const existing = await this.ensureExists(companyId, id);
    await this.prisma.meeting.delete({ where: { id } });

    if (existing.googleEventId) {
      await this.safeEnqueueOutbound('delete', {
        action: 'delete',
        companyId,
        meetingId: id,
        organizerId: existing.organizerId,
        googleEventId: existing.googleEventId,
      });
    }

    return { success: true };
  }

  private async enqueueOutboundUpdate(companyId: string, meetingId: string, organizerId: string) {
    await this.safeEnqueueOutbound('update', { action: 'update', companyId, meetingId, organizerId });
  }

  /** endAt бояд ҳатман баъд аз startAt бошад, вагарна дучанд-омори мантиқан нодуруст сохта мешавад. */
  private assertValidTimeRange(startAt: string, endAt: string) {
    if (new Date(endAt).getTime() <= new Date(startAt).getTime()) {
      throw new BadRequestException('endAt бояд баъд аз startAt бошад');
    }
  }

  private async ensureExists(companyId: string, id: string) {
    const meeting = await this.prisma.meeting.findFirst({ where: { id, companyId } });
    if (!meeting) {
      throw new NotFoundException('Meeting ёфт нашуд');
    }
    return meeting;
  }

  private async assertContactInCompany(companyId: string, contactId: string) {
    const contact = await this.prisma.contact.findFirst({ where: { id: contactId, companyId } });
    if (!contact) {
      throw new BadRequestException('contactId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }

  private async assertDealInCompany(companyId: string, dealId: string) {
    const deal = await this.prisma.deal.findFirst({ where: { id: dealId, companyId } });
    if (!deal) {
      throw new BadRequestException('dealId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }
}
