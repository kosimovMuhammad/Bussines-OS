import { IsEnum, IsOptional, IsString, IsUUID, MinLength } from 'class-validator';
import { Direction } from '@prisma/client';

export class CreateNoteDto {
  @IsUUID()
  contactId: string;

  @IsString()
  @MinLength(1)
  content: string;

  @IsOptional()
  @IsEnum(Direction)
  direction?: Direction;
}
