import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsDateString, IsEnum, IsOptional, IsString, IsUUID, MinLength } from 'class-validator';
import { TaskPriority, TaskStatus } from '@prisma/client';

export class CreateTaskDto {
  @ApiProperty({ description: 'Task title', minLength: 1, example: 'Follow up with client' })
  @IsString()
  @MinLength(1)
  title: string;

  @ApiPropertyOptional({ description: 'Task description' })
  @IsOptional()
  @IsString()
  description?: string;

  @ApiPropertyOptional({ enum: TaskPriority, description: 'Priority level', default: TaskPriority.MEDIUM })
  @IsOptional()
  @IsEnum(TaskPriority)
  priority?: TaskPriority;

  @ApiPropertyOptional({ enum: TaskStatus, description: 'Task status', default: TaskStatus.TODO })
  @IsOptional()
  @IsEnum(TaskStatus)
  status?: TaskStatus;

  @ApiPropertyOptional({ description: 'User ID this task is assigned to', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  assignedTo?: string;

  @ApiPropertyOptional({ description: 'Due date (ISO 8601)', format: 'date-time' })
  @IsOptional()
  @IsDateString()
  dueDate?: string;

  @ApiPropertyOptional({ description: 'Related deal ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  dealId?: string;

  @ApiPropertyOptional({ description: 'Related contact ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  contactId?: string;
}
