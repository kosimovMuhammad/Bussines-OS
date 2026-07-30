import { ApiProperty } from '@nestjs/swagger';
import { Type } from 'class-transformer';
import { ArrayMinSize, IsArray, IsNumber, IsString, IsUUID, Min, MinLength, ValidateNested } from 'class-validator';

export class QuotationItemDto {
  @ApiProperty({ description: 'Line item description', example: 'Website design' })
  @IsString()
  @MinLength(1)
  description: string;

  @ApiProperty({ description: 'Quantity', minimum: 1, example: 1 })
  @IsNumber()
  @Min(1)
  quantity: number;

  @ApiProperty({ description: 'Unit price', minimum: 0, example: 500 })
  @IsNumber()
  @Min(0)
  unitPrice: number;
}

export class AiGenerateQuotationDto {
  @ApiProperty({ description: 'Deal this quotation is for', format: 'uuid' })
  @IsUUID()
  dealId: string;

  @ApiProperty({ description: 'Quotation line items', type: [QuotationItemDto] })
  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => QuotationItemDto)
  items: QuotationItemDto[];
}
