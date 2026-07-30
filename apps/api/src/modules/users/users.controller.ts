import { Body, Controller, Delete, Get, Param, Patch, Post, UseGuards } from '@nestjs/common';
import { Role } from '@prisma/client';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
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

@ApiTags('Users & Roles')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('users')
export class UsersController {
  constructor(private readonly usersService: UsersService) {}

  @Get()
  @ApiOperation({ summary: 'List all users in the current company' })
  @ApiResponse({ status: 200, description: 'List of users' })
  findAll(@CompanyId() companyId: string) {
    return this.usersService.findAll(companyId);
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get a single user by ID' })
  @ApiResponse({ status: 200, description: 'User record' })
  @ApiResponse({ status: 404, description: 'User not found' })
  findOne(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.usersService.findOne(companyId, id);
  }

  @Roles(Role.OWNER, Role.ADMIN)
  @UseGuards(RolesGuard)
  @Post('invite')
  @ApiOperation({ summary: 'Invite a new user to the company (Owner/Admin only)' })
  @ApiResponse({ status: 201, description: 'Invited user created with INVITED status' })
  @ApiResponse({ status: 403, description: 'Insufficient role' })
  @ApiResponse({ status: 409, description: 'Email already in use' })
  invite(@CompanyId() companyId: string, @Body() dto: InviteUserDto) {
    return this.usersService.invite(companyId, dto);
  }

  @Patch(':id')
  @ApiOperation({ summary: "Update a user's name, avatar, or status" })
  @ApiResponse({ status: 200, description: 'Updated user record' })
  @ApiResponse({ status: 404, description: 'User not found' })
  update(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateUserDto) {
    return this.usersService.update(companyId, id, dto);
  }

  @Roles(Role.OWNER, Role.ADMIN)
  @UseGuards(RolesGuard)
  @Patch(':id/role')
  @ApiOperation({ summary: "Change a user's role (Owner/Admin only)" })
  @ApiResponse({ status: 200, description: 'Updated user record' })
  @ApiResponse({ status: 403, description: 'Insufficient role' })
  @ApiResponse({ status: 404, description: 'User not found' })
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
  @ApiOperation({ summary: 'Remove a user from the company (Owner/Admin only)' })
  @ApiResponse({ status: 200, description: 'User removed' })
  @ApiResponse({ status: 403, description: 'Insufficient role' })
  @ApiResponse({ status: 404, description: 'User not found' })
  remove(@CompanyId() companyId: string, @Param('id') id: string, @CurrentUser() actor: AuthenticatedUser) {
    return this.usersService.remove(companyId, id, actor.id);
  }
}
