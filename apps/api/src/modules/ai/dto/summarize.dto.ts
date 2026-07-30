import { ApiProperty } from '@nestjs/swagger';
import { IsUUID } from 'class-validator';

export class SummarizeDto {
  @ApiProperty({ description: 'Communication ID to summarize', format: 'uuid' })
  @IsUUID()
  communicationId: string;
}
