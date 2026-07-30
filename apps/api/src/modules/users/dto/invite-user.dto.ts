import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsEmail, IsEnum, IsOptional, IsString, MinLength } from 'class-validator';
import { Role } from '@prisma/client';

export class InviteUserDto {
  @ApiProperty({ description: 'Full name of the invited user', minLength: 2 })
  @IsString()
  @MinLength(2)
  name: string;

  @ApiProperty({ description: 'Email address to send the invite to', example: 'newuser@acme.com' })
  @IsEmail()
  email: string;

  @ApiPropertyOptional({ enum: Role, description: 'Role to assign', default: Role.EMPLOYEE })
  @IsOptional()
  @IsEnum(Role)
  role?: Role;
}
