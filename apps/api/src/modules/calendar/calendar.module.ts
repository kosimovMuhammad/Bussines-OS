import { Module } from '@nestjs/common';
import { BullModule } from '@nestjs/bullmq';
import { JwtModule } from '@nestjs/jwt';
import { QUEUES } from '../../queue/queue.constants';
import { CalendarController } from './calendar.controller';
import { CalendarService } from './calendar.service';
import { CalendarConnectionController } from './calendar-connection.controller';
import { CalendarConnectionService } from './calendar-connection.service';
import { GoogleCalendarWebhookController } from './google-calendar-webhook.controller';
import { GoogleCalendarWebhookGuard } from './guards/google-calendar-webhook.guard';
import { GoogleCalendarService } from './google-calendar.service';
import { CalendarSyncOutboundProcessor } from './calendar-sync-outbound.processor';
import { CalendarSyncInboundProcessor } from './calendar-sync-inbound.processor';

@Module({
  imports: [
    BullModule.registerQueue({ name: QUEUES.CALENDAR_SYNC_OUTBOUND }, { name: QUEUES.CALENDAR_SYNC_INBOUND }),
    JwtModule.register({}),
  ],
  controllers: [CalendarController, CalendarConnectionController, GoogleCalendarWebhookController],
  providers: [
    CalendarService,
    CalendarConnectionService,
    GoogleCalendarService,
    CalendarSyncOutboundProcessor,
    CalendarSyncInboundProcessor,
    GoogleCalendarWebhookGuard,
  ],
  exports: [CalendarService],
})
export class CalendarModule {}
