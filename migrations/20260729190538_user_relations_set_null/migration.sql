-- DropForeignKey
ALTER TABLE "time_entries" DROP CONSTRAINT "time_entries_userId_fkey";

-- AlterTable
ALTER TABLE "time_entries" ALTER COLUMN "userId" DROP NOT NULL;

-- AddForeignKey
ALTER TABLE "time_entries" ADD CONSTRAINT "time_entries_userId_fkey" FOREIGN KEY ("userId") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;
