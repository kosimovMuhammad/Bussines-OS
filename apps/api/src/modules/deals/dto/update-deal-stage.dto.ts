import { IsEnum } from 'class-validator';
import { DealStage } from '@prisma/client';

export class UpdateDealStageDto {
  @IsEnum(DealStage)
  stage: DealStage;
}
