import { ApiProperty } from '@nestjs/swagger';
import { IsEmail, IsString, MinLength } from 'class-validator';

export class RegisterDto {
  @ApiProperty({ description: 'Name of the company (tenant) being created', minLength: 2, example: 'Acme LLC' })
  @IsString()
  @MinLength(2)
  companyName: string;

  @ApiProperty({ description: 'Full name of the first user (becomes Owner)', minLength: 2, example: 'Aziz Karimov' })
  @IsString()
  @MinLength(2)
  name: string;

  @ApiProperty({ description: 'Email address, used as the login identifier', example: 'aziz@acme.com' })
  @IsEmail()
  email: string;

  @ApiProperty({ description: 'Account password', minLength: 8, example: 'S3curePassw0rd!' })
  @IsString()
  @MinLength(8)
  password: string;
}
