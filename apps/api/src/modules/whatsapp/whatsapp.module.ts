import { Module } from '@nestjs/common';
import { BullModule } from '@nestjs/bullmq';
import { QUEUES } from '../../queue/queue.constants';
import { NotificationsModule } from '../notifications/notifications.module';
import { WhatsappController } from './whatsapp.controller';
import { WhatsappWebhookController } from './whatsapp-webhook.controller';
import { WhatsappService } from './whatsapp.service';
import { WhatsappProcessor } from './whatsapp.processor';

@Module({
  imports: [BullModule.registerQueue({ name: QUEUES.WHATSAPP_INCOMING }), NotificationsModule],
  controllers: [WhatsappController, WhatsappWebhookController],
  providers: [WhatsappService, WhatsappProcessor],
})
export class WhatsappModule {}
