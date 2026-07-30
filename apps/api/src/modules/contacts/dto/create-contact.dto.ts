import { IsArray, IsEmail, IsOptional, IsString, IsUUID, Matches, MinLength } from 'class-validator';

const E164_REGEX = /^\+[1-9]\d{6,14}$/;

export class CreateContactDto {
  @IsString()
  @MinLength(1)
  firstName: string;

  @IsOptional()
  @IsString()
  lastName?: string;

  @IsOptional()
  @IsEmail()
  email?: string;

  @IsOptional()
  @Matches(E164_REGEX, { message: 'phone бояд дар формати E.164 бошад (масалан +992901234567)' })
  phone?: string;

  @IsOptional()
  @IsString()
  position?: string;

  @IsOptional()
  @IsString()
  companyName?: string;

  @IsOptional()
  @IsString()
  notes?: string;

  @IsOptional()
  @IsUUID()
  ownerId?: string;

  @IsOptional()
  @IsArray()
  @IsString({ each: true })
  tags?: string[];
}
