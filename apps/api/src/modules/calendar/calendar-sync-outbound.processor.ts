import { Processor, WorkerHost } from '@nestjs/bullmq';
import { Logger } from '@nestjs/common';
import { MeetingStatus, MeetingSyncStatus } from '@prisma/client';
import type { Job } from 'bullmq';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';
import { GoogleCalendarService } from './google-calendar.service';

export type CalendarSyncAction = 'create' | 'update' | 'delete';

export interface CalendarSyncOutboundJobData {
  action: CalendarSyncAction;
  companyId: string;
  meetingId: string;
  organizerId: string;
  googleEventId?: string;
}

@Processor(QUEUES.CALENDAR_SYNC_OUTBOUND)
export class CalendarSyncOutboundProcessor extends WorkerHost {
  private readonly logger = new Logger(CalendarSyncOutboundProcessor.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly googleCalendar: GoogleCalendarService,
  ) {
    super();
  }

  async process(job: Job<CalendarSyncOutboundJobData>) {
    const { action, companyId, meetingId, organizerId, googleEventId } = job.data;

    try {
      const client = await this.googleCalendar.getAuthorizedClient(companyId, organizerId);
      if (!client) {
        // Организатор Google Calendar-ро пайваст накардааст — sync мавзӯӣ нест, ин хатогӣ нест.
        return { skipped: true };
      }

      if (action === 'delete') {
        if (googleEventId) {
          await this.googleCalendar.deleteEvent(client, googleEventId);
        }
        return { deleted: true };
      }

      const meeting = await this.prisma.meeting.findUnique({ where: { id: meetingId } });
      if (!meeting) {
        return { skipped: true };
      }

      if (action === 'update' && meeting.status === MeetingStatus.CANCELLED) {
        if (meeting.googleEventId) {
          await this.googleCalendar.deleteEvent(client, meeting.googleEventId);
        }
        await this.prisma.meeting.update({
          where: { id: meetingId },
          data: { syncStatus: MeetingSyncStatus.SYNCED, syncError: null, lastSyncedAt: new Date() },
        });
        return { cancelledOnGoogle: true };
      }

      const attendeeEmails = await this.resolveAttendeeEmails(meeting.attendeeUserIds);
      const input = {
        meetingId: meeting.id,
        title: meeting.title,
        description: meeting.description,
        location: meeting.location,
        startAt: meeting.startAt,
        endAt: meeting.endAt,
        attendeeEmails,
      };

      if (action === 'create' || !meeting.googleEventId) {
        const { id } = await this.googleCalendar.createEvent(client, input);
        await this.prisma.meeting.update({
          where: { id: meetingId },
          data: {
            googleEventId: id,
            googleCalendarId: 'primary',
            syncStatus: MeetingSyncStatus.SYNCED,
            syncError: null,
            lastSyncedAt: new Date(),
          },
        });
      } else {
        await this.googleCalendar.patchEvent(client, meeting.googleEventId, input);
        await this.prisma.meeting.update({
          where: { id: meetingId },
          data: { syncStatus: MeetingSyncStatus.SYNCED, syncError: null, lastSyncedAt: new Date() },
        });
      }

      return { synced: true };
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Google Calendar sync ноком шуд';
      const maxAttempts = job.opts.attempts ?? 1;
      const isFinalAttempt = job.attemptsMade + 1 >= maxAttempts;

      if (isFinalAttempt && action !== 'delete') {
        await this.prisma.meeting
          .update({
            where: { id: meetingId },
            data: { syncStatus: MeetingSyncStatus.SYNC_FAILED, syncError: message },
          })
          .catch((updateError: unknown) => {
            this.logger.error(`Наметавонист syncStatus-ро SYNC_FAILED гузорад: ${updateError}`);
          });
      }

      this.logger.warn(`Calendar outbound sync (${action}) барои meeting ${meetingId} ноком шуд: ${message}`);
      throw error;
    }
  }

  private async resolveAttendeeEmails(attendeeUserIds: string[]): Promise<string[]> {
    if (attendeeUserIds.length === 0) return [];
    const users = await this.prisma.user.findMany({
      where: { id: { in: attendeeUserIds } },
      select: { email: true },
    });
    return users.map((u) => u.email);
  }
}
