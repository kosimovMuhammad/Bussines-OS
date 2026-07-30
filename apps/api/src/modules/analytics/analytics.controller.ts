import { Controller, Get, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { AnalyticsService } from './analytics.service';

@ApiTags('Analytics & Dashboard')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('dashboard')
export class AnalyticsController {
  constructor(private readonly analyticsService: AnalyticsService) {}

  @Get()
  @ApiOperation({ summary: 'Get the main dashboard overview (key metrics summary)' })
  @ApiResponse({ status: 200, description: 'Overview metrics' })
  overview(@CompanyId() companyId: string) {
    return this.analyticsService.getOverview(companyId);
  }

  @Get('sales')
  @ApiOperation({ summary: 'Get sales pipeline analytics (deals by stage, conversion, etc.)' })
  @ApiResponse({ status: 200, description: 'Sales analytics' })
  sales(@CompanyId() companyId: string) {
    return this.analyticsService.getSales(companyId);
  }

  @Get('revenue')
  @ApiOperation({ summary: 'Get revenue analytics (won deals over time)' })
  @ApiResponse({ status: 200, description: 'Revenue analytics' })
  revenue(@CompanyId() companyId: string) {
    return this.analyticsService.getRevenue(companyId);
  }

  @Get('tasks')
  @ApiOperation({ summary: 'Get task completion and workload statistics' })
  @ApiResponse({ status: 200, description: 'Task statistics' })
  tasks(@CompanyId() companyId: string) {
    return this.analyticsService.getTasksStats(companyId);
  }

  @Get('activity-feed')
  @ApiOperation({ summary: 'Get a chronological feed of recent company activity' })
  @ApiResponse({ status: 200, description: 'Activity feed entries' })
  activityFeed(@CompanyId() companyId: string) {
    return this.analyticsService.getActivityFeed(companyId);
  }
}
