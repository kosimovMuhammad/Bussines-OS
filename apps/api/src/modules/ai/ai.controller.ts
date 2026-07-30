import { Body, Controller, Get, Param, Post, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { AiService } from './ai.service';
import { SummarizeDto } from './dto/summarize.dto';
import { AiQueryDto } from './dto/ai-query.dto';
import { AiDraftEmailDto } from './dto/ai-draft-email.dto';
import { AiGenerateQuotationDto } from './dto/ai-generate-quotation.dto';

@ApiTags('AI')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('ai')
export class AiController {
  constructor(private readonly aiService: AiService) {}

  @Post('summarize')
  @ApiOperation({ summary: 'Enqueue AI summarization of a communication thread' })
  @ApiResponse({ status: 201, description: 'Summarization job queued' })
  @ApiResponse({ status: 404, description: 'Communication not found for this company' })
  @ApiResponse({ status: 500, description: 'GEMINI_API_KEY not configured' })
  summarize(@CompanyId() companyId: string, @Body() dto: SummarizeDto) {
    return this.aiService.enqueueSummarize(companyId, dto);
  }

  @Get('summary/:id')
  @ApiOperation({ summary: 'Get an AI summary by AiSummary id or communicationId' })
  @ApiResponse({ status: 200, description: 'AI summary record' })
  @ApiResponse({ status: 404, description: 'AI summary not found' })
  getSummary(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.aiService.getSummary(companyId, id);
  }

  @Post('query')
  @ApiOperation({ summary: 'Ask a natural-language question about contacts/deals/revenue (classified into a fixed set of safe intents)' })
  @ApiResponse({ status: 201, description: 'Query job queued' })
  @ApiResponse({ status: 500, description: 'GEMINI_API_KEY not configured' })
  query(@CompanyId() companyId: string, @Body() dto: AiQueryDto) {
    return this.aiService.enqueueQuery(companyId, dto);
  }

  @Get('query/result/:id')
  @ApiOperation({ summary: 'Poll for an AI query result (raw rows + a short natural-language answer)' })
  @ApiResponse({ status: 200, description: 'Query result' })
  @ApiResponse({ status: 404, description: 'Query result not found' })
  @ApiResponse({ status: 422, description: "Couldn't understand the question" })
  getQueryResult(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.aiService.getQueryResult(companyId, id);
  }

  @Post('draft-email')
  @ApiOperation({ summary: "Draft a follow-up email using a contact's communication timeline (does not send it)" })
  @ApiResponse({ status: 201, description: 'Draft job queued' })
  @ApiResponse({ status: 404, description: 'Contact not found for this company' })
  @ApiResponse({ status: 500, description: 'GEMINI_API_KEY not configured' })
  draftEmail(@CompanyId() companyId: string, @Body() dto: AiDraftEmailDto) {
    return this.aiService.enqueueDraftEmail(companyId, dto);
  }

  @Get('draft-email/result/:id')
  @ApiOperation({ summary: 'Poll for an AI email draft result (subject + body)' })
  @ApiResponse({ status: 200, description: 'Draft result' })
  @ApiResponse({ status: 404, description: 'Draft not found' })
  @ApiResponse({ status: 422, description: 'AI could not draft the email' })
  getDraftEmail(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.aiService.getDraftEmail(companyId, id);
  }

  @Post('generate-quotation')
  @ApiOperation({ summary: 'Generate a quotation PDF for a deal (uploaded to S3, linked as a FileAsset)' })
  @ApiResponse({ status: 201, description: 'Quotation job queued' })
  @ApiResponse({ status: 404, description: 'Deal not found for this company' })
  generateQuotation(
    @CompanyId() companyId: string,
    @CurrentUser() user: AuthenticatedUser,
    @Body() dto: AiGenerateQuotationDto,
  ) {
    return this.aiService.enqueueQuotation(companyId, user.id, dto);
  }

  @Get('generate-quotation/result/:id')
  @ApiOperation({ summary: 'Poll for a generated quotation (totals + FileAsset + presigned download URL)' })
  @ApiResponse({ status: 200, description: 'Quotation result' })
  @ApiResponse({ status: 404, description: 'Quotation not found' })
  @ApiResponse({ status: 422, description: 'Quotation generation failed' })
  getQuotation(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.aiService.getQuotation(companyId, id);
  }
}
