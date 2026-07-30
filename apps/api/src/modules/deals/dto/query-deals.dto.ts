import { IsEnum, IsOptional, IsUUID } from 'class-validator';
import { DealStage } from '@prisma/client';

export class QueryDealsDto {
  @IsOptional()
  @IsEnum(DealStage)
  stage?: DealStage;

  @IsOptional()
  @IsUUID()
  ownerId?: string;
}
