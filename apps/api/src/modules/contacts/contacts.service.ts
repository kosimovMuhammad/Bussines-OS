import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { Prisma } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { AuditLogService } from '../audit-log/audit-log.service';
import { CreateContactDto } from './dto/create-contact.dto';
import { UpdateContactDto } from './dto/update-contact.dto';
import { QueryContactsDto } from './dto/query-contacts.dto';

@Injectable()
export class ContactsService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly auditLog: AuditLogService,
  ) {}

  async findAll(companyId: string, query: QueryContactsDto) {
    const page = query.page ?? 1;
    const limit = query.limit ?? 20;

    const where: Prisma.ContactWhereInput = { companyId };
    if (query.search) {
      where.OR = [
        { firstName: { contains: query.search, mode: 'insensitive' } },
        { lastName: { contains: query.search, mode: 'insensitive' } },
        { email: { contains: query.search, mode: 'insensitive' } },
        { phone: { contains: query.search, mode: 'insensitive' } },
        { companyName: { contains: query.search, mode: 'insensitive' } },
      ];
    }
    if (query.tag) {
      where.tags = { has: query.tag };
    }
    if (query.ownerId) {
      where.ownerId = query.ownerId;
    }

    const [data, total] = await this.prisma.$transaction([
      this.prisma.contact.findMany({
        where,
        skip: (page - 1) * limit,
        take: limit,
        orderBy: { createdAt: 'desc' },
      }),
      this.prisma.contact.count({ where }),
    ]);

    return { data, meta: { total, page, limit } };
  }

  async findOne(companyId: string, id: string) {
    const contact = await this.prisma.contact.findFirst({
      where: { id, companyId },
      include: {
        deals: true,
        tasks: true,
        communications: {
          include: { aiSummary: true },
          orderBy: { timestamp: 'desc' },
        },
      },
    });
    if (!contact) {
      throw new NotFoundException('Contact ёфт нашуд');
    }
    return contact;
  }

  async timeline(companyId: string, id: string) {
    await this.ensureExists(companyId, id);
    return this.prisma.communication.findMany({
      where: { contactId: id, companyId },
      include: { aiSummary: true },
      orderBy: { timestamp: 'asc' },
    });
  }

  async create(companyId: string, dto: CreateContactDto) {
    if (dto.ownerId) {
      await this.assertOwnerInCompany(companyId, dto.ownerId);
    }
    return this.prisma.contact.create({ data: { ...dto, companyId } });
  }

  async update(companyId: string, id: string, dto: UpdateContactDto) {
    await this.ensureExists(companyId, id);
    if (dto.ownerId) {
      await this.assertOwnerInCompany(companyId, dto.ownerId);
    }
    return this.prisma.contact.update({ where: { id }, data: dto });
  }

  async remove(companyId: string, id: string, actorId: string) {
    await this.ensureExists(companyId, id);
    await this.prisma.contact.delete({ where: { id } });
    await this.auditLog.log({ companyId, userId: actorId, action: 'contact.deleted', entity: 'Contact', entityId: id });
    return { success: true };
  }

  private async ensureExists(companyId: string, id: string) {
    const contact = await this.prisma.contact.findFirst({ where: { id, companyId } });
    if (!contact) {
      throw new NotFoundException('Contact ёфт нашуд');
    }
    return contact;
  }

  private async assertOwnerInCompany(companyId: string, ownerId: string) {
    const owner = await this.prisma.user.findFirst({ where: { id: ownerId, companyId } });
    if (!owner) {
      throw new BadRequestException('ownerId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }
}
