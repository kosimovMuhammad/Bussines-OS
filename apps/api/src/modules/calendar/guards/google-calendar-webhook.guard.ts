import { CanActivate, ExecutionContext, Injectable, UnauthorizedException } from '@nestjs/common';
import { timingSafeEqual } from 'node:crypto';
import type { Request } from 'express';
import { PrismaService } from '../../../prisma/prisma.service';

export interface GoogleCalendarWebhookRequest extends Request {
  calendarSyncTokenId?: string;
}

@Injectable()
export class GoogleCalendarWebhookGuard implements CanActivate {
  constructor(private readonly prisma: PrismaService) {}

  async canActivate(context: ExecutionContext): Promise<boolean> {
    const req = context.switchToHttp().getRequest<GoogleCalendarWebhookRequest>();
    const channelId = req.headers['x-goog-channel-id'] as string | undefined;
    const channelToken = req.headers['x-goog-channel-token'] as string | undefined;

    if (!channelId || !channelToken) {
      throw new UnauthorizedException('X-Goog-Channel сарлавҳаҳо намерасанд');
    }

    const syncToken = await this.prisma.calendarSyncToken.findUnique({ where: { channelId } });
    if (!syncToken) {
      throw new UnauthorizedException('Channel номаълум аст');
    }

    const provided = Buffer.from(channelToken);
    const expected = Buffer.from(syncToken.channelToken);
    if (provided.length !== expected.length || !timingSafeEqual(provided, expected)) {
      throw new UnauthorizedException('X-Goog-Channel-Token нодуруст аст');
    }

    req.calendarSyncTokenId = syncToken.id;
    return true;
  }
}
