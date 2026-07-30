import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsDateString, IsEnum, IsInt, IsNumber, IsOptional, IsString, IsUUID, Max, Min, MinLength } from 'class-validator';
import { DealStage } from '@prisma/client';

export class CreateDealDto {
  @ApiProperty({ description: 'Contact this deal belongs to', format: 'uuid' })
  @IsUUID()
  contactId: string;

  @ApiProperty({ description: 'Deal title', minLength: 1, example: 'Q3 enterprise contract' })
  @IsString()
  @MinLength(1)
  title: string;

  @ApiPropertyOptional({ description: 'Deal value', minimum: 0, default: 0 })
  @IsOptional()
  @IsNumber()
  @Min(0)
  value?: number;

  @ApiPropertyOptional({ description: 'Currency code', default: 'TJS', example: 'TJS' })
  @IsOptional()
  @IsString()
  currency?: string;

  @ApiPropertyOptional({ enum: DealStage, description: 'Pipeline stage', default: DealStage.LEAD })
  @IsOptional()
  @IsEnum(DealStage)
  stage?: DealStage;

  @ApiPropertyOptional({ description: 'Win probability percentage', minimum: 0, maximum: 100 })
  @IsOptional()
  @IsInt()
  @Min(0)
  @Max(100)
  probability?: number;

  @ApiPropertyOptional({ description: 'Expected close date (ISO 8601)', format: 'date-time' })
  @IsOptional()
  @IsDateString()
  expectedCloseDate?: string;

  @ApiPropertyOptional({ description: 'User ID of the deal owner', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  ownerId?: string;
}
