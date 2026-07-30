import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import configuration from './config/configuration';
import { validationSchema } from './config/validation.schema';
import { PrismaModule } from './prisma/prisma.module';
import { HealthController } from './health/health.controller';
import { AuthModule } from './modules/auth/auth.module';
import { UsersModule } from './modules/users/users.module';
import { ContactsModule } from './modules/contacts/contacts.module';
import { DealsModule } from './modules/deals/deals.module';
import { TasksModule } from './modules/tasks/tasks.module';
import { TimeEntriesModule } from './modules/time-entries/time-entries.module';
import { CommunicationsModule } from './modules/communications/communications.module';
import { QueueModule } from './queue/queue.module';
import { NotificationsModule } from './modules/notifications/notifications.module';
import { WhatsappModule } from './modules/whatsapp/whatsapp.module';
import { AnalyticsModule } from './modules/analytics/analytics.module';
import { ThrottlerModule } from './throttler/throttler.module';
import { AuditLogModule } from './modules/audit-log/audit-log.module';
import { EmailModule } from './modules/email/email.module';
import { AiModule } from './modules/ai/ai.module';
import { CompanyModule } from './modules/company/company.module';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
      load: [configuration],
      validationSchema,
      validationOptions: { abortEarly: false },
    }),
    PrismaModule,
    ThrottlerModule,
    AuditLogModule,
    AuthModule,
    UsersModule,
    ContactsModule,
    DealsModule,
    TasksModule,
    TimeEntriesModule,
    CommunicationsModule,
    QueueModule,
    NotificationsModule,
    WhatsappModule,
    AnalyticsModule,
    EmailModule,
    AiModule,
    CompanyModule,
  ],
  controllers: [HealthController],
})
export class AppModule {}
