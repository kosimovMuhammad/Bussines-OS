import { Module } from '@nestjs/common';
import { JwtModule } from '@nestjs/jwt';
import { NotificationsModule } from '../notifications/notifications.module';
import { EmailController } from './email.controller';
import { EmailService } from './email.service';
import { GmailService } from './gmail.service';
import { OutlookService } from './outlook.service';

@Module({
  imports: [JwtModule.register({}), NotificationsModule],
  controllers: [EmailController],
  providers: [EmailService, GmailService, OutlookService],
})
export class EmailModule {}
