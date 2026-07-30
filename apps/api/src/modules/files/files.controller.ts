import { Body, Controller, Delete, Get, Param, Post, Query, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { FilesService } from './files.service';
import { PresignUploadDto } from './dto/presign-upload.dto';
import { ConfirmFileDto } from './dto/confirm-file.dto';
import { QueryFilesDto } from './dto/query-files.dto';

@ApiTags('Files')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('files')
export class FilesController {
  constructor(private readonly filesService: FilesService) {}

  @Post('presign')
  @ApiOperation({ summary: 'Get a presigned S3 PUT URL so the frontend can upload a file directly to S3' })
  @ApiResponse({ status: 201, description: 'Presigned upload URL and the future s3Key' })
  @ApiResponse({ status: 400, description: 'No related entity given, or the relation is not in this company' })
  @ApiResponse({ status: 500, description: 'AWS S3 not configured' })
  presign(@CompanyId() companyId: string, @Body() dto: PresignUploadDto) {
    return this.filesService.presign(companyId, dto);
  }

  @Post()
  @ApiOperation({ summary: 'Confirm a completed S3 upload and create the FileAsset record' })
  @ApiResponse({ status: 201, description: 'Created file record' })
  @ApiResponse({ status: 400, description: 'No related entity given, or the relation is not in this company' })
  create(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser, @Body() dto: ConfirmFileDto) {
    return this.filesService.confirm(companyId, user.id, dto);
  }

  @Get()
  @ApiOperation({ summary: 'List files for a contact/deal/invoice, each with a presigned download URL' })
  @ApiResponse({ status: 200, description: 'List of files' })
  @ApiResponse({ status: 500, description: 'AWS S3 not configured' })
  findAll(@CompanyId() companyId: string, @Query() query: QueryFilesDto) {
    return this.filesService.findAll(companyId, query);
  }

  @Delete(':id')
  @ApiOperation({ summary: 'Delete a file (removes the S3 object and the DB record)' })
  @ApiResponse({ status: 200, description: 'File deleted' })
  @ApiResponse({ status: 404, description: 'File not found' })
  @ApiResponse({ status: 500, description: 'AWS S3 not configured' })
  remove(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.filesService.remove(companyId, id);
  }
}
