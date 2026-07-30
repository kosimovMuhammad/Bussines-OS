import { ApiProperty } from '@nestjs/swagger';
import { IsString, IsUUID, MinLength } from 'class-validator';

export class SendEmailDto {
  @ApiProperty({ description: 'Contact to send the email to (must have an email address)', format: 'uuid' })
  @IsUUID()
  contactId: string;

  @ApiProperty({ description: 'Email subject line', minLength: 1 })
  @IsString()
  @MinLength(1)
  subject: string;

  @ApiProperty({ description: 'Email body (plain text)', minLength: 1 })
  @IsString()
  @MinLength(1)
  body: string;
}
