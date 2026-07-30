import { Processor, WorkerHost } from '@nestjs/bullmq';
import { Logger } from '@nestjs/common';
import { Channel, Direction, MessageStatus } from '@prisma/client';
import type { Job } from 'bullmq';
import { PrismaService } from '../../prisma/prisma.service';
import { QUEUES } from '../../queue/queue.constants';
import { NotificationsGateway } from '../notifications/notifications.gateway';
import { IncomingWhatsappMessage } from './whatsapp-webhook.util';

@Processor(QUEUES.WHATSAPP_INCOMING)
export class WhatsappProcessor extends WorkerHost {
  private readonly logger = new Logger(WhatsappProcessor.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly notifications: NotificationsGateway,
  ) {
    super();
  }

  async process(job: Job<IncomingWhatsappMessage>) {
    const { providerMessageId, phoneNumberId, from, text, timestamp } = job.data;

    const existing = await this.prisma.communication.findUnique({ where: { providerMessageId } });
    if (existing) {
      this.logger.debug(`Duplicate WhatsApp message skipped: ${providerMessageId}`);
      return;
    }

    const account = await this.prisma.whatsAppAccount.findUnique({ where: { phoneNumberId } });
    if (!account) {
      this.logger.warn(`Unknown WhatsApp phoneNumberId: ${phoneNumberId}`);
      return;
    }

    let contact = await this.prisma.contact.findFirst({
      where: { companyId: account.companyId, phone: from },
    });
    if (!contact) {
      contact = await this.prisma.contact.create({
        data: { companyId: account.companyId, firstName: from, phone: from },
      });
    }

    const communication = await this.prisma.communication.create({
      data: {
        companyId: account.companyId,
        contactId: contact.id,
        channel: Channel.WHATSAPP,
        direction: Direction.INBOUND,
        content: text,
        providerMessageId,
        status: MessageStatus.RECEIVED,
        timestamp: new Date(timestamp * 1000),
      },
    });

    this.notifications.emitToCompany(account.companyId, 'whatsapp-message', communication);
  }
}
