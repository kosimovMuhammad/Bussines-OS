import { Injectable, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { google, calendar_v3 } from 'googleapis';
import { OAuthProvider } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { decryptToken, encryptToken } from '../../common/crypto/token-cipher';

// googleapis bundles its own nested google-auth-library copy, distinct from the top-level package —
// deriving the type from google.auth.OAuth2 itself (rather than importing OAuth2Client separately)
// keeps it structurally identical to what googleapis' own APIs (e.g. google.calendar()) expect.
export type OAuth2Client = InstanceType<typeof google.auth.OAuth2>;

export const CALENDAR_SCOPES = ['https://www.googleapis.com/auth/calendar.events'];
export const CALENDAR_ID = 'primary';
export const BUSINESS_OS_SOURCE_MARKER = 'business-os';

export interface GoogleCalendarEventInput {
  meetingId: string;
  title: string;
  description?: string | null;
  location?: string | null;
  startAt: Date;
  endAt: Date;
  attendeeEmails: string[];
}

export interface GoogleCalendarChangedEvent {
  id: string;
  status?: string | null;
  summary?: string | null;
  description?: string | null;
  location?: string | null;
  start?: { dateTime?: string | null; date?: string | null } | null;
  end?: { dateTime?: string | null; date?: string | null } | null;
  attendees?: { email?: string | null }[] | null;
  extendedProperties?: { private?: Record<string, string> | null } | null;
}

/** Тунукмиёнаи googleapis барои Calendar — тамоми муомила бо Google дар ин файл ҷамъ шудааст, то processor-ҳо бе занги ҳақиқӣ ба API тест шаванд. */
@Injectable()
export class GoogleCalendarService {
  private readonly logger = new Logger(GoogleCalendarService.name);

  constructor(
    private readonly config: ConfigService,
    private readonly prisma: PrismaService,
  ) {}

  private createOAuthClient(): OAuth2Client {
    return new google.auth.OAuth2(
      this.config.get<string>('google.clientId'),
      this.config.get<string>('google.clientSecret'),
      `${this.config.get<string>('publicUrl')}/calendar/connect/google/callback`,
    );
  }

  getAuthUrl(state: string): string {
    return this.createOAuthClient().generateAuthUrl({
      access_type: 'offline',
      prompt: 'consent',
      scope: CALENDAR_SCOPES,
      state,
    });
  }

  async exchangeCode(code: string) {
    const client = this.createOAuthClient();
    const { tokens } = await client.getToken(code);
    return tokens;
  }

  /** Мизоҷи OAuth2-и авторизатсияшуда месозад; агар пайвастшавӣ мавҷуд набошад, null бармегардонад. Токени тозашударо (аз event-и "tokens") худкор дар DB нигоҳ медорад. */
  async getAuthorizedClient(companyId: string, userId: string): Promise<OAuth2Client | null> {
    const connection = await this.prisma.oAuthConnection.findFirst({
      where: { companyId, userId, provider: OAuthProvider.GOOGLE_CALENDAR },
    });
    if (!connection) {
      return null;
    }

    const encryptionKey = this.config.get<string>('encryptionKey')!;
    const client = this.createOAuthClient();
    client.setCredentials({
      access_token: decryptToken(connection.encryptedAccessToken, encryptionKey),
      refresh_token: connection.encryptedRefreshToken
        ? decryptToken(connection.encryptedRefreshToken, encryptionKey)
        : undefined,
      expiry_date: connection.expiresAt?.getTime(),
    });

    client.on('tokens', (tokens) => {
      const data: { encryptedAccessToken?: string; encryptedRefreshToken?: string; expiresAt?: Date } = {};
      if (tokens.access_token) {
        data.encryptedAccessToken = encryptToken(tokens.access_token, encryptionKey);
      }
      if (tokens.refresh_token) {
        data.encryptedRefreshToken = encryptToken(tokens.refresh_token, encryptionKey);
      }
      if (tokens.expiry_date) {
        data.expiresAt = new Date(tokens.expiry_date);
      }
      if (Object.keys(data).length === 0) {
        return;
      }
      this.prisma.oAuthConnection.update({ where: { id: connection.id }, data }).catch((error: unknown) => {
        this.logger.warn(`Наметавонист токени навшударо нигоҳ дорад: ${error}`);
      });
    });

    return client;
  }

  private calendarClient(auth: OAuth2Client) {
    return google.calendar({ version: 'v3', auth });
  }

  async createEvent(auth: OAuth2Client, input: GoogleCalendarEventInput): Promise<{ id: string }> {
    const calendar = this.calendarClient(auth);
    const response = await calendar.events.insert({
      calendarId: CALENDAR_ID,
      sendUpdates: 'none',
      requestBody: this.toEventBody(input),
    });
    if (!response.data.id) {
      throw new Error('Google Calendar event ID барнагардонд');
    }
    return { id: response.data.id };
  }

  async patchEvent(auth: OAuth2Client, eventId: string, input: GoogleCalendarEventInput): Promise<void> {
    const calendar = this.calendarClient(auth);
    try {
      await calendar.events.patch({
        calendarId: CALENDAR_ID,
        eventId,
        sendUpdates: 'none',
        requestBody: this.toEventBody(input),
      });
    } catch (error) {
      if (this.isNotFoundError(error)) return;
      throw error;
    }
  }

  async deleteEvent(auth: OAuth2Client, eventId: string): Promise<void> {
    const calendar = this.calendarClient(auth);
    try {
      await calendar.events.delete({ calendarId: CALENDAR_ID, eventId, sendUpdates: 'none' });
    } catch (error) {
      if (this.isNotFoundError(error)) return;
      throw error;
    }
  }

  async watchCalendar(
    auth: OAuth2Client,
    channelId: string,
    channelToken: string,
  ): Promise<{ resourceId: string; expiration: Date }> {
    const calendar = this.calendarClient(auth);
    const webhookUrl = `${this.config.get<string>('publicUrl')}/webhooks/google-calendar`;
    const response = await calendar.events.watch({
      calendarId: CALENDAR_ID,
      requestBody: { id: channelId, type: 'web_hook', address: webhookUrl, token: channelToken },
    });
    if (!response.data.resourceId || !response.data.expiration) {
      throw new Error('Google watch channel маълумоти пурра барнагардонд');
    }
    return { resourceId: response.data.resourceId, expiration: new Date(Number(response.data.expiration)) };
  }

  async stopChannel(auth: OAuth2Client, channelId: string, resourceId: string): Promise<void> {
    const calendar = this.calendarClient(auth);
    try {
      await calendar.channels.stop({ requestBody: { id: channelId, resourceId } });
    } catch (error) {
      this.logger.warn(`Бастани channel ${channelId} муваффақ нашуд (эҳтимол аллакай гузаштааст): ${error}`);
    }
  }

  /**
   * Синхронизатсияи қадамӣ (incremental sync). Агар syncToken надошта бошем — full baseline (танҳо рӯйдодҳои оянда).
   * Агар Google 410 (syncToken беэътибор) баргардонад, худкор бо full baseline аз нав сар мешавад.
   */
  async listChangedEvents(
    auth: OAuth2Client,
    syncToken: string | null,
  ): Promise<{ events: GoogleCalendarChangedEvent[]; nextSyncToken: string }> {
    const calendar = this.calendarClient(auth);
    const events: GoogleCalendarChangedEvent[] = [];
    let pageToken: string | undefined;
    let nextSyncToken: string | undefined;

    try {
      do {
        const response = await calendar.events.list({
          calendarId: CALENDAR_ID,
          showDeleted: true,
          singleEvents: true,
          pageToken,
          ...(syncToken ? { syncToken } : { timeMin: new Date().toISOString() }),
        });
        events.push(...((response.data.items ?? []) as GoogleCalendarChangedEvent[]));
        pageToken = response.data.nextPageToken ?? undefined;
        nextSyncToken = response.data.nextSyncToken ?? nextSyncToken;
      } while (pageToken);
    } catch (error) {
      if (this.isGoneError(error) && syncToken) {
        return this.listChangedEvents(auth, null);
      }
      throw error;
    }

    return { events, nextSyncToken: nextSyncToken ?? syncToken ?? '' };
  }

  isBusinessOsEvent(event: GoogleCalendarChangedEvent): boolean {
    return event.extendedProperties?.private?.source === BUSINESS_OS_SOURCE_MARKER;
  }

  private toEventBody(input: GoogleCalendarEventInput): calendar_v3.Schema$Event {
    return {
      summary: input.title,
      description: input.description ?? undefined,
      location: input.location ?? undefined,
      start: { dateTime: input.startAt.toISOString() },
      end: { dateTime: input.endAt.toISOString() },
      attendees: input.attendeeEmails.map((email) => ({ email })),
      extendedProperties: { private: { source: BUSINESS_OS_SOURCE_MARKER, meetingId: input.meetingId } },
    };
  }

  private isNotFoundError(error: unknown): boolean {
    const code = (error as { code?: number })?.code;
    return code === 404 || code === 410;
  }

  private isGoneError(error: unknown): boolean {
    const code = (error as { code?: number })?.code;
    return code === 410;
  }
}
