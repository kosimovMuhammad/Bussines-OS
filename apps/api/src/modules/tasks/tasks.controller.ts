import { Body, Controller, Delete, Get, Param, Patch, Post, Query, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
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

@ApiTags('Tasks & Time Tracking')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('tasks')
export class TasksController {
  constructor(private readonly tasksService: TasksService) {}

  @Get()
  @ApiOperation({ summary: 'List tasks, optionally filtered by status, assignee, deal, or contact' })
  @ApiResponse({ status: 200, description: 'List of tasks' })
  findAll(@CompanyId() companyId: string, @Query() query: QueryTasksDto) {
    return this.tasksService.findAll(companyId, query);
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get a single task by ID, with total tracked time' })
  @ApiResponse({ status: 200, description: 'Task record' })
  @ApiResponse({ status: 404, description: 'Task not found' })
  findOne(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.tasksService.findOne(companyId, id);
  }

  @Post()
  @ApiOperation({ summary: 'Create a new task' })
  @ApiResponse({ status: 201, description: 'Created task record' })
  create(@CompanyId() companyId: string, @Body() dto: CreateTaskDto) {
    return this.tasksService.create(companyId, dto);
  }

  @Patch(':id')
  @ApiOperation({ summary: 'Update task fields (title, priority, assignee, etc.)' })
  @ApiResponse({ status: 200, description: 'Updated task record' })
  @ApiResponse({ status: 404, description: 'Task not found' })
  update(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateTaskDto) {
    return this.tasksService.update(companyId, id, dto);
  }

  @Patch(':id/status')
  @ApiOperation({ summary: 'Change a task status (e.g. mark completed)' })
  @ApiResponse({ status: 200, description: 'Updated task record' })
  @ApiResponse({ status: 404, description: 'Task not found' })
  updateStatus(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateTaskStatusDto) {
    return this.tasksService.updateStatus(companyId, id, dto.status);
  }

  @Delete(':id')
  @ApiOperation({ summary: 'Delete a task' })
  @ApiResponse({ status: 200, description: 'Task deleted' })
  @ApiResponse({ status: 404, description: 'Task not found' })
  remove(@CompanyId() companyId: string, @Param('id') id: string, @CurrentUser() actor: AuthenticatedUser) {
    return this.tasksService.remove(companyId, id, actor.id);
  }
}
