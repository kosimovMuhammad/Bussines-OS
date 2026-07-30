import { Body, Controller, Delete, Get, Param, Patch, Post, Query, UseGuards } from '@nestjs/common';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { TasksService } from './tasks.service';
import { CreateTaskDto } from './dto/create-task.dto';
import { UpdateTaskDto } from './dto/update-task.dto';
import { UpdateTaskStatusDto } from './dto/update-task-status.dto';
import { QueryTasksDto } from './dto/query-tasks.dto';

@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('tasks')
export class TasksController {
  constructor(private readonly tasksService: TasksService) {}

  @Get()
  findAll(@CompanyId() companyId: string, @Query() query: QueryTasksDto) {
    return this.tasksService.findAll(companyId, query);
  }

  @Get(':id')
  findOne(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.tasksService.findOne(companyId, id);
  }

  @Post()
  create(@CompanyId() companyId: string, @Body() dto: CreateTaskDto) {
    return this.tasksService.create(companyId, dto);
  }

  @Patch(':id')
  update(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateTaskDto) {
    return this.tasksService.update(companyId, id, dto);
  }

  @Patch(':id/status')
  updateStatus(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateTaskStatusDto) {
    return this.tasksService.updateStatus(companyId, id, dto.status);
  }

  @Delete(':id')
  remove(@CompanyId() companyId: string, @Param('id') id: string, @CurrentUser() actor: AuthenticatedUser) {
    return this.tasksService.remove(companyId, id, actor.id);
  }
}
