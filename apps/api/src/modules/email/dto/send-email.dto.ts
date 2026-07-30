import { IsString, IsUUID, MinLength } from 'class-validator';

export class SendEmailDto {
  @IsUUID()
  contactId: string;

  @IsString()
  @MinLength(1)
  subject: string;

  @IsString()
  @MinLength(1)
  body: string;
}
