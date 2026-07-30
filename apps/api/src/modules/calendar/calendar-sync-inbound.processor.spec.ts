import { MeetingStatus, MeetingSyncStatus } from '@prisma/client';
import type { Job } from 'bullmq';
import { CalendarSyncInboundProcessor, RENEWAL_THRESHOLD_MS } from './calendar-sync-inbound.processor';
import type { GoogleCalendarChangedEvent, GoogleCalendarService } from './google-calendar.service';
import type { PrismaService } from '../../prisma/prisma.service';

function makePrisma(overrides: Record<string, unknown> = {}) {
  return {
    calendarSyncToken: {
      findUnique: jest.fn(),
      update: jest.fn(),
      findMany: jest.fn().mockResolvedValue([]),
    },
    meeting: { findFirst: jest.fn(), update: jest.fn(), create: jest.fn() },
    user: { findMany: jest.fn().mockResolvedValue([]) },
    ...overrides,
  } as unknown as PrismaService;
}

function makeGoogleCalendar(overrides: Record<string, unknown> = {}) {
  return {
    getAuthorizedClient: jest.fn().mockResolvedValue({}),
    listChangedEvents: jest.fn(),
    isBusinessOsEvent: jest.fn().mockReturnValue(false),
    watchCalendar: jest.fn(),
    stopChannel: jest.fn(),
    ...overrides,
  } as unknown as GoogleCalendarService;
}

const queueStub = { add: jest.fn() } as never;

describe('CalendarSyncInboundProcessor — incremental sync', () => {
  it('updates the matching Meeting when its Google event changes', async () => {
    const prisma = makePrisma({
      calendarSyncToken: {
        findUnique: jest.fn().mockResolvedValue({ id: 'st1', companyId: 'c1', userId: 'u1', syncToken: 'tok1' }),
        update: jest.fn(),
      },
      meeting: {
        findFirst: jest.fn().mockResolvedValue({ id: 'm1', title: 'Old title', description: null, location: null }),
        update: jest.fn(),
        create: jest.fn(),
      },
    });
    const changedEvent: GoogleCalendarChangedEvent = {
      id: 'evt1',
      status: 'confirmed',
      summary: 'New title',
      start: { dateTime: '2026-08-01T10:00:00.000Z' },
      end: { dateTime: '2026-08-01T11:00:00.000Z' },
    };
    const googleCalendar = makeGoogleCalendar({
      getAuthorizedClient: jest.fn().mockResolvedValue({}),
      listChangedEvents: jest.fn().mockResolvedValue({ events: [changedEvent], nextSyncToken: 'tok2' }),
    });
    const processor = new CalendarSyncInboundProcessor(prisma, googleCalendar, queueStub);

    await processor.process({ name: 'process-changes', data: { calendarSyncTokenId: 'st1' } } as Job);

    expect(prisma.meeting.update).toHaveBeenCalledWith({
      where: { id: 'm1' },
      data: expect.objectContaining({ title: 'New title', syncStatus: MeetingSyncStatus.SYNCED }),
    });
    expect(prisma.calendarSyncToken.update).toHaveBeenCalledWith({ where: { id: 'st1' }, data: { syncToken: 'tok2' } });
  });

  it('marks the matching Meeting CANCELLED when its Google event is cancelled', async () => {
    const prisma = makePrisma({
      calendarSyncToken: {
        findUnique: jest.fn().mockResolvedValue({ id: 'st1', companyId: 'c1', userId: 'u1', syncToken: 'tok1' }),
        update: jest.fn(),
      },
      meeting: {
        findFirst: jest.fn().mockResolvedValue({ id: 'm2' }),
        update: jest.fn(),
        create: jest.fn(),
      },
    });
    const cancelledEvent: GoogleCalendarChangedEvent = { id: 'evt2', status: 'cancelled' };
    const googleCalendar = makeGoogleCalendar({
      listChangedEvents: jest.fn().mockResolvedValue({ events: [cancelledEvent], nextSyncToken: 'tok2' }),
    });
    const processor = new CalendarSyncInboundProcessor(prisma, googleCalendar, queueStub);

    await processor.process({ name: 'process-changes', data: { calendarSyncTokenId: 'st1' } } as Job);

    expect(prisma.meeting.update).toHaveBeenCalledWith({
      where: { id: 'm2' },
      data: expect.objectContaining({ status: MeetingStatus.CANCELLED }),
    });
  });

  it('creates an unlinked Meeting for a new external event with no business-os marker', async () => {
    const prisma = makePrisma({
      calendarSyncToken: {
        findUnique: jest.fn().mockResolvedValue({ id: 'st1', companyId: 'c1', userId: 'u1', syncToken: 'tok1' }),
        update: jest.fn(),
      },
      meeting: { findFirst: jest.fn().mockResolvedValue(null), update: jest.fn(), create: jest.fn() },
    });
    const externalEvent: GoogleCalendarChangedEvent = {
      id: 'evt3',
      status: 'confirmed',
      summary: 'External meeting',
      start: { dateTime: '2026-08-02T10:00:00.000Z' },
      end: { dateTime: '2026-08-02T11:00:00.000Z' },
    };
    const googleCalendar = makeGoogleCalendar({
      listChangedEvents: jest.fn().mockResolvedValue({ events: [externalEvent], nextSyncToken: 'tok2' }),
      isBusinessOsEvent: jest.fn().mockReturnValue(false),
    });
    const processor = new CalendarSyncInboundProcessor(prisma, googleCalendar, queueStub);

    await processor.process({ name: 'process-changes', data: { calendarSyncTokenId: 'st1' } } as Job);

    expect(prisma.meeting.create).toHaveBeenCalledWith({
      data: expect.objectContaining({
        companyId: 'c1',
        organizerId: 'u1',
        title: 'External meeting',
        googleEventId: 'evt3',
        syncStatus: MeetingSyncStatus.SYNCED,
      }),
    });
  });

  it('does not create a duplicate Meeting for an unmatched event that is already marked as ours', async () => {
    const prisma = makePrisma({
      calendarSyncToken: {
        findUnique: jest.fn().mockResolvedValue({ id: 'st1', companyId: 'c1', userId: 'u1', syncToken: 'tok1' }),
        update: jest.fn(),
      },
      meeting: { findFirst: jest.fn().mockResolvedValue(null), update: jest.fn(), create: jest.fn() },
    });
    const raceEvent: GoogleCalendarChangedEvent = {
      id: 'evt4',
      status: 'confirmed',
      start: { dateTime: '2026-08-03T10:00:00.000Z' },
      end: { dateTime: '2026-08-03T11:00:00.000Z' },
      extendedProperties: { private: { source: 'business-os' } },
    };
    const googleCalendar = makeGoogleCalendar({
      listChangedEvents: jest.fn().mockResolvedValue({ events: [raceEvent], nextSyncToken: 'tok2' }),
      isBusinessOsEvent: jest.fn().mockReturnValue(true),
    });
    const processor = new CalendarSyncInboundProcessor(prisma, googleCalendar, queueStub);

    await processor.process({ name: 'process-changes', data: { calendarSyncTokenId: 'st1' } } as Job);

    expect(prisma.meeting.create).not.toHaveBeenCalled();
  });
});

