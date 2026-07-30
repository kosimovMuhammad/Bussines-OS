import { Controller, Get, Post, Query, UseGuards } from '@nestjs/common';
import { ApiBearerAuth, ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompanyGuard } from '../../common/guards/company.guard';
import { CompanyId } from '../../common/decorators/company-id.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { AuthenticatedUser } from '../../common/interfaces/authenticated-user.interface';
import { CalendarConnectionService } from './calendar-connection.service';

@ApiTags('Calendar')
@Controller('calendar')
export class CalendarConnectionController {
  constructor(private readonly calendarConnectionService: CalendarConnectionService) {}

  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard, CompanyGuard)
  @Get('connect/google')
  @ApiOperation({ summary: 'Get the Google OAuth consent URL for connecting Google Calendar (separate opt-in from Gmail)' })
  @ApiResponse({ status: 200, description: 'Google OAuth consent URL' })
  connect(@CurrentUser() user: AuthenticatedUser) {
    return { url: this.calendarConnectionService.getAuthUrl(user.id, user.companyId) };
  }

  @Get('connect/google/callback')
  @ApiOperation({
    summary: 'Google Calendar OAuth callback — exchanges code, stores the connection, registers the initial watch channel',
  })
  @ApiResponse({ status: 200, description: 'Google Calendar connected' })
  @ApiResponse({ status: 401, description: 'Invalid or expired state' })
  @ApiResponse({ status: 502, description: 'Token exchange with Google failed' })
  callback(@Query('code') code: string, @Query('state') state: string) {
    return this.calendarConnectionService.handleCallback(code, state);
  }

  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard, CompanyGuard)
  @Get('status')
  @ApiOperation({ summary: "Get the current user's Google Calendar connection status and sync health" })
  @ApiResponse({ status: 200, description: 'Connection status' })
  status(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser) {
    return this.calendarConnectionService.getStatus(companyId, user.id);
  }

  @ApiBearerAuth()
  @UseGuards(JwtAuthGuard, CompanyGuard)
  @Post('disconnect')
  @ApiOperation({
    summary: 'Disconnect Google Calendar — stops the watch channel and resets sync status (keeps googleEventId history)',
  })
  @ApiResponse({ status: 200, description: 'Disconnected' })
  disconnect(@CompanyId() companyId: string, @CurrentUser() user: AuthenticatedUser) {
    return this.calendarConnectionService.disconnect(companyId, user.id);
  }
}
