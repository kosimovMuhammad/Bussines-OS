import { Body, Controller, Get, Post, Query, UseGuards } from '@nestjs/common';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CommunicationsService } from './communications.service';
import { CreateNoteDto } from './dto/create-note.dto';
import { QueryCommunicationsDto } from './dto/query-communications.dto';

@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('communications')
export class CommunicationsController {
  constructor(private readonly communicationsService: CommunicationsService) {}

  @Get()
  findAll(@CompanyId() companyId: string, @Query() query: QueryCommunicationsDto) {
    return this.communicationsService.findAll(companyId, query);
  }

  @Post('notes')
  createNote(@CompanyId() companyId: string, @Body() dto: CreateNoteDto) {
    return this.communicationsService.createNote(companyId, dto);
  }
}