describe('CalendarSyncInboundProcessor — channel renewal', () => {
  it('renews only channels expiring within 24h, not ones expiring in 5 days', async () => {
    const soon = { id: 'tok-soon', companyId: 'c1', userId: 'u1', channelId: 'chan-soon', resourceId: 'res-soon', channelExpiration: new Date(Date.now() + 60 * 60 * 1000) };
    const far = { id: 'tok-far', companyId: 'c2', userId: 'u2', channelId: 'chan-far', resourceId: 'res-far', channelExpiration: new Date(Date.now() + 5 * 24 * 60 * 60 * 1000) };
    const allTokens = [soon, far];

    const prisma = makePrisma({
      calendarSyncToken: {
        findUnique: jest.fn(),
        update: jest.fn(),
        findMany: jest
          .fn()
          .mockImplementation((args: { where: { channelExpiration: { lte: Date } } }) =>
            Promise.resolve(allTokens.filter((t) => t.channelExpiration.getTime() <= args.where.channelExpiration.lte.getTime())),
          ),
      },
    });
    const googleCalendar = makeGoogleCalendar({
      watchCalendar: jest.fn().mockResolvedValue({ resourceId: 'new-res', expiration: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000) }),
    });
    const processor = new CalendarSyncInboundProcessor(prisma, googleCalendar, queueStub);

    const result = await processor.process({ name: 'renew-channels', data: {} } as Job);

    expect(result).toEqual({ renewed: 1, checked: 1 });
    expect(googleCalendar.watchCalendar).toHaveBeenCalledTimes(1);
    expect(prisma.calendarSyncToken.update).toHaveBeenCalledWith(
      expect.objectContaining({ where: { id: 'tok-soon' } }),
    );
    // sanity: threshold used really is ~24h out, not something looser
    expect(RENEWAL_THRESHOLD_MS).toBe(24 * 60 * 60 * 1000);
  });
});
