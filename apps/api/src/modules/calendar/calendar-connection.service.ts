import { BadGatewayException, Injectable, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { MeetingSyncStatus, OAuthProvider } from '@prisma/client';
import { randomUUID } from 'crypto';
import { PrismaService } from '../../prisma/prisma.service';
import { encryptToken } from '../../common/crypto/token-cipher';
import { GoogleCalendarService } from './google-calendar.service';

interface StatePayload {
  sub: string;
  companyId: string;
}

@Injectable()
export class CalendarConnectionService {
  constructor(
    private readonly config: ConfigService,
    private readonly prisma: PrismaService,
    private readonly jwt: JwtService,
    private readonly googleCalendar: GoogleCalendarService,
  ) {}

  getAuthUrl(userId: string, companyId: string): string {
    const state = this.jwt.sign(
      { sub: userId, companyId } satisfies StatePayload,
      { secret: this.config.get<string>('jwt.accessSecret'), expiresIn: '10m' },
    );
    return this.googleCalendar.getAuthUrl(state);
  }

  async handleCallback(code: string, state: string) {
    let payload: StatePayload;
    try {
      payload = this.jwt.verify<StatePayload>(state, { secret: this.config.get<string>('jwt.accessSecret') });
    } catch {
      throw new UnauthorizedException('state нодуруст ё муддаташ гузаштааст');
    }

    let tokens: { access_token?: string | null; refresh_token?: string | null; scope?: string; expiry_date?: number | null };
    try {
      tokens = await this.googleCalendar.exchangeCode(code);
    } catch {
      throw new BadGatewayException('Мубодилаи code бо Google муваффақ нашуд');
    }
    if (!tokens.access_token) {
      throw new BadGatewayException('Google access token барнагардонд');
    }

    const encryptionKey = this.config.get<string>('encryptionKey')!;
    const encryptedAccessToken = encryptToken(tokens.access_token, encryptionKey);
    const encryptedRefreshToken = tokens.refresh_token ? encryptToken(tokens.refresh_token, encryptionKey) : undefined;

    await this.prisma.oAuthConnection.upsert({
      where: { userId_provider: { userId: payload.sub, provider: OAuthProvider.GOOGLE_CALENDAR } },
      create: {
        companyId: payload.companyId,
        userId: payload.sub,
        provider: OAuthProvider.GOOGLE_CALENDAR,
        encryptedAccessToken,
        encryptedRefreshToken,
        scope: tokens.scope,
        expiresAt: tokens.expiry_date ? new Date(tokens.expiry_date) : undefined,
      },
      update: {
        encryptedAccessToken,
        ...(encryptedRefreshToken ? { encryptedRefreshToken } : {}),
        scope: tokens.scope,
        expiresAt: tokens.expiry_date ? new Date(tokens.expiry_date) : undefined,
      },
    });

    await this.registerWatch(payload.companyId, payload.sub);

    return { success: true, provider: 'google_calendar' as const };
  }

  private async registerWatch(companyId: string, userId: string) {
    const client = await this.googleCalendar.getAuthorizedClient(companyId, userId);
    if (!client) return;

    const channelId = randomUUID();
    const channelToken = randomUUID();

    const { resourceId, expiration } = await this.googleCalendar.watchCalendar(client, channelId, channelToken);
    const { nextSyncToken } = await this.googleCalendar.listChangedEvents(client, null);

    await this.prisma.calendarSyncToken.upsert({
      where: { userId },
      create: {
        companyId,
        userId,
        resourceId,
        syncToken: nextSyncToken,
        channelId,
        channelToken,
        channelExpiration: expiration,
      },
      update: { resourceId, syncToken: nextSyncToken, channelId, channelToken, channelExpiration: expiration },
    });
  }

  async getStatus(companyId: string, userId: string) {
    const [connection, syncToken] = await Promise.all([
      this.prisma.oAuthConnection.findFirst({ where: { companyId, userId, provider: OAuthProvider.GOOGLE_CALENDAR } }),
      this.prisma.calendarSyncToken.findUnique({ where: { userId } }),
    ]);

    if (!connection) {
      return { connected: false };
    }

    const channelHealthy = !!syncToken && syncToken.channelExpiration.getTime() > Date.now();

    return {
      connected: true,
      lastSyncedAt: syncToken?.updatedAt ?? null,
      channelExpiresAt: syncToken?.channelExpiration ?? null,
      syncHealth: channelHealthy ? ('ok' as const) : ('unhealthy' as const),
    };
  }

  async disconnect(companyId: string, userId: string) {
    const [connection, syncToken] = await Promise.all([
      this.prisma.oAuthConnection.findFirst({ where: { companyId, userId, provider: OAuthProvider.GOOGLE_CALENDAR } }),
      this.prisma.calendarSyncToken.findUnique({ where: { userId } }),
    ]);

    if (syncToken) {
      const client = await this.googleCalendar.getAuthorizedClient(companyId, userId);
      if (client) {
        await this.googleCalendar.stopChannel(client, syncToken.channelId, syncToken.resourceId);
      }
      await this.prisma.calendarSyncToken.delete({ where: { id: syncToken.id } });
    }

    if (connection) {
      await this.prisma.oAuthConnection.delete({ where: { id: connection.id } });
    }

    await this.prisma.meeting.updateMany({
      where: { companyId, organizerId: userId },
      data: { syncStatus: MeetingSyncStatus.NOT_SYNCED },
    });

    return { success: true };
  }
}
