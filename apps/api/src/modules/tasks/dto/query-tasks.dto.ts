import { ApiPropertyOptional } from '@nestjs/swagger';
import { IsEnum, IsOptional, IsUUID } from 'class-validator';
import { TaskStatus } from '@prisma/client';

export class QueryTasksDto {
  @ApiPropertyOptional({ enum: TaskStatus, description: 'Filter by task status' })
  @IsOptional()
  @IsEnum(TaskStatus)
  status?: TaskStatus;

  @ApiPropertyOptional({ description: 'Filter by assignee user ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  assignedTo?: string;

  @ApiPropertyOptional({ description: 'Filter by related deal ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  dealId?: string;

  @ApiPropertyOptional({ description: 'Filter by related contact ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  contactId?: string;
}
