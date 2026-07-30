import { IsOptional, IsString, MinLength } from 'class-validator';

export class ConnectWhatsappAccountDto {
  @IsString()
  @MinLength(1)
  phoneNumberId: string;

  @IsString()
  @MinLength(1)
  wabaId: string;

  @IsString()
  @MinLength(1)
  accessToken: string;

  @IsOptional()
  @IsString()
  verifiedName?: string;
}
