import { Body, Controller, Get, Post, UseGuards } from '@nestjs/common';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { WhatsappService } from './whatsapp.service';
import { ConnectWhatsappAccountDto } from './dto/connect-account.dto';
import { SendWhatsappDto } from './dto/send-whatsapp.dto';

@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('whatsapp')
export class WhatsappController {
  constructor(private readonly whatsappService: WhatsappService) {}

  @Get('accounts')
  listAccounts(@CompanyId() companyId: string) {
    return this.whatsappService.listAccounts(companyId);
  }

  @Post('accounts')
  connectAccount(@CompanyId() companyId: string, @Body() dto: ConnectWhatsappAccountDto) {
    return this.whatsappService.connectAccount(companyId, dto);
  }

  @Post('send')
  send(@CompanyId() companyId: string, @Body() dto: SendWhatsappDto) {
    return this.whatsappService.send(companyId, dto);
  }
}
