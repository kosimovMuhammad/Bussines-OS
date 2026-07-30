import { ConflictException, Injectable, NotFoundException } from '@nestjs/common';
import { Role, User, UserStatus } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { AuditLogService } from '../audit-log/audit-log.service';
import { InviteUserDto } from './dto/invite-user.dto';
import { UpdateUserDto } from './dto/update-user.dto';

@Injectable()
export class UsersService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly auditLog: AuditLogService,
  ) {}

  async findAll(companyId: string) {
    const users = await this.prisma.user.findMany({
      where: { companyId },
      orderBy: { createdAt: 'asc' },
    });
    return users.map((user) => this.toPublic(user));
  }

  async findOne(companyId: string, id: string) {
    const user = await this.prisma.user.findFirst({ where: { id, companyId } });
    if (!user) {
      throw new NotFoundException('Корбар ёфт нашуд');
    }
    return this.toPublic(user);
  }

  async invite(companyId: string, dto: InviteUserDto) {
    const existing = await this.prisma.user.findUnique({ where: { email: dto.email } });
    if (existing) {
      throw new ConflictException('Ин почтаи электронӣ аллакай истифода шудааст');
    }

    const user = await this.prisma.user.create({
      data: {
        companyId,
        name: dto.name,
        email: dto.email,
        role: dto.role ?? Role.EMPLOYEE,
        status: UserStatus.INVITED,
      },
    });
    return this.toPublic(user);
  }

  async update(companyId: string, id: string, dto: UpdateUserDto) {
    await this.findOne(companyId, id);
    const user = await this.prisma.user.update({ where: { id }, data: dto });
    return this.toPublic(user);
  }

  async updateRole(companyId: string, id: string, role: Role, actorId: string) {
    const previous = await this.findOne(companyId, id);
    const user = await this.prisma.user.update({ where: { id }, data: { role } });
    await this.auditLog.log({
      companyId,
      userId: actorId,
      action: 'user.role_changed',
      entity: 'User',
      entityId: id,
      metadata: { previousRole: previous.role, newRole: role },
    });
    return this.toPublic(user);
  }

  async remove(companyId: string, id: string, actorId: string) {
    await this.findOne(companyId, id);
    await this.prisma.user.delete({ where: { id } });
    await this.auditLog.log({ companyId, userId: actorId, action: 'user.deleted', entity: 'User', entityId: id });
    return { success: true };
  }

  private toPublic(user: User) {
    const { passwordHash: _passwordHash, ...rest } = user;
    return rest;
  }
}
