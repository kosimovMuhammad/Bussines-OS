import { Module } from '@nestjs/common';
import { JwtModule } from '@nestjs/jwt';
import { EmailController } from './email.controller';
import { GmailService } from './gmail.service';

@Module({
  imports: [JwtModule.register({})],
  controllers: [EmailController],
  providers: [GmailService],
})
export class EmailModule {}
