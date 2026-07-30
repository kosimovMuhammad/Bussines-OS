import { ApiProperty } from '@nestjs/swagger';
import { IsString, MinLength } from 'class-validator';

export class AiQueryDto {
  @ApiProperty({
    description: 'Natural-language question about contacts/deals/revenue',
    example: "Show clients who haven't replied in 14 days",
  })
  @IsString()
  @MinLength(1)
  question: string;
}
