import { BadGatewayException, BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { Channel, Direction, MessageStatus } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { decryptToken, encryptToken } from '../../common/crypto/token-cipher';
import { ConnectWhatsappAccountDto } from './dto/connect-account.dto';
import { SendWhatsappDto } from './dto/send-whatsapp.dto';

const GRAPH_API_VERSION = 'v20.0';

@Injectable()
export class WhatsappService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly config: ConfigService,
  ) {}

  async listAccounts(companyId: string) {
    const accounts = await this.prisma.whatsAppAccount.findMany({ where: { companyId } });
    return accounts.map(({ encryptedAccessToken: _encryptedAccessToken, ...rest }) => rest);
  }

  async connectAccount(companyId: string, dto: ConnectWhatsappAccountDto) {
    const encryptionKey = this.config.get<string>('encryptionKey')!;
    const encryptedAccessToken = encryptToken(dto.accessToken, encryptionKey);

    const account = await this.prisma.whatsAppAccount.upsert({
      where: { phoneNumberId: dto.phoneNumberId },
      create: {
        companyId,
        phoneNumberId: dto.phoneNumberId,
        wabaId: dto.wabaId,
        encryptedAccessToken,
        verifiedName: dto.verifiedName,
      },
      update: {
        wabaId: dto.wabaId,
        encryptedAccessToken,
        verifiedName: dto.verifiedName,
      },
    });

    const { encryptedAccessToken: _encryptedAccessToken, ...rest } = account;
    return rest;
  }

  async send(companyId: string, dto: SendWhatsappDto) {
    const contact = await this.prisma.contact.findFirst({ where: { id: dto.contactId, companyId } });
    if (!contact) {
      throw new NotFoundException('Contact ёфт нашуд');
    }
    if (!contact.phone) {
      throw new BadRequestException('Ин contact рақами телефон надорад');
    }

    const account = await this.prisma.whatsAppAccount.findFirst({ where: { companyId } });
    if (!account) {
      throw new BadRequestException('WhatsApp барои ин company пайваст нашудааст');
    }

    const encryptionKey = this.config.get<string>('encryptionKey')!;
    const accessToken = decryptToken(account.encryptedAccessToken, encryptionKey);

    let response: Response;
    try {
      response = await fetch(`https://graph.facebook.com/${GRAPH_API_VERSION}/${account.phoneNumberId}/messages`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${accessToken}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          messaging_product: 'whatsapp',
          to: contact.phone,
          type: 'text',
          text: { body: dto.message },
        }),
      });
    } catch {
      throw new BadGatewayException('Пайвастшавӣ ба WhatsApp API имконнопазир аст');
    }

    const data: { messages?: { id: string }[]; error?: { message?: string } } | null = await response
      .json()
      .catch(() => null);

    if (!response.ok) {
      await this.prisma.communication.create({
        data: {
          companyId,
          contactId: contact.id,
          channel: Channel.WHATSAPP,
          direction: Direction.OUTBOUND,
          content: dto.message,
          status: MessageStatus.FAILED,
        },
      });
      throw new BadGatewayException(data?.error?.message ?? 'WhatsApp паёмро қабул накард');
    }

    return this.prisma.communication.create({
      data: {
        companyId,
        contactId: contact.id,
        channel: Channel.WHATSAPP,
        direction: Direction.OUTBOUND,
        content: dto.message,
        status: MessageStatus.SENT,
        providerMessageId: data?.messages?.[0]?.id,
      },
    });
  }
}
