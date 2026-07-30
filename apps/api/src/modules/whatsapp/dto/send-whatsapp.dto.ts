import { IsString, IsUUID, MinLength } from 'class-validator';

export class SendWhatsappDto {
  @IsUUID()
  contactId: string;

  @IsString()
  @MinLength(1)
  message: string;
}
