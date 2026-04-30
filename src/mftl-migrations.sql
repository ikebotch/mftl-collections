CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425100048_InitialCreate') THEN
    CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425100048_InitialCreate') THEN
    ALTER TABLE "Events" ADD "DisplayImageUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425100048_InitialCreate') THEN
    ALTER TABLE "Events" ADD "ReceiptLogoUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425100048_InitialCreate') THEN
    CREATE TABLE "Settlements" (
        "Id" uuid NOT NULL,
        "CollectorId" uuid NOT NULL,
        "Amount" numeric NOT NULL,
        "Currency" text NOT NULL,
        "SettlementDate" timestamp with time zone NOT NULL,
        "Status" text NOT NULL,
        "Note" text,
        "ReviewedByUserId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ModifiedAt" timestamp with time zone,
        "CreatedBy" text,
        "ModifiedBy" text,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_Settlements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Settlements_Users_CollectorId" FOREIGN KEY ("CollectorId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Settlements_Users_ReviewedByUserId" FOREIGN KEY ("ReviewedByUserId") REFERENCES "Users" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425100048_InitialCreate') THEN
    CREATE INDEX "IX_Settlements_CollectorId" ON "Settlements" ("CollectorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425100048_InitialCreate') THEN
    CREATE INDEX "IX_Settlements_ReviewedByUserId" ON "Settlements" ("ReviewedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425100048_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260425100048_InitialCreate', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425161502_AddIsActiveToRecipientFund') THEN
    ALTER TABLE "RecipientFunds" ADD "IsActive" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425161502_AddIsActiveToRecipientFund') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260425161502_AddIsActiveToRecipientFund', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425162118_AddUserPhoneNumber') THEN
    ALTER TABLE "Users" ADD "PhoneNumber" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425162118_AddUserPhoneNumber') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260425162118_AddUserPhoneNumber', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426074905_ExpandUserAndAddAuditLog') THEN
    ALTER TABLE "UserScopeAssignments" ADD "CollectorId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426074905_ExpandUserAndAddAuditLog') THEN
    ALTER TABLE "Users" ADD "InviteStatus" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426074905_ExpandUserAndAddAuditLog') THEN
    ALTER TABLE "Users" ADD "IsSuspended" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426074905_ExpandUserAndAddAuditLog') THEN
    ALTER TABLE "Users" ADD "LastLoginAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426074905_ExpandUserAndAddAuditLog') THEN
    CREATE TABLE "AuditLogs" (
        "Id" uuid NOT NULL,
        "UserId" uuid,
        "Action" text NOT NULL,
        "EntityName" text NOT NULL,
        "EntityId" text NOT NULL,
        "Details" text NOT NULL,
        "PerformedBy" text NOT NULL,
        "TenantId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ModifiedAt" timestamp with time zone,
        "CreatedBy" text,
        "ModifiedBy" text,
        CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426074905_ExpandUserAndAddAuditLog') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260426074905_ExpandUserAndAddAuditLog', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426091754_AddBranchesTable') THEN
    CREATE TABLE "Branches" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "Identifier" text NOT NULL,
        "Location" text,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ModifiedAt" timestamp with time zone,
        "CreatedBy" text,
        "ModifiedBy" text,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_Branches" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Branches_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426091754_AddBranchesTable') THEN
    CREATE INDEX "IX_Branches_TenantId" ON "Branches" ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426091754_AddBranchesTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260426091754_AddBranchesTable', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    ALTER TABLE "Settlements" ADD "BranchId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    ALTER TABLE "Receipts" ADD "BranchId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    ALTER TABLE "Events" ADD "BranchId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    ALTER TABLE "Contributors" ADD "BranchId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    ALTER TABLE "Contributions" ADD "BranchId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    CREATE INDEX "IX_Settlements_BranchId" ON "Settlements" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    CREATE INDEX "IX_Receipts_BranchId" ON "Receipts" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    CREATE INDEX "IX_Events_BranchId" ON "Events" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    CREATE INDEX "IX_Contributors_BranchId" ON "Contributors" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    CREATE INDEX "IX_Contributions_BranchId" ON "Contributions" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    ALTER TABLE "Contributions" ADD CONSTRAINT "FK_Contributions_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    ALTER TABLE "Contributors" ADD CONSTRAINT "FK_Contributors_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    ALTER TABLE "Events" ADD CONSTRAINT "FK_Events_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    ALTER TABLE "Receipts" ADD CONSTRAINT "FK_Receipts_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    ALTER TABLE "Settlements" ADD CONSTRAINT "FK_Settlements_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426092411_MakeBranchesOperational') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260426092411_MakeBranchesOperational', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426095149_AddBranchIdToRecipientFund') THEN
    ALTER TABLE "RecipientFunds" ADD "BranchId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426095149_AddBranchIdToRecipientFund') THEN
    CREATE INDEX "IX_RecipientFunds_BranchId" ON "RecipientFunds" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426095149_AddBranchIdToRecipientFund') THEN
    ALTER TABLE "RecipientFunds" ADD CONSTRAINT "FK_RecipientFunds_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426095149_AddBranchIdToRecipientFund') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260426095149_AddBranchIdToRecipientFund', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "Contributions" DROP CONSTRAINT "FK_Contributions_Branches_BranchId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "Contributors" DROP CONSTRAINT "FK_Contributors_Branches_BranchId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "Events" DROP CONSTRAINT "FK_Events_Branches_BranchId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "Receipts" DROP CONSTRAINT "FK_Receipts_Branches_BranchId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "RecipientFunds" DROP CONSTRAINT "FK_RecipientFunds_Branches_BranchId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "Settlements" DROP CONSTRAINT "FK_Settlements_Branches_BranchId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    UPDATE "Settlements" SET "BranchId" = '00000000-0000-0000-0000-000000000000' WHERE "BranchId" IS NULL;
    ALTER TABLE "Settlements" ALTER COLUMN "BranchId" SET NOT NULL;
    ALTER TABLE "Settlements" ALTER COLUMN "BranchId" SET DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    UPDATE "RecipientFunds" SET "BranchId" = '00000000-0000-0000-0000-000000000000' WHERE "BranchId" IS NULL;
    ALTER TABLE "RecipientFunds" ALTER COLUMN "BranchId" SET NOT NULL;
    ALTER TABLE "RecipientFunds" ALTER COLUMN "BranchId" SET DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    UPDATE "Receipts" SET "BranchId" = '00000000-0000-0000-0000-000000000000' WHERE "BranchId" IS NULL;
    ALTER TABLE "Receipts" ALTER COLUMN "BranchId" SET NOT NULL;
    ALTER TABLE "Receipts" ALTER COLUMN "BranchId" SET DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    UPDATE "Events" SET "BranchId" = '00000000-0000-0000-0000-000000000000' WHERE "BranchId" IS NULL;
    ALTER TABLE "Events" ALTER COLUMN "BranchId" SET NOT NULL;
    ALTER TABLE "Events" ALTER COLUMN "BranchId" SET DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    UPDATE "Contributors" SET "BranchId" = '00000000-0000-0000-0000-000000000000' WHERE "BranchId" IS NULL;
    ALTER TABLE "Contributors" ALTER COLUMN "BranchId" SET NOT NULL;
    ALTER TABLE "Contributors" ALTER COLUMN "BranchId" SET DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    UPDATE "Contributions" SET "BranchId" = '00000000-0000-0000-0000-000000000000' WHERE "BranchId" IS NULL;
    ALTER TABLE "Contributions" ALTER COLUMN "BranchId" SET NOT NULL;
    ALTER TABLE "Contributions" ALTER COLUMN "BranchId" SET DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "Contributions" ADD CONSTRAINT "FK_Contributions_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "Contributors" ADD CONSTRAINT "FK_Contributors_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "Events" ADD CONSTRAINT "FK_Events_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "Receipts" ADD CONSTRAINT "FK_Receipts_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "RecipientFunds" ADD CONSTRAINT "FK_RecipientFunds_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    ALTER TABLE "Settlements" ADD CONSTRAINT "FK_Settlements_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426100834_EnforceMandatoryBranchId') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260426100834_EnforceMandatoryBranchId', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430080501_AddPermissionsTable') THEN
    CREATE TABLE "Permissions" (
        "Id" uuid NOT NULL,
        "Key" text NOT NULL,
        "Description" text NOT NULL,
        "Group" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ModifiedAt" timestamp with time zone,
        "CreatedBy" text,
        "ModifiedBy" text,
        CONSTRAINT "PK_Permissions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430080501_AddPermissionsTable') THEN
    CREATE TABLE "RolePermissions" (
        "Id" uuid NOT NULL,
        "RoleName" text NOT NULL,
        "PermissionKey" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ModifiedAt" timestamp with time zone,
        "CreatedBy" text,
        "ModifiedBy" text,
        CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430080501_AddPermissionsTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260430080501_AddPermissionsTable', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430114332_AddProcessedExternalPaymentCallback') THEN
    CREATE TABLE "ProcessedExternalPaymentCallbacks" (
        "Id" uuid NOT NULL,
        "CallbackEventId" character varying(160) NOT NULL,
        "PaymentServicePaymentId" character varying(64) NOT NULL,
        "TenantId" uuid NOT NULL,
        "ContributionId" uuid NOT NULL,
        "Provider" character varying(64) NOT NULL,
        "ProviderReference" character varying(256) NOT NULL,
        "ProviderTransactionId" character varying(256) NOT NULL,
        "ExternalReference" character varying(256) NOT NULL,
        "EventType" character varying(64) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "PayloadHash" character varying(256) NOT NULL,
        "Status" character varying(64) NOT NULL,
        "Error" text,
        "OccurredAt" timestamp with time zone,
        "ProcessedAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ModifiedAt" timestamp with time zone,
        "CreatedBy" text,
        "ModifiedBy" text,
        CONSTRAINT "PK_ProcessedExternalPaymentCallbacks" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430114332_AddProcessedExternalPaymentCallback') THEN
    CREATE UNIQUE INDEX "IX_ProcessedExternalPaymentCallbacks_CallbackEventId" ON "ProcessedExternalPaymentCallbacks" ("CallbackEventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430114332_AddProcessedExternalPaymentCallback') THEN
    CREATE INDEX "IX_ProcessedExternalPaymentCallbacks_PaymentServicePaymentId" ON "ProcessedExternalPaymentCallbacks" ("PaymentServicePaymentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430114332_AddProcessedExternalPaymentCallback') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260430114332_AddProcessedExternalPaymentCallback', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430214200_AddCollectorPins') THEN
    CREATE TABLE "CollectorPins" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "PinHash" text NOT NULL,
        "LastVerifiedAt" timestamp with time zone,
        "FailedAttempts" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ModifiedAt" timestamp with time zone,
        "CreatedBy" text,
        "ModifiedBy" text,
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_CollectorPins" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CollectorPins_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430214200_AddCollectorPins') THEN
    CREATE UNIQUE INDEX "IX_CollectorPins_UserId_TenantId" ON "CollectorPins" ("UserId", "TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430214200_AddCollectorPins') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260430214200_AddCollectorPins', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430214926_RepairOutboxMessagesPayloadJsonSchema') THEN

    DO $$ 
    BEGIN 
        -- 1. Fix PayloadJson column mismatch
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'PayloadJson') THEN
            ALTER TABLE "OutboxMessages" ADD COLUMN "PayloadJson" text NOT NULL DEFAULT '{}';
            
            -- Migrate data if the old 'Payload' column exists
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'Payload') THEN
                UPDATE "OutboxMessages" SET "PayloadJson" = "Payload"::text;
            END IF;
        END IF;

        -- 2. Ensure Status is integer (Enum) not varchar
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'Status' AND data_type = 'character varying') THEN
            ALTER TABLE "OutboxMessages" ALTER COLUMN "Status" TYPE integer USING (
                CASE "Status"
                    WHEN 'Pending' THEN 0
                    WHEN 'Processing' THEN 1
                    WHEN 'Sent' THEN 2
                    WHEN 'Failed' THEN 3
                    WHEN 'DeadLetter' THEN 4
                    ELSE 0
                END
            );
        END IF;
    END $$;

    ALTER TABLE "OutboxMessages" ALTER COLUMN "PayloadJson" DROP DEFAULT;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430214926_RepairOutboxMessagesPayloadJsonSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260430214926_RepairOutboxMessagesPayloadJsonSchema', '10.0.7');
    END IF;
END $EF$;
COMMIT;

