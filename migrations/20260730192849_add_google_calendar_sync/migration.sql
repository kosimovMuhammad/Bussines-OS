-- CreateEnum
CREATE TYPE "MeetingSyncStatus" AS ENUM ('NOT_SYNCED', 'SYNCED', 'SYNC_FAILED');

-- AlterEnum
ALTER TYPE "OAuthProvider" ADD VALUE 'GOOGLE_CALENDAR';

-- AlterTable
ALTER TABLE "meetings" ADD COLUMN     "googleCalendarId" TEXT,
ADD COLUMN     "googleEventId" TEXT,
ADD COLUMN     "lastSyncedAt" TIMESTAMP(3),
ADD COLUMN     "syncError" TEXT,
ADD COLUMN     "syncStatus" "MeetingSyncStatus" NOT NULL DEFAULT 'NOT_SYNCED';

-- CreateTable
CREATE TABLE "calendar_sync_tokens" (
    "id" TEXT NOT NULL,
    "companyId" TEXT NOT NULL,
    "userId" TEXT NOT NULL,
    "resourceId" TEXT NOT NULL,
    "syncToken" TEXT,
    "channelId" TEXT NOT NULL,
    "channelToken" TEXT NOT NULL,
    "channelExpiration" TIMESTAMP(3) NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "calendar_sync_tokens_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "calendar_sync_tokens_userId_key" ON "calendar_sync_tokens"("userId");

-- CreateIndex
CREATE UNIQUE INDEX "calendar_sync_tokens_channelId_key" ON "calendar_sync_tokens"("channelId");

-- CreateIndex
CREATE INDEX "calendar_sync_tokens_companyId_idx" ON "calendar_sync_tokens"("companyId");

-- CreateIndex
CREATE UNIQUE INDEX "meetings_googleEventId_key" ON "meetings"("googleEventId");

-- AddForeignKey
ALTER TABLE "calendar_sync_tokens" ADD CONSTRAINT "calendar_sync_tokens_companyId_fkey" FOREIGN KEY ("companyId") REFERENCES "companies"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "calendar_sync_tokens" ADD CONSTRAINT "calendar_sync_tokens_userId_fkey" FOREIGN KEY ("userId") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

