import { Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { OnGatewayConnection, WebSocketGateway, WebSocketServer } from '@nestjs/websockets';
import type { Server, Socket } from 'socket.io';

@WebSocketGateway({
  cors: { origin: process.env.CORS_ORIGIN ?? 'http://localhost:5173', credentials: true },
})
export class NotificationsGateway implements OnGatewayConnection {
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
      const payload = this.jwt.verify<{ companyId: string }>(token, {
        secret: this.config.get<string>('jwt.accessSecret'),
      });
      client.join(`company:${payload.companyId}`);
    } catch {
      this.logger.warn('WebSocket connection rejected: invalid token');
      client.disconnect();
    }
  }

  emitToCompany(companyId: string, event: string, payload: unknown) {
    this.server.to(`company:${companyId}`).emit(event, payload);
  }
}
