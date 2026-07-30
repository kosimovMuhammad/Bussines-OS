import { ApiProperty } from '@nestjs/swagger';
import { IsEmail, IsString } from 'class-validator';

export class LoginDto {
  @ApiProperty({ description: 'Account email address', example: 'aziz@acme.com' })
  @IsEmail()
  email: string;

  @ApiProperty({ description: 'Account password' })
  @IsString()
  password: string;
}
