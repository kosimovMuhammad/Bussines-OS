import { CanActivate, ExecutionContext, Injectable, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { createHmac, timingSafeEqual } from 'node:crypto';
import type { Request } from 'express';

@Injectable()
export class WhatsappSignatureGuard implements CanActivate {
  constructor(private readonly config: ConfigService) {}

  canActivate(context: ExecutionContext): boolean {
    const req = context.switchToHttp().getRequest<Request & { rawBody?: Buffer }>();
    const signature = req.headers['x-hub-signature-256'] as string | undefined;
    const appSecret = this.config.get<string>('whatsapp.appSecret');

    if (!signature || !appSecret || !req.rawBody) {
      throw new UnauthorizedException('Имзои webhook нодуруст аст');
    }

    const expected = 'sha256=' + createHmac('sha256', appSecret).update(req.rawBody).digest('hex');
    const signatureBuf = Buffer.from(signature);
    const expectedBuf = Buffer.from(expected);

    if (signatureBuf.length !== expectedBuf.length || !timingSafeEqual(signatureBuf, expectedBuf)) {
      throw new UnauthorizedException('Имзои webhook нодуруст аст');
    }

    return true;
  }
}
