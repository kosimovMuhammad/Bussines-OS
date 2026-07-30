import { ApiProperty } from '@nestjs/swagger';
import { IsEnum } from 'class-validator';
import { TaskStatus } from '@prisma/client';

export class UpdateTaskStatusDto {
  @ApiProperty({ enum: TaskStatus, description: 'New status for the task' })
  @IsEnum(TaskStatus)
  status: TaskStatus;
}
