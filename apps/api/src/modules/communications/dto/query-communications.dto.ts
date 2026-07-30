import { IsEnum, IsOptional, IsUUID } from 'class-validator';
import { Channel } from '@prisma/client';

export class QueryCommunicationsDto {
  @IsOptional()
  @IsUUID()
  contactId?: string;

  @IsOptional()
  @IsEnum(Channel)
  channel?: Channel;
}
