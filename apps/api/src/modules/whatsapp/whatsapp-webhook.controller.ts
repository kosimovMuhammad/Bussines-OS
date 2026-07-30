import { Body, Controller, Get, HttpCode, Post, Query, Res, UseGuards } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { InjectQueue } from '@nestjs/bullmq';
import { Throttle } from '@nestjs/throttler';
import { ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import type { Queue } from 'bullmq';
import type { Response } from 'express';
import { QUEUES } from '../../queue/queue.constants';
import { WhatsappSignatureGuard } from './guards/whatsapp-signature.guard';
import { extractIncomingMessages } from './whatsapp-webhook.util';

@ApiTags('WhatsApp')
@Throttle({ default: { limit: 20, ttl: 60_000 } })
@Controller('webhooks/whatsapp')
export class WhatsappWebhookController {
  constructor(
    private readonly config: ConfigService,
    @InjectQueue(QUEUES.WHATSAPP_INCOMING) private readonly queue: Queue,
  ) {}

  @Get()
  @ApiOperation({ summary: 'Meta webhook verification handshake (hub.challenge echo)' })
  @ApiResponse({ status: 200, description: 'Challenge echoed back, verification succeeded' })
  @ApiResponse({ status: 403, description: 'Verify token mismatch' })
  verify(
    @Query('hub.mode') mode: string,
    @Query('hub.verify_token') verifyToken: string,
    @Query('hub.challenge') challenge: string,
    @Res() res: Response,
  ) {
    const expectedToken = this.config.get<string>('whatsapp.verifyToken');
    if (mode === 'subscribe' && verifyToken && expectedToken && verifyToken === expectedToken) {
      return res.status(200).send(challenge);
    }
    return res.status(403).send('Verification failed');
  }

  @UseGuards(WhatsappSignatureGuard)
  @Post()
  @HttpCode(200)
  @ApiOperation({ summary: 'Meta webhook delivery endpoint — receives inbound WhatsApp events (signature-verified)' })
  @ApiResponse({ status: 200, description: 'Messages enqueued for async processing' })
  @ApiResponse({ status: 401, description: 'Invalid X-Hub-Signature-256' })
  async receive(@Body() payload: Parameters<typeof extractIncomingMessages>[0]) {
    const messages = extractIncomingMessages(payload);
    for (const message of messages) {
      await this.queue.add('incoming', message);
    }
    return { received: true };
  }
}
