import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsInt, IsOptional, IsString, IsUUID, Min, MinLength } from 'class-validator';

export class ConfirmFileDto {
  @ApiProperty({ description: 'S3 object key returned by POST /files/presign' })
  @IsString()
  @MinLength(1)
  s3Key: string;

  @ApiProperty({ description: 'Original filename', example: 'contract.pdf' })
  @IsString()
  @MinLength(1)
  filename: string;

  @ApiProperty({ description: 'MIME type of the file', example: 'application/pdf' })
  @IsString()
  @MinLength(1)
  mimeType: string;

  @ApiProperty({ description: 'File size in bytes', minimum: 1 })
  @IsInt()
  @Min(1)
  sizeBytes: number;

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
