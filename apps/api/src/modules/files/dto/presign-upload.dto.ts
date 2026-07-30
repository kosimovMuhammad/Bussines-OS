import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsOptional, IsString, IsUUID, MinLength } from 'class-validator';

export class PresignUploadDto {
  @ApiProperty({ description: 'Original filename', example: 'contract.pdf' })
  @IsString()
  @MinLength(1)
  filename: string;

  @ApiProperty({ description: 'MIME type of the file', example: 'application/pdf' })
  @IsString()
  @MinLength(1)
  mimeType: string;

  @ApiPropertyOptional({ description: 'Related contact ID (at least one relation is required)', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  contactId?: string;

  @ApiPropertyOptional({ description: 'Related deal ID (at least one relation is required)', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  dealId?: string;

  @ApiPropertyOptional({ description: 'Related invoice ID (at least one relation is required)', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  invoiceId?: string;
}
