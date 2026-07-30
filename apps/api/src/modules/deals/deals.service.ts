import { Injectable, NotFoundException } from '@nestjs/common';
import { DealStage, Prisma } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { AuditLogService } from '../audit-log/audit-log.service';
import { CreateDealDto } from './dto/create-deal.dto';
import { UpdateDealDto } from './dto/update-deal.dto';
import { QueryDealsDto } from './dto/query-deals.dto';

@Injectable()
export class DealsService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly auditLog: AuditLogService,
  ) {}

  async findAll(companyId: string, query: QueryDealsDto) {
    const where: Prisma.DealWhereInput = { companyId };
    if (query.stage) where.stage = query.stage;
    if (query.ownerId) where.ownerId = query.ownerId;

    return this.prisma.deal.findMany({ where, orderBy: { createdAt: 'desc' } });
  }

  async findOne(companyId: string, id: string) {
    const deal = await this.prisma.deal.findFirst({
      where: { id, companyId },
      include: { contact: true, tasks: true },
    });
    if (!deal) {
      throw new NotFoundException('Deal ёфт нашуд');
    }
    return deal;
  }

  async create(companyId: string, dto: CreateDealDto) {
    await this.assertContactInCompany(companyId, dto.contactId);
    if (dto.ownerId) {
      await this.assertOwnerInCompany(companyId, dto.ownerId);
    }

    const stage = dto.stage ?? DealStage.LEAD;

    return this.prisma.deal.create({
      data: {
        companyId,
        contactId: dto.contactId,
        title: dto.title,
        value: dto.value,
        currency: dto.currency,
        stage,
        probability: this.resolveProbability(stage, dto.probability) ?? 0,
        expectedCloseDate: dto.expectedCloseDate ? new Date(dto.expectedCloseDate) : undefined,
        ownerId: dto.ownerId,
      },
    });
  }

  async update(companyId: string, id: string, dto: UpdateDealDto) {
    await this.ensureExists(companyId, id);
    if (dto.ownerId) {
      await this.assertOwnerInCompany(companyId, dto.ownerId);
    }
    if (dto.contactId) {
      await this.assertContactInCompany(companyId, dto.contactId);
    }

    const { expectedCloseDate, ...rest } = dto;

    return this.prisma.deal.update({
      where: { id },
      data: {
        ...rest,
        ...(expectedCloseDate ? { expectedCloseDate: new Date(expectedCloseDate) } : {}),
      },
    });
  }

  async updateStage(companyId: string, id: string, stage: DealStage) {
    await this.ensureExists(companyId, id);
    const probability = this.resolveProbability(stage);

    return this.prisma.deal.update({
      where: { id },
      data: {
        stage,
        ...(probability !== undefined ? { probability } : {}),
      },
    });
  }

  async remove(companyId: string, id: string, actorId: string) {
    await this.ensureExists(companyId, id);
    await this.prisma.deal.delete({ where: { id } });
    await this.auditLog.log({ companyId, userId: actorId, action: 'deal.deleted', entity: 'Deal', entityId: id });
    return { success: true };
  }

  /** WON/LOST мантиқан probability-и муайянро талаб мекунанд; барои дигар stage-ҳо танҳо агар корбар возеҳ дода бошад иваз мешавад. */
  private resolveProbability(stage: DealStage, explicit?: number): number | undefined {
    if (stage === DealStage.WON) return 100;
    if (stage === DealStage.LOST) return 0;
    return explicit;
  }

  private async ensureExists(companyId: string, id: string) {
    const deal = await this.prisma.deal.findFirst({ where: { id, companyId } });
    if (!deal) {
      throw new NotFoundException('Deal ёфт нашуд');
    }
    return deal;
  }

  private async assertContactInCompany(companyId: string, contactId: string) {
    const contact = await this.prisma.contact.findFirst({ where: { id: contactId, companyId } });
    if (!contact) {
      throw new NotFoundException('contactId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }

  private async assertOwnerInCompany(companyId: string, ownerId: string) {
    const owner = await this.prisma.user.findFirst({ where: { id: ownerId, companyId } });
    if (!owner) {
      throw new NotFoundException('ownerId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }
}
