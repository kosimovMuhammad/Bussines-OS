import { Body, Controller, Delete, Get, Param, Post, UseGuards } from '@nestjs/common';
import { Role } from '@prisma/client';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { TimeEntriesService } from './time-entries.service';
import { StartTimeDto } from './dto/start-time.dto';

@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('tasks')
export class TimeEntriesController {
  constructor(private readonly timeEntriesService: TimeEntriesService) {}

  @Get('time/active')
  findActive(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser) {
    return this.timeEntriesService.findActive(companyId, user.id);
  }

  @Post(':id/time/start')
  start(
    @CompanyId() companyId: string,
    @CurrentUser() user: AuthenticatedUser,
    @Param('id') taskId: string,
    @Body() dto: StartTimeDto,
  ) {
    return this.timeEntriesService.start(companyId, user.id, taskId, dto.note);
  }

  @Post(':id/time/stop')
  stop(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser, @Param('id') taskId: string) {
    return this.timeEntriesService.stop(companyId, user.id, taskId);
  }

  @Get(':id/time')
  findAllForTask(@CompanyId() companyId: string, @Param('id') taskId: string) {
    return this.timeEntriesService.findAllForTask(companyId, taskId);
  }

  @Roles(Role.OWNER, Role.ADMIN, Role.MANAGER)
  @UseGuards(RolesGuard)
  @Delete(':id/time/:entryId')
  remove(
    @CompanyId() companyId: string,
    @Param('id') taskId: string,
    @Param('entryId') entryId: string,
    @CurrentUser() actor: AuthenticatedUser,
  ) {
    return this.timeEntriesService.remove(companyId, taskId, entryId, actor.id);
  }
}
