import { Body, Controller, Delete, Get, Param, Post, UseGuards } from '@nestjs/common';
import { Role } from '@prisma/client';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { TimeEntriesService } from './time-entries.service';
import { StartTimeDto } from './dto/start-time.dto';

@ApiTags('Tasks & Time Tracking')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('tasks')
export class TimeEntriesController {
  constructor(private readonly timeEntriesService: TimeEntriesService) {}

  @Get('time/active')
  @ApiOperation({ summary: "Get the current user's currently-running time entry, if any" })
  @ApiResponse({ status: 200, description: 'Active time entry, or null' })
  findActive(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser) {
    return this.timeEntriesService.findActive(companyId, user.id);
  }

  @Post(':id/time/start')
  @ApiOperation({ summary: 'Start a timer for a task' })
  @ApiResponse({ status: 201, description: 'Time entry created' })
  @ApiResponse({ status: 409, description: 'A timer is already running for this user' })
  start(
    @CompanyId() companyId: string,
    @CurrentUser() user: AuthenticatedUser,
    @Param('id') taskId: string,
    @Body() dto: StartTimeDto,
  ) {
    return this.timeEntriesService.start(companyId, user.id, taskId, dto.note);
  }

  @Post(':id/time/stop')
  @ApiOperation({ summary: 'Stop the running timer for a task' })
  @ApiResponse({ status: 201, description: 'Time entry stopped, duration calculated' })
  @ApiResponse({ status: 404, description: 'No running timer found for this task' })
  stop(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser, @Param('id') taskId: string) {
    return this.timeEntriesService.stop(companyId, user.id, taskId);
  }

  @Get(':id/time')
  @ApiOperation({ summary: 'List all time entries logged against a task' })
  @ApiResponse({ status: 200, description: 'List of time entries' })
  findAllForTask(@CompanyId() companyId: string, @Param('id') taskId: string) {
    return this.timeEntriesService.findAllForTask(companyId, taskId);
  }

  @Roles(Role.OWNER, Role.ADMIN, Role.MANAGER)
  @UseGuards(RolesGuard)
  @Delete(':id/time/:entryId')
  @ApiOperation({ summary: 'Delete a time entry (Owner/Admin/Manager only)' })
  @ApiResponse({ status: 200, description: 'Time entry deleted' })
  @ApiResponse({ status: 403, description: 'Insufficient role' })
  @ApiResponse({ status: 404, description: 'Time entry not found' })
  remove(
    @CompanyId() companyId: string,
    @Param('id') taskId: string,
    @Param('entryId') entryId: string,
    @CurrentUser() actor: AuthenticatedUser,
  ) {
    return this.timeEntriesService.remove(companyId, taskId, entryId, actor.id);
  }
}
