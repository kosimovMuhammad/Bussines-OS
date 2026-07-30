import { Body, Controller, Get, Post, Query, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CommunicationsService } from './communications.service';
import { CreateNoteDto } from './dto/create-note.dto';
import { QueryCommunicationsDto } from './dto/query-communications.dto';

@ApiTags('Communications')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('communications')
export class CommunicationsController {
  constructor(private readonly communicationsService: CommunicationsService) {}

  @Get()
  @ApiOperation({ summary: 'List communications across all channels, optionally filtered by contact or channel' })
  @ApiResponse({ status: 200, description: 'List of communications, including AI summaries where available' })
  findAll(@CompanyId() companyId: string, @Query() query: QueryCommunicationsDto) {
    return this.communicationsService.findAll(companyId, query);
  }

  @Post('notes')
  @ApiOperation({ summary: 'Add a manual note to a contact\'s communication timeline' })
  @ApiResponse({ status: 201, description: 'Created note (as a Communication record)' })
  @ApiResponse({ status: 404, description: 'Contact not found' })
  createNote(@CompanyId() companyId: string, @Body() dto: CreateNoteDto) {
    return this.communicationsService.createNote(companyId, dto);
  }
}
