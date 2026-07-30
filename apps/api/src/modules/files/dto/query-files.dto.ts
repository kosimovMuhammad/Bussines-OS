import { ApiPropertyOptional } from '@nestjs/swagger';
import { IsOptional, IsUUID } from 'class-validator';

export class QueryFilesDto {
  @ApiPropertyOptional({ description: 'Filter by contact ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  contactId?: string;

  @ApiPropertyOptional({ description: 'Filter by deal ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  dealId?: string;

  @ApiPropertyOptional({ description: 'Filter by invoice ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  invoiceId?: string;
}
