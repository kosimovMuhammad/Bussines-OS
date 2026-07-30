import { Controller, Get, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
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

  @Get('finance')
  @ApiOperation({ summary: 'Get finance analytics (outstanding/paid/overdue invoices, revenue by month)' })
  @ApiResponse({ status: 200, description: 'Finance analytics' })
  finance(@CompanyId() companyId: string) {
    return this.analyticsService.getFinance(companyId);
  }

  @Get('upcoming-meetings')
  @ApiOperation({ summary: "Get the current user's meetings for today and the next 7 days" })
  @ApiResponse({ status: 200, description: 'Upcoming meetings' })
  upcomingMeetings(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser) {
    return this.analyticsService.getUpcomingMeetings(companyId, user.id);
  }

  @Get('forecast')
  @ApiOperation({
    summary: 'Get a deterministic next-30-days revenue forecast (weighted open deals + outstanding invoices), with a one-sentence AI summary',
  })
  @ApiResponse({ status: 200, description: 'Revenue forecast' })
  forecast(@CompanyId() companyId: string) {
    return this.analyticsService.getForecast(companyId);
  }
}
