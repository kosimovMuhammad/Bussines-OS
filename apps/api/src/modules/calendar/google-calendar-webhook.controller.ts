import { Controller, Headers, HttpCode, Post, Req, UseGuards } from '@nestjs/common';
import { ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { InjectQueue } from '@nestjs/bullmq';
import { Throttle } from '@nestjs/throttler';
import type { Queue } from 'bullmq';
import { QUEUES } from '../../queue/queue.constants';
import { GoogleCalendarWebhookGuard, GoogleCalendarWebhookRequest } from './guards/google-calendar-webhook.guard';

@ApiTags('Calendar')
@Throttle({ default: { limit: 30, ttl: 60_000 } })
@Controller('webhooks/google-calendar')
export class GoogleCalendarWebhookController {
  constructor(@InjectQueue(QUEUES.CALENDAR_SYNC_INBOUND) private readonly queue: Queue) {}

  @UseGuards(GoogleCalendarWebhookGuard)
  @Post()
  @HttpCode(200)
  @ApiOperation({ summary: 'Google Calendar push-notification receiver (X-Goog-Channel-Token verified, no bearer auth)' })
  @ApiResponse({ status: 200, description: 'Notification accepted, incremental sync enqueued' })
  @ApiResponse({ status: 401, description: 'Missing/unknown/invalid X-Goog-Channel-Id or X-Goog-Channel-Token' })
  async receive(
    @Headers('x-goog-resource-state') resourceState: string | undefined,
    @Req() req: GoogleCalendarWebhookRequest,
  ) {
    if (resourceState === 'sync' || !req.calendarSyncTokenId) {
      return { received: true };
    }
    await this.queue.add('process-changes', { calendarSyncTokenId: req.calendarSyncTokenId });
    return { received: true };
  }
}
