import { ApiPropertyOptional } from '@nestjs/swagger';
import { IsEnum, IsOptional, IsUUID } from 'class-validator';
import { DealStage } from '@prisma/client';

export class QueryDealsDto {
  @ApiPropertyOptional({ enum: DealStage, description: 'Filter by pipeline stage' })
  @IsOptional()
  @IsEnum(DealStage)
  stage?: DealStage;

  @ApiPropertyOptional({ description: 'Filter by owning user ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  ownerId?: string;
}
