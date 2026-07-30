import { Body, Controller, Delete, Get, Param, Patch, Post, Query, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { DealsService } from './deals.service';
import { CreateDealDto } from './dto/create-deal.dto';
import { UpdateDealDto } from './dto/update-deal.dto';
import { UpdateDealStageDto } from './dto/update-deal-stage.dto';
import { QueryDealsDto } from './dto/query-deals.dto';

@ApiTags('Deals')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('deals')
export class DealsController {
  constructor(private readonly dealsService: DealsService) {}

  @Get()
  @ApiOperation({ summary: 'List deals, optionally filtered by stage or owner' })
  @ApiResponse({ status: 200, description: 'List of deals' })
  findAll(@CompanyId() companyId: string, @Query() query: QueryDealsDto) {
    return this.dealsService.findAll(companyId, query);
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get a single deal by ID, with contact and tasks' })
  @ApiResponse({ status: 200, description: 'Deal record' })
  @ApiResponse({ status: 404, description: 'Deal not found' })
  findOne(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.dealsService.findOne(companyId, id);
  }

  @Post()
  @ApiOperation({ summary: 'Create a new deal' })
  @ApiResponse({ status: 201, description: 'Created deal record' })
  create(@CompanyId() companyId: string, @Body() dto: CreateDealDto) {
    return this.dealsService.create(companyId, dto);
  }

  @Patch(':id')
  @ApiOperation({ summary: 'Update deal fields (title, value, owner, etc.)' })
  @ApiResponse({ status: 200, description: 'Updated deal record' })
  @ApiResponse({ status: 404, description: 'Deal not found' })
  update(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateDealDto) {
    return this.dealsService.update(companyId, id, dto);
  }

  @Patch(':id/stage')
  @ApiOperation({ summary: 'Move a deal to a different pipeline stage' })
  @ApiResponse({ status: 200, description: 'Updated deal record' })
  @ApiResponse({ status: 404, description: 'Deal not found' })
  updateStage(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateDealStageDto) {
    return this.dealsService.updateStage(companyId, id, dto.stage);
  }

  @Delete(':id')
  @ApiOperation({ summary: 'Delete a deal' })
  @ApiResponse({ status: 200, description: 'Deal deleted' })
  @ApiResponse({ status: 404, description: 'Deal not found' })
  remove(@CompanyId() companyId: string, @Param('id') id: string, @CurrentUser() actor: AuthenticatedUser) {
    return this.dealsService.remove(companyId, id, actor.id);
  }
}
