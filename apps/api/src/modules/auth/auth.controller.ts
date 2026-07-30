import { Body, Controller, HttpCode, Post, Req, Res, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import type { Request, Response } from 'express';
import { AuthService } from './auth.service';
import { RegisterDto } from './dto/register.dto';
import { LoginDto } from './dto/login.dto';

const REFRESH_COOKIE = 'refresh_token';
const REFRESH_COOKIE_MAX_AGE = 30 * 24 * 60 * 60 * 1000;

@ApiTags('Auth')
@Controller('auth')
export class AuthController {
  constructor(
    private readonly authService: AuthService,
    private readonly config: ConfigService,
  ) {}

  @Post('register')
  @ApiOperation({ summary: 'Register a new company (tenant) and its first user (Owner)' })
  @ApiResponse({ status: 201, description: 'Company and user created, access token returned' })
  async register(@Body() dto: RegisterDto, @Res({ passthrough: true }) res: Response) {
    const { accessToken, refreshToken, user } = await this.authService.register(dto);
    this.setRefreshCookie(res, refreshToken);
    return { accessToken, user };
  }

  @Post('login')
  @HttpCode(200)
  @ApiOperation({ summary: 'Log in with email and password' })
  @ApiResponse({ status: 200, description: 'Access token returned, refresh token set as an HttpOnly cookie' })
  @ApiResponse({ status: 401, description: 'Invalid credentials' })
  async login(@Body() dto: LoginDto, @Res({ passthrough: true }) res: Response) {
    const { accessToken, refreshToken, user } = await this.authService.login(dto);
    this.setRefreshCookie(res, refreshToken);
    return { accessToken, user };
  }

  @Post('refresh')
  @HttpCode(200)
  @ApiOperation({ summary: 'Exchange the refresh token cookie for a new access token' })
  @ApiResponse({ status: 200, description: 'New access token issued' })
  @ApiResponse({ status: 401, description: 'Missing or invalid refresh token' })
  async refresh(@Req() req: Request, @Res({ passthrough: true }) res: Response) {
    const rawToken = req.cookies?.[REFRESH_COOKIE];
    if (!rawToken) {
      throw new UnauthorizedException('Refresh token нест');
    }
    const { accessToken, refreshToken, user } = await this.authService.refresh(rawToken);
    this.setRefreshCookie(res, refreshToken);
    return { accessToken, user };
  }

  @Post('logout')
  @HttpCode(200)
  @ApiOperation({ summary: 'Revoke the refresh token and clear the session cookie' })
  @ApiResponse({ status: 200, description: 'Logged out' })
  async logout(@Req() req: Request, @Res({ passthrough: true }) res: Response) {
    const rawToken = req.cookies?.[REFRESH_COOKIE];
    await this.authService.logout(rawToken);
    res.clearCookie(REFRESH_COOKIE, { path: '/' });
    return { success: true };
  }

  private setRefreshCookie(res: Response, token: string) {
    res.cookie(REFRESH_COOKIE, token, {
      httpOnly: true,
      secure: this.config.get<string>('nodeEnv') === 'production',
      sameSite: 'lax',
      path: '/',
      maxAge: REFRESH_COOKIE_MAX_AGE,
    });
  }
}
