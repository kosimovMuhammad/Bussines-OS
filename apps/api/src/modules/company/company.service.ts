import { Injectable, NotFoundException } from '@nestjs/common';
import { OAuthProvider } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { AuditLogService } from '../audit-log/audit-log.service';
import { UpdateCompanyDto } from './dto/update-company.dto';
import { UpdatePlanDto } from './dto/update-plan.dto';

@Injectable()
export class CompanyService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly auditLog: AuditLogService,
  ) {}

  async findOne(companyId: string) {
    const company = await this.prisma.company.findUnique({ where: { id: companyId } });
    if (!company) {
      throw new NotFoundException('Company ёфт нашуд');
    }
    return company;
  }

  async update(companyId: string, dto: UpdateCompanyDto, actorId: string) {
    await this.findOne(companyId);
    const company = await this.prisma.company.update({ where: { id: companyId }, data: { ...dto } });
    await this.auditLog.log({
      companyId,
      userId: actorId,
      action: 'company.updated',
      entity: 'Company',
      entityId: companyId,
      metadata: { ...dto },
    });
    return company;
  }

  async getPlan(companyId: string) {
    const company = await this.findOne(companyId);
    return { plan: company.plan };
  }

  async updatePlan(companyId: string, dto: UpdatePlanDto, actorId: string) {
    const previous = await this.findOne(companyId);
    const company = await this.prisma.company.update({ where: { id: companyId }, data: { plan: dto.plan } });
    await this.auditLog.log({
      companyId,
      userId: actorId,
      action: 'company.plan_changed',
      entity: 'Company',
      entityId: companyId,
      metadata: { previousPlan: previous.plan, newPlan: dto.plan },
    });
    return { plan: company.plan };
  }

  /** Танҳо статуси пайвастшавӣ, ҳеҷ гуна маълумоти пинҳонӣ (токенҳо) баргардонида намешавад. */
  async getIntegrationsStatus(companyId: string) {
    const [waAccount, googleConnection, microsoftConnection] = await Promise.all([
      this.prisma.whatsAppAccount.findFirst({ where: { companyId } }),
      this.prisma.oAuthConnection.findFirst({ where: { companyId, provider: OAuthProvider.GOOGLE } }),
      this.prisma.oAuthConnection.findFirst({ where: { companyId, provider: OAuthProvider.MICROSOFT } }),
    ]);

    return {
      whatsapp: { connected: !!waAccount },
      gmail: { connected: !!googleConnection },
      outlook: { connected: !!microsoftConnection },
      telegram: { connected: false, comingSoon: true },
      stripe: { connected: false, comingSoon: true },
      paypal: { connected: false, comingSoon: true },
    };
  }
}
