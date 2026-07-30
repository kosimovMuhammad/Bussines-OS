import { Body, Controller, Get, Post, Query, UseGuards } from '@nestjs/common';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { GmailService } from './gmail.service';
import { SendEmailDto } from './dto/send-email.dto';

@Controller('email')
export class EmailController {
  constructor(private readonly gmailService: GmailService) {}

  @UseGuards(JwtAuthGuard, CompanyGuard)
  @Get('connect/gmail')
  connectGmail(@CurrentUser() user: AuthenticatedUser) {
    return { url: this.gmailService.getAuthUrl(user.id, user.companyId) };
  }

  @Get('connect/gmail/callback')
  gmailCallback(@Query('code') code: string, @Query('state') state: string) {
    return this.gmailService.handleCallback(code, state);
  }

  @UseGuards(JwtAuthGuard, CompanyGuard)
  @Post('send')
  send(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser, @Body() dto: SendEmailDto) {
    return this.gmailService.send(companyId, user.id, dto);
  }
}
