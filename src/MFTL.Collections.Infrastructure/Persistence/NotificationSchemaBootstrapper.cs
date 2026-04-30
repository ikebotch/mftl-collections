using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MFTL.Collections.Infrastructure.Configuration;
using Npgsql;

namespace MFTL.Collections.Infrastructure.Persistence;

public sealed class NotificationSchemaBootstrapper(
    IOptions<DatabaseOptions> databaseOptions,
    ILogger<NotificationSchemaBootstrapper> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(databaseOptions.Value.ConnectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(databaseOptions.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            CREATE TABLE IF NOT EXISTS "OutboxMessages" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "BranchId" uuid NULL,
                "AggregateId" uuid NOT NULL,
                "AggregateType" character varying(128) NOT NULL,
                "EventType" character varying(128) NOT NULL,
                "Payload" jsonb NOT NULL,
                "CorrelationId" character varying(128) NOT NULL,
                "Status" character varying(32) NOT NULL,
                "AttemptCount" integer NOT NULL DEFAULT 0,
                "Priority" integer NOT NULL DEFAULT 0,
                "NextAttemptAt" timestamp with time zone NULL,
                "ProcessedAt" timestamp with time zone NULL,
                "LastError" character varying(2000) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ModifiedAt" timestamp with time zone NULL,
                "CreatedBy" text NULL,
                "ModifiedBy" text NULL,
                CONSTRAINT "PK_OutboxMessages" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS "Notifications" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "BranchId" uuid NULL,
                "OutboxMessageId" uuid NULL,
                "ReceiptId" uuid NULL,
                "PaymentId" uuid NULL,
                "ContributionId" uuid NULL,
                "Channel" character varying(32) NOT NULL,
                "Status" character varying(32) NOT NULL,
                "TemplateKey" character varying(100) NOT NULL,
                "Recipient" character varying(256) NOT NULL,
                "RecipientPhone" character varying(64) NULL,
                "RecipientEmail" character varying(256) NULL,
                "Subject" character varying(500) NULL,
                "Body" text NOT NULL,
                "ProviderMessageId" character varying(256) NULL,
                "Error" character varying(1000) NULL,
                "SentAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ModifiedAt" timestamp with time zone NULL,
                "CreatedBy" text NULL,
                "ModifiedBy" text NULL,
                CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Status_NextAttemptAt_CreatedAt" ON "OutboxMessages" ("Status", "NextAttemptAt", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_TenantId" ON "OutboxMessages" ("TenantId");
            CREATE INDEX IF NOT EXISTS "IX_Notifications_TenantId" ON "Notifications" ("TenantId");
            CREATE INDEX IF NOT EXISTS "IX_Notifications_OutboxMessageId" ON "Notifications" ("OutboxMessageId");

            ALTER TABLE "OutboxMessages"
                ADD COLUMN IF NOT EXISTS "AggregateType" character varying(128) NOT NULL DEFAULT '';

            ALTER TABLE "Notifications"
                ADD COLUMN IF NOT EXISTS "ReceiptId" uuid NULL;

            ALTER TABLE "Notifications"
                ADD COLUMN IF NOT EXISTS "PaymentId" uuid NULL;

            ALTER TABLE "Notifications"
                ADD COLUMN IF NOT EXISTS "ContributionId" uuid NULL;

            ALTER TABLE "Notifications"
                ADD COLUMN IF NOT EXISTS "Recipient" character varying(256) NOT NULL DEFAULT '';
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Notification schema bootstrap completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
