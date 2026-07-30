import { Body, Controller, Delete, Get, Param, Patch, Post, Query, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { CalendarService } from './calendar.service';
import { CreateMeetingDto } from './dto/create-meeting.dto';
import { UpdateMeetingDto } from './dto/update-meeting.dto';
import { UpdateMeetingStatusDto } from './dto/update-meeting-status.dto';
import { QueryMeetingsDto } from './dto/query-meetings.dto';

@ApiTags('Calendar')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('meetings')
export class CalendarController {
  constructor(private readonly calendarService: CalendarService) {}

  @Get()
  @ApiOperation({ summary: 'List meetings in a date range, filterable by contact/deal/organizer' })
  @ApiResponse({ status: 200, description: 'List of meetings' })
  findAll(@CompanyId() companyId: string, @Query() query: QueryMeetingsDto) {
    return this.calendarService.findAll(companyId, query);
  }

  @Post()
  @ApiOperation({ summary: 'Create a new meeting' })
  @ApiResponse({ status: 201, description: 'Created meeting record' })
  @ApiResponse({ status: 400, description: 'endAt not after startAt, or contactId/dealId invalid' })
  create(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser, @Body() dto: CreateMeetingDto) {
    return this.calendarService.create(companyId, user.id, dto);
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get a single meeting, with contact and deal populated' })
  @ApiResponse({ status: 200, description: 'Meeting record' })
  @ApiResponse({ status: 404, description: 'Meeting not found' })
  findOne(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.calendarService.findOne(companyId, id);
  }

  @Patch(':id')
  @ApiOperation({ summary: 'Update meeting fields' })
  @ApiResponse({ status: 200, description: 'Updated meeting record' })
  @ApiResponse({ status: 400, description: 'endAt not after startAt, or contactId/dealId invalid' })
  @ApiResponse({ status: 404, description: 'Meeting not found' })
  update(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateMeetingDto) {
    return this.calendarService.update(companyId, id, dto);
  }

  @Patch(':id/status')
  @ApiOperation({ summary: 'Change a meeting status' })
  @ApiResponse({ status: 200, description: 'Updated meeting record' })
  @ApiResponse({ status: 404, description: 'Meeting not found' })
  updateStatus(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateMeetingStatusDto) {
    return this.calendarService.updateStatus(companyId, id, dto.status);
  }

  @Delete(':id')
  @ApiOperation({ summary: 'Delete a meeting' })
  @ApiResponse({ status: 200, description: 'Meeting deleted' })
  @ApiResponse({ status: 404, description: 'Meeting not found' })
  remove(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.calendarService.remove(companyId, id);
  }
}
