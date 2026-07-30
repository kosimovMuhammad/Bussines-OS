import { ApiProperty } from '@nestjs/swagger';
import { IsEnum } from 'class-validator';
import { DealStage } from '@prisma/client';

export class UpdateDealStageDto {
  @ApiProperty({ enum: DealStage, description: 'New pipeline stage for the deal' })
  @IsEnum(DealStage)
  stage: DealStage;
}
