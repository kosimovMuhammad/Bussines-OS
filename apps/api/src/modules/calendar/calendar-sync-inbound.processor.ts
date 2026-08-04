import { InjectQueue, Processor, WorkerHost } from '@nestjs/bullmq';
import { Logger, OnModuleInit } from '@nestjs/common';
import { MeetingStatus, MeetingSyncStatus } from '@prisma/client';
import type { Job, Queue } from 'bullmq';
import { randomUUID } from 'crypto';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';
import { GoogleCalendarChangedEvent, GoogleCalendarService } from './google-calendar.service';

const RENEWAL_CHECK_CRON = '0 3 * * *';
const RENEWAL_JOB_ID = 'calendar-channel-renewal-daily';
export const RENEWAL_THRESHOLD_MS = 24 * 60 * 60 * 1000;

export interface ProcessChangesJobData {
  calendarSyncTokenId: string;
}

@Processor(QUEUES.CALENDAR_SYNC_INBOUND)
export class CalendarSyncInboundProcessor extends WorkerHost implements OnModuleInit {
  private readonly logger = new Logger(CalendarSyncInboundProcessor.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly googleCalendar: GoogleCalendarService,
    @InjectQueue(QUEUES.CALENDAR_SYNC_INBOUND) private readonly queue: Queue,
  ) {
    super();
  }

  async onModuleInit() {
    try {
      await this.queue.add('renew-channels', {}, { repeat: { pattern: RENEWAL_CHECK_CRON }, jobId: RENEWAL_JOB_ID });
    } catch (error) {
      this.logger.error(`Сабти repeatable job-и calendar channel renewal ноком шуд: ${error}`);
    }
  }

  async process(job: Job) {
    if (job.name === 'renew-channels') {
      return this.renewExpiringChannels();
    }
    return this.processChanges(job.data as ProcessChangesJobData);
  }

  private async processChanges({ calendarSyncTokenId }: ProcessChangesJobData) {
    const syncToken = await this.prisma.calendarSyncToken.findUnique({ where: { id: calendarSyncTokenId } });
    if (!syncToken) {
      this.logger.warn(`CalendarSyncToken ${calendarSyncTokenId} ёфт нашуд`);
      return { skipped: true };
    }

    const client = await this.googleCalendar.getAuthorizedClient(syncToken.companyId, syncToken.userId);
    if (!client) {
      this.logger.warn(`Пайвастшавии Calendar барои user ${syncToken.userId} дигар мавҷуд нест`);
      return { skipped: true };
    }

    const { events, nextSyncToken } = await this.googleCalendar.listChangedEvents(client, syncToken.syncToken);

    for (const event of events) {
      await this.applyChangedEvent(syncToken.companyId, syncToken.userId, event);
    }

    await this.prisma.calendarSyncToken.update({ where: { id: syncToken.id }, data: { syncToken: nextSyncToken } });

    return { processed: events.length };
  }

  private async applyChangedEvent(companyId: string, userId: string, event: GoogleCalendarChangedEvent) {
    const existing = await this.prisma.meeting.findFirst({ where: { googleEventId: event.id, companyId } });

    if (event.status === 'cancelled') {
      if (existing) {
        await this.prisma.meeting.update({
          where: { id: existing.id },
          data: { status: MeetingStatus.CANCELLED, syncStatus: MeetingSyncStatus.SYNCED, lastSyncedAt: new Date() },
        });
      }
      return;
    }

    const startAt = event.start?.dateTime ? new Date(event.start.dateTime) : undefined;
    const endAt = event.end?.dateTime ? new Date(event.end.dateTime) : undefined;
    if (!startAt || !endAt) {
      return;
    }

    if (existing) {
      await this.prisma.meeting.update({
        where: { id: existing.id },
        data: {
          title: event.summary ?? existing.title,
          description: event.description ?? existing.description,
          location: event.location ?? existing.location,
          startAt,
          endAt,
          syncStatus: MeetingSyncStatus.SYNCED,
          lastSyncedAt: new Date(),
        },
      });
      return;
    }

    if (this.googleCalendar.isBusinessOsEvent(event)) {
      return;
    }

    const attendeeEmails = (event.attendees ?? []).map((a) => a.email).filter((e): e is string => !!e);
    const attendeeUsers = attendeeEmails.length
      ? await this.prisma.user.findMany({ where: { companyId, email: { in: attendeeEmails } }, select: { id: true } })
      : [];

    await this.prisma.meeting.create({
      data: {
        companyId,
        organizerId: userId,
        title: event.summary ?? '(Бе унвон)',
        description: event.description,
        location: event.location,
        startAt,
        endAt,
        attendeeUserIds: attendeeUsers.map((u) => u.id),
        googleEventId: event.id,
        googleCalendarId: 'primary',
        syncStatus: MeetingSyncStatus.SYNCED,
        lastSyncedAt: new Date(),
      },
    });
  }

  private async renewExpiringChannels() {
    const threshold = new Date(Date.now() + RENEWAL_THRESHOLD_MS);
    const expiringSoon = await this.prisma.calendarSyncToken.findMany({
      where: { channelExpiration: { lte: threshold } },
    });

    let renewed = 0;
    for (const token of expiringSoon) {
      const client = await this.googleCalendar.getAuthorizedClient(token.companyId, token.userId);
      if (!client) continue;

      try {
        const newChannelId = randomUUID();
        const newChannelToken = randomUUID();
        const { resourceId, expiration } = await this.googleCalendar.watchCalendar(
          client,
          newChannelId,
          newChannelToken,
        );
        await this.googleCalendar.stopChannel(client, token.channelId, token.resourceId);
        await this.prisma.calendarSyncToken.update({
          where: { id: token.id },
          data: { channelId: newChannelId, channelToken: newChannelToken, resourceId, channelExpiration: expiration },
        });
        renewed++;
      } catch (error) {
        this.logger.error(`Навсозии channel барои user ${token.userId} ноком шуд: ${error}`);
      }
    }

    return { renewed, checked: expiringSoon.length };
  }
}
