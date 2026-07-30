import { Test } from '@nestjs/testing';
import { BadRequestException } from '@nestjs/common';
import { getQueueToken } from '@nestjs/bullmq';
import { CalendarService } from './calendar.service';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';

describe('CalendarService', () => {
  let service: CalendarService;
  let prisma: {
    meeting: { create: jest.Mock; findFirst: jest.Mock; update: jest.Mock };
    contact: { findFirst: jest.Mock };
    deal: { findFirst: jest.Mock };
  };
  let outboundQueue: { add: jest.Mock };

  beforeEach(async () => {
    prisma = {
      meeting: { create: jest.fn(), findFirst: jest.fn(), update: jest.fn() },
      contact: { findFirst: jest.fn() },
      deal: { findFirst: jest.fn() },
    };
    outboundQueue = { add: jest.fn() };

    const moduleRef = await Test.createTestingModule({
      providers: [
        CalendarService,
        { provide: PrismaService, useValue: prisma },
        { provide: getQueueToken(QUEUES.CALENDAR_SYNC_OUTBOUND), useValue: outboundQueue },
      ],
    }).compile();

    service = moduleRef.get(CalendarService);
  });

  describe('create', () => {
    it('rejects a meeting where endAt is before startAt', async () => {
      await expect(
        service.create('company-1', 'user-1', {
          title: 'Sync',
          startAt: '2026-08-01T10:00:00.000Z',
          endAt: '2026-08-01T09:00:00.000Z',
        }),
      ).rejects.toThrow(BadRequestException);

      expect(prisma.meeting.create).not.toHaveBeenCalled();
    });

    it('rejects a meeting where endAt equals startAt', async () => {
      await expect(
        service.create('company-1', 'user-1', {
          title: 'Sync',
          startAt: '2026-08-01T10:00:00.000Z',
          endAt: '2026-08-01T10:00:00.000Z',
        }),
      ).rejects.toThrow(BadRequestException);
    });

    it('creates the meeting, defaults attendees to the organizer, and enqueues an outbound Google Calendar sync job', async () => {
      prisma.meeting.create.mockResolvedValue({ id: 'meeting-1', organizerId: 'user-1' });

      await service.create('company-1', 'user-1', {
        title: 'Sync',
        startAt: '2026-08-01T09:00:00.000Z',
        endAt: '2026-08-01T10:00:00.000Z',
      });

      expect(prisma.meeting.create).toHaveBeenCalledWith(
        expect.objectContaining({
          data: expect.objectContaining({ attendeeUserIds: ['user-1'] }),
        }),
      );
      expect(outboundQueue.add).toHaveBeenCalledWith('create', {
        action: 'create',
        companyId: 'company-1',
        meetingId: 'meeting-1',
        organizerId: 'user-1',
      });
    });
  });

  describe('update', () => {
    it('rejects updating endAt to before the existing startAt', async () => {
      prisma.meeting.findFirst.mockResolvedValue({
        id: 'meeting-1',
        startAt: new Date('2026-08-01T09:00:00.000Z'),
        endAt: new Date('2026-08-01T10:00:00.000Z'),
      });

      await expect(service.update('company-1', 'meeting-1', { endAt: '2026-08-01T08:00:00.000Z' })).rejects.toThrow(
        BadRequestException,
      );

      expect(prisma.meeting.update).not.toHaveBeenCalled();
    });
  });
});
