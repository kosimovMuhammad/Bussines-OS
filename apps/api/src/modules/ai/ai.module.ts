import { Module } from '@nestjs/common';
import { BullModule } from '@nestjs/bullmq';
import { QUEUES } from '../../queue/queue.constants';
import { NotificationsModule } from '../notifications/notifications.module';
import { S3Module } from '../../common/s3/s3.module';
import { FilesModule } from '../files/files.module';
import { AiController } from './ai.controller';
import { AiService } from './ai.service';
import { AiSummaryProcessor } from './ai-summary.processor';
import { AiQueryProcessor } from './ai-query.processor';
import { AiDraftEmailProcessor } from './ai-draft-email.processor';
import { AiQuotationProcessor } from './ai-quotation.processor';

@Module({
  imports: [
    BullModule.registerQueue(
      { name: QUEUES.AI_SUMMARIZE },
      { name: QUEUES.AI_QUERY },
      { name: QUEUES.AI_DRAFT_EMAIL },
      { name: QUEUES.AI_QUOTATION },
    ),
    NotificationsModule,
    S3Module,
    FilesModule,
  ],
  controllers: [AiController],
  providers: [AiService, AiSummaryProcessor, AiQueryProcessor, AiDraftEmailProcessor, AiQuotationProcessor],
  exports: [AiService],
})
export class AiModule {}
