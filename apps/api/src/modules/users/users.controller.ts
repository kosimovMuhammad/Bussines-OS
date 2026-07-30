import { Body, Controller, Delete, Get, Param, Patch, Post, UseGuards } from '@nestjs/common';
import { Role } from '@prisma/client';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { UsersService } from './users.service';
import { InviteUserDto } from './dto/invite-user.dto';
import { UpdateUserDto } from './dto/update-user.dto';
import { UpdateRoleDto } from './dto/update-role.dto';

@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('users')
export class UsersController {
  constructor(private readonly usersService: UsersService) {}

  @Get()
  findAll(@CompanyId() companyId: string) {
    return this.usersService.findAll(companyId);
  }

  @Get(':id')
  findOne(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.usersService.findOne(companyId, id);
  }

  @Roles(Role.OWNER, Role.ADMIN)
  @UseGuards(RolesGuard)
  @Post('invite')
  invite(@CompanyId() companyId: string, @Body() dto: InviteUserDto) {
    return this.usersService.invite(companyId, dto);
  }

  @Patch(':id')
  update(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateUserDto) {
    return this.usersService.update(companyId, id, dto);
  }

  @Roles(Role.OWNER, Role.ADMIN)
  @UseGuards(RolesGuard)
  @Patch(':id/role')
  updateRole(
    @CompanyId() companyId: string,
    @Param('id') id: string,
    @Body() dto: UpdateRoleDto,
    @CurrentUser() actor: AuthenticatedUser,
  ) {
    return this.usersService.updateRole(companyId, id, dto.role, actor.id);
  }

  @Roles(Role.OWNER, Role.ADMIN)
  @UseGuards(RolesGuard)
  @Delete(':id')
  remove(@CompanyId() companyId: string, @Param('id') id: string, @CurrentUser() actor: AuthenticatedUser) {
    return this.usersService.remove(companyId, id, actor.id);
  }
}
