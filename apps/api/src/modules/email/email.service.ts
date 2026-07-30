import { BadRequestException, Injectable } from '@nestjs/common';
import { OAuthProvider } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { GmailService } from './gmail.service';
import { OutlookService } from './outlook.service';
import { SendEmailDto } from './dto/send-email.dto';

@Injectable()
export class EmailService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly gmailService: GmailService,
    private readonly outlookService: OutlookService,
  ) {}

  async send(companyId: string, userId: string, dto: SendEmailDto) {
    const connection = await this.prisma.oAuthConnection.findFirst({
      where: { companyId, userId, provider: { in: [OAuthProvider.GOOGLE, OAuthProvider.MICROSOFT] } },
      orderBy: { updatedAt: 'desc' },
    });
    if (!connection) {
      throw new BadRequestException('Ягон email провайдер барои ин корбар пайваст нашудааст');
    }

    if (connection.provider === OAuthProvider.MICROSOFT) {
      return this.outlookService.send(companyId, userId, dto);
    }
    return this.gmailService.send(companyId, userId, dto);
  }
}
