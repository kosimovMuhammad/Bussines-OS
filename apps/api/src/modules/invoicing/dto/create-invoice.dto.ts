import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsDateString, IsNumber, IsOptional, IsString, IsUUID, Min, MinLength } from 'class-validator';

export class CreateInvoiceDto {
  @ApiPropertyOptional({ description: 'Related deal ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  dealId?: string;

  @ApiProperty({ description: 'Contact this invoice is billed to', format: 'uuid' })
  @IsUUID()
  contactId: string;

  @ApiProperty({ description: 'Invoice amount', minimum: 0, example: 1500 })
  @IsNumber()
  @Min(0)
  amount: number;

  @ApiPropertyOptional({ description: 'Currency code', default: 'TJS', example: 'TJS' })
  @IsOptional()
  @IsString()
  currency?: string;

  @ApiProperty({ description: 'Due date (ISO 8601)', format: 'date-time' })
  @IsDateString()
  dueDate: string;

  @ApiPropertyOptional({ description: 'Free-form notes' })
  @IsOptional()
  @IsString()
  @MinLength(1)
  notes?: string;
}
