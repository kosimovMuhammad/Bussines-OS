import { Injectable } from '@nestjs/common';
import { NotificationsGateway } from './notifications.gateway';

@Injectable()
export class NotificationsService {
  constructor(private readonly gateway: NotificationsGateway) {}

  emitToCompany(companyId: string, event: string, payload: unknown): void {
    this.gateway.emitToCompany(companyId, event, payload);
  }
}
