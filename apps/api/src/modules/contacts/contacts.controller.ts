import { Body, Controller, Delete, Get, Param, Patch, Post, Query, UseGuards } from '@nestjs/common';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { ContactsService } from './contacts.service';
import { CreateContactDto } from './dto/create-contact.dto';
import { UpdateContactDto } from './dto/update-contact.dto';
import { QueryContactsDto } from './dto/query-contacts.dto';

@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('contacts')
export class ContactsController {
  constructor(private readonly contactsService: ContactsService) {}

  @Get()
  findAll(@CompanyId() companyId: string, @Query() query: QueryContactsDto) {
    return this.contactsService.findAll(companyId, query);
  }

  @Get(':id')
  findOne(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.contactsService.findOne(companyId, id);
  }

  @Get(':id/timeline')
  timeline(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.contactsService.timeline(companyId, id);
  }

  @Post()
  create(@CompanyId() companyId: string, @Body() dto: CreateContactDto) {
    return this.contactsService.create(companyId, dto);
  }

  @Patch(':id')
  update(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateContactDto) {
    return this.contactsService.update(companyId, id, dto);
  }

  @Delete(':id')
  remove(@CompanyId() companyId: string, @Param('id') id: string, @CurrentUser() actor: AuthenticatedUser) {
    return this.contactsService.remove(companyId, id, actor.id);
  }
}
