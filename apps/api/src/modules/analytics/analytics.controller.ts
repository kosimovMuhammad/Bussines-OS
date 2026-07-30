import { Controller, Get, UseGuards } from '@nestjs/common';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { AnalyticsService } from './analytics.service';

@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('dashboard')
export class AnalyticsController {
  constructor(private readonly analyticsService: AnalyticsService) {}

  @Get()
  overview(@CompanyId() companyId: string) {
    return this.analyticsService.getOverview(companyId);
  }

  @Get('sales')
  sales(@CompanyId() companyId: string) {
    return this.analyticsService.getSales(companyId);
  }

  @Get('revenue')
  revenue(@CompanyId() companyId: string) {
    return this.analyticsService.getRevenue(companyId);
  }

  @Get('tasks')
  tasks(@CompanyId() companyId: string) {
    return this.analyticsService.getTasksStats(companyId);
  }

  @Get('activity-feed')
  activityFeed(@CompanyId() companyId: string) {
    return this.analyticsService.getActivityFeed(companyId);
  }
}
