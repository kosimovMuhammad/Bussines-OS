import { ApiProperty } from '@nestjs/swagger';
import { Plan } from '@prisma/client';
import { IsEnum } from 'class-validator';

export class UpdatePlanDto {
  @ApiProperty({ enum: Plan, description: 'Subscription plan to switch the company to' })
  @IsEnum(Plan)
  plan: Plan;
}
