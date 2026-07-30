import { Role } from '@prisma/client';

export interface AuthenticatedUser {
  id: string;
  companyId: string;
  role: Role;
}
