import { Body, Controller, Delete, Get, Param, Patch, Post, Query, UseGuards } from '@nestjs/common';
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

@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('deals')
export class DealsController {
  constructor(private readonly dealsService: DealsService) {}

  @Get()
  findAll(@CompanyId() companyId: string, @Query() query: QueryDealsDto) {
    return this.dealsService.findAll(companyId, query);
  }

  @Get(':id')
  findOne(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.dealsService.findOne(companyId, id);
  }

  @Post()
  create(@CompanyId() companyId: string, @Body() dto: CreateDealDto) {
    return this.dealsService.create(companyId, dto);
  }

  @Patch(':id')
  update(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateDealDto) {
    return this.dealsService.update(companyId, id, dto);
  }

  @Patch(':id/stage')
  updateStage(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateDealStageDto) {
    return this.dealsService.updateStage(companyId, id, dto.stage);
  }

  @Delete(':id')
  remove(@CompanyId() companyId: string, @Param('id') id: string, @CurrentUser() actor: AuthenticatedUser) {
    return this.dealsService.remove(companyId, id, actor.id);
  }
}
