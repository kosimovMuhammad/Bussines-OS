-- CreateEnum
CREATE TYPE "AiJobStatus" AS ENUM ('PENDING', 'COMPLETED', 'FAILED');

-- CreateEnum
CREATE TYPE "QueryIntent" AS ENUM ('STALE_CONTACTS', 'TOP_SALESPERSON', 'REVENUE_BY_PERIOD');

-- CreateTable
CREATE TABLE "ai_query_results" (
    "id" TEXT NOT NULL,
    "companyId" TEXT NOT NULL,
    "question" TEXT NOT NULL,
    "intent" "QueryIntent",
    "status" "AiJobStatus" NOT NULL DEFAULT 'PENDING',
    "resultRows" JSONB,
    "answer" TEXT,
    "errorMessage" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "completedAt" TIMESTAMP(3),

    CONSTRAINT "ai_query_results_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "ai_email_drafts" (
    "id" TEXT NOT NULL,
    "companyId" TEXT NOT NULL,
    "contactId" TEXT NOT NULL,
    "context" TEXT,
    "status" "AiJobStatus" NOT NULL DEFAULT 'PENDING',
    "subject" TEXT,
    "body" TEXT,
    "errorMessage" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "completedAt" TIMESTAMP(3),

    CONSTRAINT "ai_email_drafts_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "ai_quotations" (
    "id" TEXT NOT NULL,
    "companyId" TEXT NOT NULL,
    "dealId" TEXT NOT NULL,
    "items" JSONB NOT NULL,
    "totalAmount" DECIMAL(65,30),
    "status" "AiJobStatus" NOT NULL DEFAULT 'PENDING',
    "fileAssetId" TEXT,
    "errorMessage" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "completedAt" TIMESTAMP(3),

    CONSTRAINT "ai_quotations_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "ai_query_results_companyId_idx" ON "ai_query_results"("companyId");

-- CreateIndex
CREATE INDEX "ai_email_drafts_companyId_idx" ON "ai_email_drafts"("companyId");

-- CreateIndex
CREATE INDEX "ai_email_drafts_contactId_idx" ON "ai_email_drafts"("contactId");

-- CreateIndex
CREATE UNIQUE INDEX "ai_quotations_fileAssetId_key" ON "ai_quotations"("fileAssetId");

-- CreateIndex
CREATE INDEX "ai_quotations_companyId_idx" ON "ai_quotations"("companyId");

-- CreateIndex
CREATE INDEX "ai_quotations_dealId_idx" ON "ai_quotations"("dealId");

-- AddForeignKey
ALTER TABLE "ai_query_results" ADD CONSTRAINT "ai_query_results_companyId_fkey" FOREIGN KEY ("companyId") REFERENCES "companies"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ai_email_drafts" ADD CONSTRAINT "ai_email_drafts_companyId_fkey" FOREIGN KEY ("companyId") REFERENCES "companies"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ai_email_drafts" ADD CONSTRAINT "ai_email_drafts_contactId_fkey" FOREIGN KEY ("contactId") REFERENCES "contacts"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ai_quotations" ADD CONSTRAINT "ai_quotations_companyId_fkey" FOREIGN KEY ("companyId") REFERENCES "companies"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ai_quotations" ADD CONSTRAINT "ai_quotations_dealId_fkey" FOREIGN KEY ("dealId") REFERENCES "deals"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ai_quotations" ADD CONSTRAINT "ai_quotations_fileAssetId_fkey" FOREIGN KEY ("fileAssetId") REFERENCES "file_assets"("id") ON DELETE SET NULL ON UPDATE CASCADE;
