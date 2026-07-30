import { BadGatewayException, BadRequestException, Injectable, InternalServerErrorException, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { ConfidentialClientApplication, type AuthenticationResult } from '@azure/msal-node';
import { Channel, Direction, MessageStatus, OAuthProvider } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { decryptToken, encryptToken } from '../../common/crypto/token-cipher';
import { NotificationsService } from '../notifications/notifications.service';
import { NOTIFICATION_EVENTS } from '../notifications/notifications.constants';
import { SendEmailDto } from './dto/send-email.dto';

const OUTLOOK_SCOPES = [
  'https://graph.microsoft.com/Mail.Read',
  'https://graph.microsoft.com/Mail.Send',
  'offline_access',
];

interface StatePayload {
  sub: string;
  companyId: string;
}

interface OutlookCachePayload {
  homeAccountId: string;
  cache: string;
}

@Injectable()
export class OutlookService {
  constructor(
    private readonly config: ConfigService,
    private readonly prisma: PrismaService,
    private readonly jwt: JwtService,
    private readonly notifications: NotificationsService,
  ) {}

  private createMsalClient(): ConfidentialClientApplication {
    const clientId = this.config.get<string>('microsoft.clientId');
    const clientSecret = this.config.get<string>('microsoft.clientSecret');
    if (!clientId || !clientSecret) {
      throw new InternalServerErrorException(
        'MICROSOFT_CLIENT_ID/MICROSOFT_CLIENT_SECRET танзим нашудаанд — Outlook пайвастшавӣ имконнопазир аст',
      );
    }

    return new ConfidentialClientApplication({
      auth: {
        clientId,
        clientSecret,
        authority: `https://login.microsoftonline.com/${this.config.get<string>('microsoft.tenantId') ?? 'common'}`,
      },
    });
  }

  private getRedirectUri(): string {
    const redirectUri = this.config.get<string>('microsoft.redirectUri');
    if (!redirectUri) {
      throw new InternalServerErrorException(
        'MICROSOFT_REDIRECT_URI танзим нашудааст — Outlook пайвастшавӣ имконнопазир аст',
      );
    }
    return redirectUri;
  }

  async getAuthUrl(userId: string, companyId: string): Promise<string> {
    const state = this.jwt.sign(
      { sub: userId, companyId } satisfies StatePayload,
      { secret: this.config.get<string>('jwt.accessSecret'), expiresIn: '10m' },
    );

    return this.createMsalClient().getAuthCodeUrl({
      scopes: OUTLOOK_SCOPES,
      redirectUri: this.getRedirectUri(),
      state,
      prompt: 'consent',
    });
  }

  async handleCallback(code: string, state: string) {
    let payload: StatePayload;
    try {
      payload = this.jwt.verify<StatePayload>(state, { secret: this.config.get<string>('jwt.accessSecret') });
    } catch {
      throw new UnauthorizedException('state нодуруст ё муддаташ гузаштааст');
    }

    const client = this.createMsalClient();
    let result: AuthenticationResult | null;
    try {
      result = await client.acquireTokenByCode({
        code,
        scopes: OUTLOOK_SCOPES,
        redirectUri: this.getRedirectUri(),
      });
    } catch {
      throw new BadGatewayException('Мубодилаи code бо Microsoft муваффақ нашуд');
    }

    if (!result?.accessToken || !result.account) {
      throw new BadGatewayException('Microsoft access token барнагардонд');
    }

    const encryptionKey = this.config.get<string>('encryptionKey')!;
    const encryptedAccessToken = encryptToken(result.accessToken, encryptionKey);
    const cachePayload: OutlookCachePayload = {
      homeAccountId: result.account.homeAccountId,
      cache: client.getTokenCache().serialize(),
    };
    const encryptedRefreshToken = encryptToken(JSON.stringify(cachePayload), encryptionKey);

    await this.prisma.oAuthConnection.upsert({
      where: { userId_provider: { userId: payload.sub, provider: OAuthProvider.MICROSOFT } },
      create: {
        companyId: payload.companyId,
        userId: payload.sub,
        provider: OAuthProvider.MICROSOFT,
        encryptedAccessToken,
        encryptedRefreshToken,
        scope: result.scopes.join(' '),
        expiresAt: result.expiresOn ?? undefined,
      },
      update: {
        encryptedAccessToken,
        encryptedRefreshToken,
        scope: result.scopes.join(' '),
        expiresAt: result.expiresOn ?? undefined,
      },
    });

    return { success: true, provider: 'microsoft' as const };
  }

  async send(companyId: string, userId: string, dto: SendEmailDto) {
    const connection = await this.prisma.oAuthConnection.findFirst({
      where: { companyId, userId, provider: OAuthProvider.MICROSOFT },
    });
    if (!connection?.encryptedRefreshToken) {
      throw new BadRequestException('Outlook барои ин корбар пайваст нашудааст');
    }

    const contact = await this.prisma.contact.findFirst({ where: { id: dto.contactId, companyId } });
    if (!contact?.email) {
      throw new BadRequestException('Contact email надорад');
    }

    const encryptionKey = this.config.get<string>('encryptionKey')!;
    const cachePayload = JSON.parse(
      decryptToken(connection.encryptedRefreshToken, encryptionKey),
    ) as OutlookCachePayload;

    const client = this.createMsalClient();
    client.getTokenCache().deserialize(cachePayload.cache);

    const account = await client.getTokenCache().getAccountByHomeId(cachePayload.homeAccountId);
    if (!account) {
      throw new BadRequestException('Outlook пайвастшавӣ куҳна шудааст, лутфан дубора пайваст кунед');
    }

    let result: AuthenticationResult | null;
    try {
      result = await client.acquireTokenSilent({ account, scopes: OUTLOOK_SCOPES });
    } catch {
      throw new BadGatewayException('Навсозии Outlook access token муваффақ нашуд, лутфан дубора пайваст кунед');
    }
    if (!result?.accessToken) {
      throw new BadGatewayException('Outlook access token гирифта нашуд');
    }

    const refreshedCachePayload: OutlookCachePayload = {
      homeAccountId: account.homeAccountId,
      cache: client.getTokenCache().serialize(),
    };
    await this.prisma.oAuthConnection.update({
      where: { id: connection.id },
      data: {
        encryptedAccessToken: encryptToken(result.accessToken, encryptionKey),
        encryptedRefreshToken: encryptToken(JSON.stringify(refreshedCachePayload), encryptionKey),
        expiresAt: result.expiresOn ?? undefined,
      },
    });

    let response: Response;
    try {
      response = await fetch('https://graph.microsoft.com/v1.0/me/sendMail', {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${result.accessToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          message: {
            subject: dto.subject,
            body: { contentType: 'Text', content: dto.body },
            toRecipients: [{ emailAddress: { address: contact.email } }],
          },
          saveToSentItems: true,
        }),
      });
    } catch {
      throw new BadGatewayException('Пайвастшавӣ ба Microsoft Graph имконнопазир аст');
    }

    if (!response.ok) {
      const errorBody: { error?: { message?: string } } | null = await response.json().catch(() => null);
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
      throw new BadGatewayException(errorBody?.error?.message ?? 'Outlook паёмро нафиристод');
    }

    const communication = await this.prisma.communication.create({
      data: {
        companyId,
        contactId: contact.id,
        channel: Channel.EMAIL,
        direction: Direction.OUTBOUND,
        content: `${dto.subject}\n\n${dto.body}`,
        status: MessageStatus.SENT,
      },
    });

    this.notifications.emitToCompany(companyId, NOTIFICATION_EVENTS.EMAIL_NEW, communication);

    return communication;
  }
}
