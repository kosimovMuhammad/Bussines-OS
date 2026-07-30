import { ApiProperty } from '@nestjs/swagger';
import { IsEnum } from 'class-validator';
import { MeetingStatus } from '@prisma/client';

export class UpdateMeetingStatusDto {
  @ApiProperty({ enum: MeetingStatus, description: 'New status for the meeting' })
  @IsEnum(MeetingStatus)
  status: MeetingStatus;
}
