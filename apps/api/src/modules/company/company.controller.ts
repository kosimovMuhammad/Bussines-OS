import { Body, Controller, Get, Patch, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { Role } from '@prisma/client';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { CompanyService } from './company.service';
import { UpdateCompanyDto } from './dto/update-company.dto';
import { UpdatePlanDto } from './dto/update-plan.dto';

@ApiTags('Company')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('company')
export class CompanyController {
  constructor(private readonly companyService: CompanyService) {}

  @Get()
  @ApiOperation({ summary: 'Get the current user\'s company (tenant)' })
  @ApiResponse({ status: 200, description: 'Company record' })
  findOne(@CompanyId() companyId: string) {
    return this.companyService.findOne(companyId);
  }

  @Roles(Role.OWNER, Role.ADMIN)
  @UseGuards(RolesGuard)
  @Patch()
  @ApiOperation({ summary: 'Update company name, industry, or timezone (Owner/Admin only)' })
  @ApiResponse({ status: 200, description: 'Updated company record' })
  @ApiResponse({ status: 403, description: 'Insufficient role' })
  update(
    @CompanyId() companyId: string,
    @Body() dto: UpdateCompanyDto,
    @CurrentUser() actor: AuthenticatedUser,
  ) {
    return this.companyService.update(companyId, dto, actor.id);
  }

  @Get('plan')
  @ApiOperation({ summary: 'Get the current subscription plan' })
  @ApiResponse({ status: 200, description: 'Plan info' })
  getPlan(@CompanyId() companyId: string) {
    return this.companyService.getPlan(companyId);
  }

  @Roles(Role.OWNER)
  @UseGuards(RolesGuard)
  @Patch('plan')
  @ApiOperation({ summary: 'Upgrade or downgrade the subscription plan (Owner only)' })
  @ApiResponse({ status: 200, description: 'Updated plan info' })
  @ApiResponse({ status: 403, description: 'Only the Owner can change the plan' })
  updatePlan(
    @CompanyId() companyId: string,
    @Body() dto: UpdatePlanDto,
    @CurrentUser() actor: AuthenticatedUser,
  ) {
    return this.companyService.updatePlan(companyId, dto, actor.id);
  }
}
