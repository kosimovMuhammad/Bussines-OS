import { ApiPropertyOptional, ApiProperty } from '@nestjs/swagger';
import { IsArray, IsEmail, IsOptional, IsString, IsUUID, Matches, MinLength } from 'class-validator';

const E164_REGEX = /^\+[1-9]\d{6,14}$/;

export class CreateContactDto {
  @ApiProperty({ description: 'First name', minLength: 1, example: 'Farrukh' })
  @IsString()
  @MinLength(1)
  firstName: string;

  @ApiPropertyOptional({ description: 'Last name' })
  @IsOptional()
  @IsString()
  lastName?: string;

  @ApiPropertyOptional({ description: 'Email address', example: 'farrukh@example.com' })
  @IsOptional()
  @IsEmail()
  email?: string;

  @ApiPropertyOptional({ description: 'Phone number in E.164 format', example: '+992901234567' })
  @IsOptional()
  @Matches(E164_REGEX, { message: 'phone бояд дар формати E.164 бошад (масалан +992901234567)' })
  phone?: string;

  @ApiPropertyOptional({ description: 'Job title / position' })
  @IsOptional()
  @IsString()
  position?: string;

  @ApiPropertyOptional({ description: "Contact's employer / company name" })
  @IsOptional()
  @IsString()
  companyName?: string;

  @ApiPropertyOptional({ description: 'Free-form notes' })
  @IsOptional()
  @IsString()
  notes?: string;

  @ApiPropertyOptional({ description: 'User ID of the owning sales rep', format: 'uuid' })
  @IsOptional()
  @IsUUID()
  ownerId?: string;

  @ApiPropertyOptional({ description: 'Freeform tags', type: [String] })
  @IsOptional()
  @IsArray()
  @IsString({ each: true })
  tags?: string[];
}
