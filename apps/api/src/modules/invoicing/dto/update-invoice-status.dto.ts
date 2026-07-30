import { ApiProperty } from '@nestjs/swagger';
import { IsEnum } from 'class-validator';
import { InvoiceStatus } from '@prisma/client';

export class UpdateInvoiceStatusDto {
  @ApiProperty({ enum: InvoiceStatus, description: 'New status for the invoice' })
  @IsEnum(InvoiceStatus)
  status: InvoiceStatus;
}
