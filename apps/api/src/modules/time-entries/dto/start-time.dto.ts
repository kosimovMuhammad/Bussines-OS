import { IsOptional, IsString } from 'class-validator';

export class StartTimeDto {
  @IsOptional()
  @IsString()
  note?: string;
}
