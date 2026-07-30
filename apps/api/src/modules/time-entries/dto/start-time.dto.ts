import { ApiPropertyOptional } from '@nestjs/swagger';
import { IsOptional, IsString } from 'class-validator';

export class StartTimeDto {
  @ApiPropertyOptional({ description: 'Optional note describing the work being timed' })
  @IsOptional()
  @IsString()
  note?: string;
}
