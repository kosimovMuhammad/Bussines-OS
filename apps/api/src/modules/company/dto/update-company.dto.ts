import { ApiPropertyOptional } from '@nestjs/swagger';
import { IsOptional, IsString, MinLength } from 'class-validator';

export class UpdateCompanyDto {
  @ApiPropertyOptional({ description: 'Company display name' })
  @IsOptional()
  @IsString()
  @MinLength(2)
  name?: string;

  @ApiPropertyOptional({ description: 'Industry the company operates in' })
  @IsOptional()
  @IsString()
  industry?: string;

  @ApiPropertyOptional({ description: 'IANA timezone, e.g. Asia/Dushanbe' })
  @IsOptional()
  @IsString()
  timezone?: string;
}
