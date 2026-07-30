import { Body, Controller, Get, Post, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { WhatsappService } from './whatsapp.service';
import { ConnectWhatsappAccountDto } from './dto/connect-account.dto';
import { SendWhatsappDto } from './dto/send-whatsapp.dto';

@ApiTags('WhatsApp')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('whatsapp')
export class WhatsappController {
  constructor(private readonly whatsappService: WhatsappService) {}

  @Get('accounts')
  @ApiOperation({ summary: 'List WhatsApp Business accounts connected to this company' })
  @ApiResponse({ status: 200, description: 'List of connected accounts (access tokens omitted)' })
  listAccounts(@CompanyId() companyId: string) {
    return this.whatsappService.listAccounts(companyId);
  }

  @Post('accounts')
  @ApiOperation({ summary: 'Connect (or update) a WhatsApp Business account via Meta Graph API credentials' })
  @ApiResponse({ status: 201, description: 'Connected account (access token omitted)' })
  connectAccount(@CompanyId() companyId: string, @Body() dto: ConnectWhatsappAccountDto) {
    return this.whatsappService.connectAccount(companyId, dto);
  }

  @Post('send')
  @ApiOperation({ summary: 'Send a WhatsApp text message to a contact' })
  @ApiResponse({ status: 201, description: 'Message sent, Communication record created' })
  @ApiResponse({ status: 400, description: 'Contact has no phone number, or no account connected' })
  @ApiResponse({ status: 502, description: 'WhatsApp API rejected the message' })
  send(@CompanyId() companyId: string, @Body() dto: SendWhatsappDto) {
    return this.whatsappService.send(companyId, dto);
  }
}
