import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsOptional, IsString, MinLength } from 'class-validator';

export class ConnectWhatsappAccountDto {
  @ApiProperty({ description: 'Meta WhatsApp phone number ID' })
  @IsString()
  @MinLength(1)
  phoneNumberId: string;

  @ApiProperty({ description: 'Meta WhatsApp Business Account ID' })
  @IsString()
  @MinLength(1)
  wabaId: string;

  @ApiProperty({ description: 'Meta Graph API access token for this account' })
  @IsString()
  @MinLength(1)
  accessToken: string;

  @ApiPropertyOptional({ description: 'Verified display name for the WhatsApp Business account' })
  @IsOptional()
  @IsString()
  verifiedName?: string;
}
