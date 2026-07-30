import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsArray, IsDateString, IsOptional, IsString, IsUUID, MinLength } from 'class-validator';

export class CreateMeetingDto {
  @ApiProperty({ description: 'Meeting title', minLength: 1, example: 'Contract review call' })
  @IsString()
  @MinLength(1)
  title: string;

  @ApiPropertyOptional({ description: 'Related contact ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  contactId?: string;

  @ApiPropertyOptional({ description: 'Related deal ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  dealId?: string;

  @ApiProperty({ description: 'Meeting start time (ISO 8601)', format: 'date-time' })
  @IsDateString()
  startAt: string;

  @ApiProperty({ description: 'Meeting end time (ISO 8601), must be after startAt', format: 'date-time' })
  @IsDateString()
  endAt: string;

  @ApiPropertyOptional({ description: 'Physical address or a Zoom/Meet link' })
  @IsOptional()
  @IsString()
  location?: string;

  @ApiPropertyOptional({ description: 'Meeting description/agenda' })
  @IsOptional()
  @IsString()
  description?: string;

  @ApiPropertyOptional({
    description: 'User IDs attending, in addition to the organizer. Defaults to just the creator.',
    type: [String],
  })
  @IsOptional()
  @IsArray()
  @IsUUID('4', { each: true })
  attendeeUserIds?: string[];
}
