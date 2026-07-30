import { ApiProperty } from '@nestjs/swagger';
import { IsEnum } from 'class-validator';
import { Role } from '@prisma/client';

export class UpdateRoleDto {
  @ApiProperty({ enum: Role, description: 'New role for the user' })
  @IsEnum(Role)
  role: Role;
}
