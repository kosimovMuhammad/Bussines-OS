import { Injectable, NotFoundException } from '@nestjs/common';
import { Channel, Direction, MessageStatus, Prisma } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { CreateNoteDto } from './dto/create-note.dto';
import { QueryCommunicationsDto } from './dto/query-communications.dto';

@Injectable()
export class CommunicationsService {
  constructor(private readonly prisma: PrismaService) {}

  async findAll(companyId: string, query: QueryCommunicationsDto) {
    const where: Prisma.CommunicationWhereInput = { companyId };
    if (query.contactId) where.contactId = query.contactId;
    if (query.channel) where.channel = query.channel;

    return this.prisma.communication.findMany({
      where,
      include: { aiSummary: true },
      orderBy: { timestamp: 'desc' },
    });
  }

  async createNote(companyId: string, dto: CreateNoteDto) {
    await this.assertContactInCompany(companyId, dto.contactId);

    return this.prisma.communication.create({
      data: {
        companyId,
        contactId: dto.contactId,
        channel: Channel.MANUAL_NOTE,
        direction: dto.direction ?? Direction.OUTBOUND,
        content: dto.content,
        status: MessageStatus.RECEIVED,
      },
    });
  }

  private async assertContactInCompany(companyId: string, contactId: string) {
    const contact = await this.prisma.contact.findFirst({ where: { id: contactId, companyId } });
    if (!contact) {
      throw new NotFoundException('contactId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }
}
