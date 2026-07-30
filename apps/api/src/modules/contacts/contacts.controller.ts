import { Body, Controller, Delete, Get, Param, Patch, Post, Query, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { ContactsService } from './contacts.service';
import { CreateContactDto } from './dto/create-contact.dto';
import { UpdateContactDto } from './dto/update-contact.dto';
import { QueryContactsDto } from './dto/query-contacts.dto';

@ApiTags('Contacts')
@ApiBearerAuth()
@UseGuards(JwtAuthGuard, CompanyGuard)
@Controller('contacts')
export class ContactsController {
  constructor(private readonly contactsService: ContactsService) {}

  @Get()
  @ApiOperation({ summary: 'List contacts, with search/filter/pagination' })
  @ApiResponse({ status: 200, description: 'Paginated list of contacts' })
  findAll(@CompanyId() companyId: string, @Query() query: QueryContactsDto) {
    return this.contactsService.findAll(companyId, query);
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get a single contact by ID' })
  @ApiResponse({ status: 200, description: 'Contact record' })
  @ApiResponse({ status: 404, description: 'Contact not found' })
  findOne(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.contactsService.findOne(companyId, id);
  }

  @Get(':id/timeline')
  @ApiOperation({ summary: "Get a contact's activity timeline (communications, deals, tasks)" })
  @ApiResponse({ status: 200, description: 'Timeline entries' })
  @ApiResponse({ status: 404, description: 'Contact not found' })
  timeline(@CompanyId() companyId: string, @Param('id') id: string) {
    return this.contactsService.timeline(companyId, id);
  }

  @Post()
  @ApiOperation({ summary: 'Create a new contact' })
  @ApiResponse({ status: 201, description: 'Created contact record' })
  create(@CompanyId() companyId: string, @Body() dto: CreateContactDto) {
    return this.contactsService.create(companyId, dto);
  }

  @Patch(':id')
  @ApiOperation({ summary: 'Update a contact' })
  @ApiResponse({ status: 200, description: 'Updated contact record' })
  @ApiResponse({ status: 404, description: 'Contact not found' })
  update(@CompanyId() companyId: string, @Param('id') id: string, @Body() dto: UpdateContactDto) {
    return this.contactsService.update(companyId, id, dto);
  }

  @Delete(':id')
  @ApiOperation({ summary: 'Delete a contact' })
  @ApiResponse({ status: 200, description: 'Contact deleted' })
  @ApiResponse({ status: 404, description: 'Contact not found' })
  remove(@CompanyId() companyId: string, @Param('id') id: string, @CurrentUser() actor: AuthenticatedUser) {
    return this.contactsService.remove(companyId, id, actor.id);
  }
}
