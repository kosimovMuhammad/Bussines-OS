import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsOptional, IsString, IsUUID } from 'class-validator';

export class AiDraftEmailDto {
  @ApiProperty({ description: 'Contact to draft the follow-up email for', format: 'uuid' })
  @IsUUID()
  contactId: string;

  @ApiPropertyOptional({
    description: 'Extra free-text context/instructions for the draft',
    example: 'follow up about the unpaid invoice',
  })
  @IsOptional()
  @IsString()
  context?: string;
}
