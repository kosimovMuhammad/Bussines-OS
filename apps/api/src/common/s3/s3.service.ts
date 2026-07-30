import { Injectable, InternalServerErrorException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { DeleteObjectCommand, GetObjectCommand, PutObjectCommand, S3Client } from '@aws-sdk/client-s3';
import { getSignedUrl } from '@aws-sdk/s3-request-presigner';
import { randomUUID } from 'crypto';

const PRESIGN_EXPIRY_SECONDS = 900;

@Injectable()
export class S3Service {
  private readonly client: S3Client | null;
  private readonly bucket: string | undefined;

  constructor(private readonly config: ConfigService) {
    this.bucket = this.config.get<string>('aws.s3Bucket') || undefined;
    const accessKeyId = this.config.get<string>('aws.accessKeyId');
    const secretAccessKey = this.config.get<string>('aws.secretAccessKey');
    const region = this.config.get<string>('aws.region');

    this.client =
      this.bucket && accessKeyId && secretAccessKey
        ? new S3Client({ region, credentials: { accessKeyId, secretAccessKey } })
        : null;
  }

  buildKey(companyId: string, filename: string): string {
    const safeName = filename.replace(/[^a-zA-Z0-9._-]/g, '_');
    return `companies/${companyId}/${randomUUID()}-${safeName}`;
  }

  async getPresignedUploadUrl(key: string, mimeType: string): Promise<string> {
    const client = this.assertConfigured();
    const command = new PutObjectCommand({ Bucket: this.bucket, Key: key, ContentType: mimeType });
    return getSignedUrl(client, command, { expiresIn: PRESIGN_EXPIRY_SECONDS });
  }

  async getPresignedDownloadUrl(key: string): Promise<string> {
    const client = this.assertConfigured();
    const command = new GetObjectCommand({ Bucket: this.bucket, Key: key });
    return getSignedUrl(client, command, { expiresIn: PRESIGN_EXPIRY_SECONDS });
  }

  async uploadBuffer(key: string, body: Buffer, mimeType: string): Promise<void> {
    const client = this.assertConfigured();
    await client.send(new PutObjectCommand({ Bucket: this.bucket, Key: key, Body: body, ContentType: mimeType }));
  }

  async deleteObject(key: string): Promise<void> {
    const client = this.assertConfigured();
    await client.send(new DeleteObjectCommand({ Bucket: this.bucket, Key: key }));
  }

  private assertConfigured(): S3Client {
    if (!this.client || !this.bucket) {
      throw new InternalServerErrorException(
        'AWS S3 танзим нашудааст (AWS_S3_BUCKET/AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY)',
      );
    }
    return this.client;
  }
}
