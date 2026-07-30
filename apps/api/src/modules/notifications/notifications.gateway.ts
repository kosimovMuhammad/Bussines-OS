import { Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { OnGatewayConnection, OnGatewayDisconnect, WebSocketGateway, WebSocketServer } from '@nestjs/websockets';
import { Role } from '@prisma/client';
import type { Server, Socket } from 'socket.io';

interface AccessTokenPayload {
  sub: string;
  companyId: string;
  role: Role;
}

@WebSocketGateway({
  cors: { origin: process.env.CORS_ORIGIN ?? 'http://localhost:5173', credentials: true },
})
export class NotificationsGateway implements OnGatewayConnection, OnGatewayDisconnect {
  @WebSocketServer()
  server: Server;

  private readonly logger = new Logger(NotificationsGateway.name);

  constructor(
    private readonly jwt: JwtService,
    private readonly config: ConfigService,
  ) {}

  handleConnection(client: Socket) {
    const token = (client.handshake.auth?.token ?? client.handshake.query?.token) as string | undefined;
    try {
      if (!token) throw new Error('no token');
      const payload = this.jwt.verify<AccessTokenPayload>(token, {
        secret: this.config.get<string>('jwt.accessSecret'),
      });

      client.data.userId = payload.sub;
      client.data.companyId = payload.companyId;
      client.data.role = payload.role;
      client.join(`company:${payload.companyId}`);

      this.logger.debug(`Socket ${client.id} пайваст шуд: user=${payload.sub}, company=${payload.companyId}`);
    } catch {
      this.logger.warn('WebSocket connection rejected: invalid token');
      client.disconnect();
    }
  }

  handleDisconnect(client: Socket) {
    this.logger.debug(`Socket ${client.id} қатъ шуд`);
  }

  emitToCompany(companyId: string, event: string, payload: unknown) {
    this.server.to(`company:${companyId}`).emit(event, payload);
  }
}
