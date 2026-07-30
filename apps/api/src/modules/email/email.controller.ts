import { Body, Controller, Get, Post, Query, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { EmailService } from './email.service';
import { GmailService } from './gmail.service';
import { OutlookService } from './outlook.service';
import { SendEmailDto } from './dto/send-email.dto';

@ApiTags('Email')
@Controller('email')
export class EmailController {
  constructor(
    private readonly emailService: EmailService,
    private readonly gmailService: GmailService,
    private readonly outlookService: OutlookService,
  ) {}

  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard, CompanyGuard)
  @Get('connect/gmail')
  @ApiOperation({ summary: 'Get the Google OAuth consent URL for connecting Gmail' })
  @ApiResponse({ status: 200, description: 'Google OAuth consent URL' })
  connectGmail(@CurrentUser() user: AuthenticatedUser) {
    return { url: this.gmailService.getAuthUrl(user.id, user.companyId) };
  }

  @Get('connect/gmail/callback')
  @ApiOperation({ summary: 'Gmail OAuth callback — exchanges code for tokens and stores the connection' })
  @ApiResponse({ status: 200, description: 'Gmail account connected' })
  @ApiResponse({ status: 401, description: 'Invalid or expired state' })
  @ApiResponse({ status: 502, description: 'Token exchange with Google failed' })
  gmailCallback(@Query('code') code: string, @Query('state') state: string) {
    return this.gmailService.handleCallback(code, state);
  }

  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard, CompanyGuard)
  @Get('connect/outlook')
  @ApiOperation({ summary: 'Get the Microsoft OAuth consent URL for connecting Outlook' })
  @ApiResponse({ status: 200, description: 'Microsoft OAuth consent URL' })
  @ApiResponse({ status: 500, description: 'MICROSOFT_CLIENT_ID/SECRET/REDIRECT_URI not configured' })
  async connectOutlook(@CurrentUser() user: AuthenticatedUser) {
    return { url: await this.outlookService.getAuthUrl(user.id, user.companyId) };
  }

  @Get('connect/outlook/callback')
  @ApiOperation({ summary: 'Outlook OAuth callback — exchanges code for tokens and stores the connection' })
  @ApiResponse({ status: 200, description: 'Outlook account connected' })
  @ApiResponse({ status: 401, description: 'Invalid or expired state' })
  @ApiResponse({ status: 502, description: 'Token exchange with Microsoft failed' })
  outlookCallback(@Query('code') code: string, @Query('state') state: string) {
    return this.outlookService.handleCallback(code, state);
  }

  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard, CompanyGuard)
  @Post('send')
  @ApiOperation({ summary: 'Send an email through the user\'s connected Gmail or Outlook account' })
  @ApiResponse({ status: 201, description: 'Email sent, Communication record created' })
  @ApiResponse({ status: 400, description: 'No email provider connected, or contact has no email' })
  @ApiResponse({ status: 502, description: 'Provider rejected the send request' })
  send(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser, @Body() dto: SendEmailDto) {
    return this.emailService.send(companyId, user.id, dto);
  }
}
