import { ApiPropertyOptional } from '@nestjs/swagger';
import { IsEnum, IsOptional, IsUUID } from 'class-validator';
import { Channel } from '@prisma/client';

export class QueryCommunicationsDto {
  @ApiPropertyOptional({ description: 'Filter by contact ID', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  contactId?: string;

  @ApiPropertyOptional({ enum: Channel, description: 'Filter by channel' })
  @IsOptional()
  @IsEnum(Channel)
  channel?: Channel;
}
