import { Module } from '@nestjs/common';
import { BullModule } from '@nestjs/bullmq';
import { QUEUES } from '../../queue/queue.constants';
import { NotificationsModule } from '../notifications/notifications.module';
import { AiController } from './ai.controller';
import { AiService } from './ai.service';
import { AiSummaryProcessor } from './ai-summary.processor';

@Module({
  imports: [BullModule.registerQueue({ name: QUEUES.AI_SUMMARIZE }), NotificationsModule],
  controllers: [AiController],
  providers: [AiService, AiSummaryProcessor],
  exports: [AiService],
})
export class AiModule {}
