import { ApiProperty } from '@nestjs/swagger';
import { IsString, IsUUID, MinLength } from 'class-validator';

export class SendWhatsappDto {
  @ApiProperty({ description: 'Contact to send the message to', format: 'uuid' })
  @IsUUID()
  contactId: string;

  @ApiProperty({ description: 'Message text', minLength: 1 })
  @IsString()
  @MinLength(1)
  message: string;
}
