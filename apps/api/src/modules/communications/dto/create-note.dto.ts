import { ApiProperty, ApiPropertyOptional } from '@nestjs/swagger';
import { IsEnum, IsOptional, IsString, IsUUID, MinLength } from 'class-validator';
import { Direction } from '@prisma/client';

export class CreateNoteDto {
  @ApiProperty({ description: 'Contact this note is attached to', format: 'uuid' })
  @IsUUID()
  contactId: string;

  @ApiProperty({ description: 'Note text', minLength: 1 })
  @IsString()
  @MinLength(1)
  content: string;

  @ApiPropertyOptional({ enum: Direction, description: 'Direction of the note', default: Direction.OUTBOUND })
  @IsOptional()
  @IsEnum(Direction)
  direction?: Direction;
}
