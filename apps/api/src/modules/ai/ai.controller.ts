import { Body, Controller, Get, Param, Post, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { AiService } from './ai.service';
import { SummarizeDto } from './dto/summarize.dto';

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
}
