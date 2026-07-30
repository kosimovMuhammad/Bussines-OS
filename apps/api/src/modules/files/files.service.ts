import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { Prisma } from '@prisma/client';
import { PrismaService } from '../../prisma/prisma.service';
import { S3Service } from '../../common/s3/s3.service';
import { PresignUploadDto } from './dto/presign-upload.dto';
import { ConfirmFileDto } from './dto/confirm-file.dto';
import { QueryFilesDto } from './dto/query-files.dto';

interface EntityRelations {
  contactId?: string;
  dealId?: string;
  invoiceId?: string;
}

@Injectable()
export class FilesService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly s3: S3Service,
  ) {}

  async presign(companyId: string, dto: PresignUploadDto) {
    this.assertHasRelation(dto);
    await this.assertRelationsInCompany(companyId, dto);

    const s3Key = this.s3.buildKey(companyId, dto.filename);
    const url = await this.s3.getPresignedUploadUrl(s3Key, dto.mimeType);
    return { url, s3Key };
  }

  async confirm(companyId: string, uploadedById: string, dto: ConfirmFileDto) {
    this.assertHasRelation(dto);
    await this.assertRelationsInCompany(companyId, dto);

    return this.prisma.fileAsset.create({
      data: {
        companyId,
        contactId: dto.contactId,
        dealId: dto.dealId,
        invoiceId: dto.invoiceId,
        uploadedById,
        s3Key: dto.s3Key,
        filename: dto.filename,
        mimeType: dto.mimeType,
        sizeBytes: dto.sizeBytes,
      },
    });
  }

  async findAll(companyId: string, query: QueryFilesDto) {
    const where: Prisma.FileAssetWhereInput = { companyId };
    if (query.contactId) where.contactId = query.contactId;
    if (query.dealId) where.dealId = query.dealId;
    if (query.invoiceId) where.invoiceId = query.invoiceId;

    const files = await this.prisma.fileAsset.findMany({ where, orderBy: { createdAt: 'desc' } });

    return Promise.all(
      files.map(async (file) => ({
        ...file,
        downloadUrl: await this.s3.getPresignedDownloadUrl(file.s3Key),
      })),
    );
  }

  async remove(companyId: string, id: string) {
    const file = await this.ensureExists(companyId, id);
    await this.s3.deleteObject(file.s3Key);
    await this.prisma.fileAsset.delete({ where: { id } });
    return { success: true };
  }

  private assertHasRelation(dto: EntityRelations) {
    if (!dto.contactId && !dto.dealId && !dto.invoiceId) {
      throw new BadRequestException('Ҳадди ақал яке аз contactId, dealId ё invoiceId лозим аст');
    }
  }

  private async assertRelationsInCompany(companyId: string, dto: EntityRelations) {
    if (dto.contactId) {
      const contact = await this.prisma.contact.findFirst({ where: { id: dto.contactId, companyId } });
      if (!contact) throw new BadRequestException('contactId нодуруст аст ё ба ин company тааллуқ надорад');
    }
    if (dto.dealId) {
      const deal = await this.prisma.deal.findFirst({ where: { id: dto.dealId, companyId } });
      if (!deal) throw new BadRequestException('dealId нодуруст аст ё ба ин company тааллуқ надорад');
    }
    if (dto.invoiceId) {
      const invoice = await this.prisma.invoice.findFirst({ where: { id: dto.invoiceId, companyId } });
      if (!invoice) throw new BadRequestException('invoiceId нодуруст аст ё ба ин company тааллуқ надорад');
    }
  }

  private async ensureExists(companyId: string, id: string) {
    const file = await this.prisma.fileAsset.findFirst({ where: { id, companyId } });
    if (!file) {
      throw new NotFoundException('Файл ёфт нашуд');
    }
    return file;
  }
}
