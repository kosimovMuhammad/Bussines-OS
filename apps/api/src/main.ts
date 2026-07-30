import { NestFactory } from '@nestjs/core';
import { ConfigService } from '@nestjs/config';
import { UnprocessableEntityException, ValidationPipe } from '@nestjs/common';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import { SwaggerTheme, SwaggerThemeNameEnum } from 'swagger-themes';
import type { NestExpressApplication } from '@nestjs/platform-express';
import type { ValidationError } from 'class-validator';
import helmet from 'helmet';
import cookieParser from 'cookie-parser';
import { AppModule } from './app.module';

function flattenValidationErrors(errors: ValidationError[]): string[] {
  return errors.flatMap((error) => {
    const messages = Object.values(error.constraints ?? {});
    const childMessages = error.children?.length ? flattenValidationErrors(error.children) : [];
    return [...messages, ...childMessages];
  });
}

async function bootstrap() {
  const app = await NestFactory.create<NestExpressApplication>(AppModule, { rawBody: true });
  const config = app.get(ConfigService);

  app.use(helmet());
  app.use(cookieParser());
  app.enableCors({
    origin: config.get<string>('corsOrigin'),
    credentials: true,
  });
  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      forbidNonWhitelisted: true,
      transform: true,
      exceptionFactory: (errors) =>
        new UnprocessableEntityException({
          statusCode: 422,
          error: 'Unprocessable Entity',
          message: flattenValidationErrors(errors),
        }),
    }),
  );

  const swaggerConfig = new DocumentBuilder()
    .setTitle('Business OS API')
    .setDescription('Unified CRM + WhatsApp + Email + AI platform API')
    .setVersion('1.0')
    .addBearerAuth({ type: 'http', scheme: 'bearer', bearerFormat: 'JWT' }, 'bearer')
    .addTag('Auth')
    .addTag('Users & Roles')
    .addTag('Contacts')
    .addTag('Deals')
    .addTag('Tasks & Time Tracking')
    .addTag('Communications')
    .addTag('WhatsApp')
    .addTag('Email')
    .addTag('Analytics & Dashboard')
    .addTag('AI')
    .addTag('Company')
    .build();
  const swaggerDocument = SwaggerModule.createDocument(app, swaggerConfig);
  const swaggerTheme = new SwaggerTheme();
  SwaggerModule.setup('docs', app, swaggerDocument, {
    customSiteTitle: 'Business OS API',
    customCss: swaggerTheme.getBuffer(SwaggerThemeNameEnum.DARK),
  });

  const port = config.get<number>('port') ?? 3000;
  await app.listen(port);
  // eslint-disable-next-line no-console
  console.log(`API listening on http://localhost:${port}`);
  // eslint-disable-next-line no-console
  console.log(`Swagger docs at http://localhost:${port}/docs`);
}

bootstrap();
