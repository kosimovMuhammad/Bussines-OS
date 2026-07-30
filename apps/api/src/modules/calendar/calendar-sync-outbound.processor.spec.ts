import { MeetingStatus, MeetingSyncStatus } from '@prisma/client';
import type { Job } from 'bullmq';
import { CalendarSyncOutboundProcessor } from './calendar-sync-outbound.processor';
import type { GoogleCalendarService } from './google-calendar.service';
import type { PrismaService } from '../../prisma/prisma.service';

function makePrisma() {
  return {
    meeting: {
      findUnique: jest.fn().mockResolvedValue({
        id: 'm1',
        status: MeetingStatus.SCHEDULED,
        googleEventId: null,
        attendeeUserIds: [],
        title: 'Sync test',
        description: null,
        location: null,
        startAt: new Date('2026-08-01T09:00:00.000Z'),
        endAt: new Date('2026-08-01T10:00:00.000Z'),
      }),
      update: jest.fn().mockResolvedValue({}),
    },
    user: { findMany: jest.fn().mockResolvedValue([]) },
  } as unknown as PrismaService;
}

function makeGoogleCalendar() {
  return {
    getAuthorizedClient: jest.fn().mockResolvedValue({}),
    createEvent: jest.fn().mockRejectedValue(new Error('Google API is down')),
    patchEvent: jest.fn(),
    deleteEvent: jest.fn(),
  } as unknown as GoogleCalendarService;
}

describe('CalendarSyncOutboundProcessor — retry/failure path', () => {
  it('marks the Meeting SYNC_FAILED with the error message once the final retry attempt fails', async () => {
    const prisma = makePrisma();
    const googleCalendar = makeGoogleCalendar();
    const processor = new CalendarSyncOutboundProcessor(prisma, googleCalendar);

    const job = {
      data: { action: 'create', companyId: 'c1', meetingId: 'm1', organizerId: 'u1' },
      attemptsMade: 4,
      opts: { attempts: 5 },
    } as unknown as Job;

    await expect(processor.process(job)).rejects.toThrow('Google API is down');

    expect(prisma.meeting.update).toHaveBeenCalledWith({
      where: { id: 'm1' },
      data: { syncStatus: MeetingSyncStatus.SYNC_FAILED, syncError: 'Google API is down' },
    });
  });

  it('does not touch syncStatus while retries remain (not the final attempt), but still surfaces the error to BullMQ for retry', async () => {
    const prisma = makePrisma();
    const googleCalendar = makeGoogleCalendar();
    const processor = new CalendarSyncOutboundProcessor(prisma, googleCalendar);

    const job = {
      data: { action: 'create', companyId: 'c1', meetingId: 'm1', organizerId: 'u1' },
      attemptsMade: 0,
      opts: { attempts: 5 },
    } as unknown as Job;

    await expect(processor.process(job)).rejects.toThrow('Google API is down');

    expect(prisma.meeting.update).not.toHaveBeenCalled();
  });

  it('skips silently (no error, no retry) when the organizer has not connected Google Calendar', async () => {
    const prisma = makePrisma();
    const googleCalendar = makeGoogleCalendar();
    (googleCalendar.getAuthorizedClient as jest.Mock).mockResolvedValue(null);
    const processor = new CalendarSyncOutboundProcessor(prisma, googleCalendar);

    const job = {
      data: { action: 'create', companyId: 'c1', meetingId: 'm1', organizerId: 'u1' },
      attemptsMade: 0,
      opts: { attempts: 5 },
    } as unknown as Job;

    await expect(processor.process(job)).resolves.toEqual({ skipped: true });
    expect(googleCalendar.createEvent).not.toHaveBeenCalled();
  });
});
