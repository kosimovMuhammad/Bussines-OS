import { BadGatewayException, BadRequestException, Injectable, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { google } from 'googleapis';
import { Channel, Direction, MessageStatus, OAuthProvider } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { decryptToken, encryptToken } from '../../common/crypto/token-cipher';
import { NotificationsService } from '../notifications/notifications.service';
import { NOTIFICATION_EVENTS } from '../notifications/notifications.constants';
import { SendEmailDto } from './dto/send-email.dto';

const GMAIL_SCOPES = [
  'https://www.googleapis.com/auth/gmail.readonly',
  'https://www.googleapis.com/auth/gmail.send',
  'https://www.googleapis.com/auth/gmail.modify',
];

interface StatePayload {
  sub: string;
  companyId: string;
}

@Injectable()
export class GmailService {
  constructor(
    private readonly config: ConfigService,
    private readonly prisma: PrismaService,
    private readonly jwt: JwtService,
    private readonly notifications: NotificationsService,
  ) {}

  private createOAuthClient() {
    return new google.auth.OAuth2(
      this.config.get<string>('google.clientId'),
      this.config.get<string>('google.clientSecret'),
      `${this.config.get<string>('publicUrl')}/email/connect/gmail/callback`,
    );
  }

  getAuthUrl(userId: string, companyId: string): string {
    const state = this.jwt.sign(
      { sub: userId, companyId } satisfies StatePayload,
      { secret: this.config.get<string>('jwt.accessSecret'), expiresIn: '10m' },
    );

    return this.createOAuthClient().generateAuthUrl({
      access_type: 'offline',
      prompt: 'consent',
      scope: GMAIL_SCOPES,
      state,
    });
  }

  async handleCallback(code: string, state: string) {
    let payload: StatePayload;
    try {
      payload = this.jwt.verify<StatePayload>(state, { secret: this.config.get<string>('jwt.accessSecret') });
    } catch {
      throw new UnauthorizedException('state нодуруст ё муддаташ гузаштааст');
    }

    const client = this.createOAuthClient();
    let tokens: { access_token?: string | null; refresh_token?: string | null; scope?: string; expiry_date?: number | null };
    try {
      const tokenResponse = await client.getToken(code);
      tokens = tokenResponse.tokens;
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
      where: { userId_provider: { userId: payload.sub, provider: OAuthProvider.GOOGLE } },
      create: {
        companyId: payload.companyId,
        userId: payload.sub,
        provider: OAuthProvider.GOOGLE,
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

    return { success: true, provider: 'google' as const };
  }

  async send(companyId: string, userId: string, dto: SendEmailDto) {
    const connection = await this.prisma.oAuthConnection.findFirst({
      where: { companyId, userId, provider: OAuthProvider.GOOGLE },
    });
    if (!connection) {
      throw new BadRequestException('Gmail барои ин корбар пайваст нашудааст');
    }

    const contact = await this.prisma.contact.findFirst({ where: { id: dto.contactId, companyId } });
    if (!contact?.email) {
      throw new BadRequestException('Contact email надорад');
    }

    const encryptionKey = this.config.get<string>('encryptionKey')!;
    const client = this.createOAuthClient();
    client.setCredentials({
      access_token: decryptToken(connection.encryptedAccessToken, encryptionKey),
      refresh_token: connection.encryptedRefreshToken
        ? decryptToken(connection.encryptedRefreshToken, encryptionKey)
        : undefined,
    });

    const gmail = google.gmail({ version: 'v1', auth: client });
    const raw = this.buildRawMessage(contact.email, dto.subject, dto.body);

    let response;
    try {
      response = await gmail.users.messages.send({ userId: 'me', requestBody: { raw } });
    } catch (err) {
      await this.prisma.communication.create({
        data: {
          companyId,
          contactId: contact.id,
          channel: Channel.EMAIL,
          direction: Direction.OUTBOUND,
          content: `${dto.subject}\n\n${dto.body}`,
          status: MessageStatus.FAILED,
        },
      });
      const message = err instanceof Error ? err.message : 'Gmail паёмро нафиристод';
      throw new BadGatewayException(message);
    }

    const communication = await this.prisma.communication.create({
      data: {
        companyId,
        contactId: contact.id,
        channel: Channel.EMAIL,
        direction: Direction.OUTBOUND,
        content: `${dto.subject}\n\n${dto.body}`,
        status: MessageStatus.SENT,
        providerMessageId: response.data.id ?? undefined,
      },
    });

    this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.EMAIL_NEW, communication);

    return communication;
  }

  private buildRawMessage(to: string, subject: string, body: string): string {
    const message = [`To: ${to}`, `Subject: ${subject}`, 'Content-Type: text/plain; charset=utf-8', '', body].join(
      '\r\n',
    );
    return Buffer.from(message).toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  }
}
