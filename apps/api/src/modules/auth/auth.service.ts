import { ConflictException, Injectable, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { Role, User, UserStatus } from '@prisma/client';
import * as argon2 from 'argon2';
import { randomUUID } from 'node:crypto';
import { PrismaService } from '../../prisma/prisma.service';
import { RegisterDto } from './dto/register.dto';
import { LoginDto } from './dto/login.dto';

const ACCESS_EXPIRES_IN = '15m';
const REFRESH_EXPIRES_IN = '30d';
const REFRESH_EXPIRES_IN_MS = 30 * 24 * 60 * 60 * 1000;

@Injectable()
export class AuthService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly jwt: JwtService,
    private readonly config: ConfigService,
  ) {}

  async register(dto: RegisterDto) {
    const existing = await this.prisma.user.findUnique({ where: { email: dto.email } });
    if (existing) {
      throw new ConflictException('Ин почтаи электронӣ аллакай истифода шудааст');
    }

    const passwordHash = await argon2.hash(dto.password, { type: argon2.argon2id });

    const user = await this.prisma.$transaction(
      async (tx) => {
        const company = await tx.company.create({ data: { name: dto.companyName } });
        return tx.user.create({
          data: {
            companyId: company.id,
            name: dto.name,
            email: dto.email,
            passwordHash,
            role: Role.OWNER,
          },
        });
      },
      { maxWait: 15000, timeout: 15000 },
    );

    return this.issueTokens(user);
  }

  async login(dto: LoginDto) {
    const user = await this.prisma.user.findUnique({ where: { email: dto.email } });
    if (!user || !user.passwordHash) {
      throw new UnauthorizedException('Email ё парол нодуруст аст');
    }
    if (user.status === UserStatus.SUSPENDED) {
      throw new UnauthorizedException('Аккаунти шумо бастааст');
    }

    const valid = await argon2.verify(user.passwordHash, dto.password);
    if (!valid) {
      throw new UnauthorizedException('Email ё парол нодуруст аст');
    }

    await this.prisma.user.update({ where: { id: user.id }, data: { lastLoginAt: new Date() } });

    return this.issueTokens(user);
  }

  async refresh(rawToken: string) {
    let payload: { sub: string; jti: string };
    try {
      payload = this.jwt.verify(rawToken, { secret: this.config.get<string>('jwt.refreshSecret') });
    } catch {
      throw new UnauthorizedException('Refresh token нодуруст ё муддаташ гузаштааст');
    }

    const record = await this.prisma.refreshToken.findUnique({ where: { id: payload.jti } });
    if (!record || record.revoked || record.expiresAt < new Date()) {
      throw new UnauthorizedException('Refresh token эътибор надорад');
    }

    const valid = await argon2.verify(record.tokenHash, rawToken);
    if (!valid) {
      throw new UnauthorizedException('Refresh token нодуруст аст');
    }

    await this.prisma.refreshToken.update({ where: { id: record.id }, data: { revoked: true } });

    const user = await this.prisma.user.findUnique({ where: { id: record.userId } });
    if (!user) {
      throw new UnauthorizedException('Корбар ёфт нашуд');
    }

    return this.issueTokens(user);
  }

  async logout(rawToken: string | undefined) {
    if (!rawToken) return;
    const payload = this.jwt.decode(rawToken) as { jti?: string } | null;
    if (payload?.jti) {
      await this.prisma.refreshToken.updateMany({ where: { id: payload.jti }, data: { revoked: true } });
    }
  }

  private async issueTokens(user: User) {
    const accessToken = this.jwt.sign(
      { sub: user.id, companyId: user.companyId, role: user.role },
      { secret: this.config.get<string>('jwt.accessSecret'), expiresIn: ACCESS_EXPIRES_IN },
    );

    const tokenId = randomUUID();
    const refreshToken = this.jwt.sign(
      { sub: user.id, jti: tokenId },
      { secret: this.config.get<string>('jwt.refreshSecret'), expiresIn: REFRESH_EXPIRES_IN },
    );
    const tokenHash = await argon2.hash(refreshToken, { type: argon2.argon2id });

    await this.prisma.refreshToken.create({
      data: {
        id: tokenId,
        userId: user.id,
        tokenHash,
        expiresAt: new Date(Date.now() + REFRESH_EXPIRES_IN_MS),
      },
    });

    return { accessToken, refreshToken, user: this.toPublicUser(user) };
  }

  private toPublicUser(user: User) {
    const { passwordHash: _passwordHash, ...rest } = user;
    return rest;
  }
}
