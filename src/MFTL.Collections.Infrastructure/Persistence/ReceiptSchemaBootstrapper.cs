using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MFTL.Collections.Infrastructure.Configuration;
using Npgsql;

namespace MFTL.Collections.Infrastructure.Persistence;

public sealed class ReceiptSchemaBootstrapper(
    IOptions<DatabaseOptions> databaseOptions,
    ILogger<ReceiptSchemaBootstrapper> logger) : IHostedService
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
            CREATE TABLE IF NOT EXISTS "Receipts" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "EventId" uuid NOT NULL,
                "RecipientFundId" uuid NOT NULL,
                "ContributionId" uuid NOT NULL,
                "PaymentId" uuid NULL,
                "RecordedByUserId" uuid NULL,
                "ReceiptNumber" character varying(64) NOT NULL,
                "IssuedAt" timestamp with time zone NOT NULL,
                "Status" character varying(32) NOT NULL,
                "Note" character varying(1000) NULL,
                "Metadata" jsonb NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ModifiedAt" timestamp with time zone NULL,
                "CreatedBy" text NULL,
                "ModifiedBy" text NULL,
                CONSTRAINT "PK_Receipts" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Receipts_ReceiptNumber" ON "Receipts" ("ReceiptNumber");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Receipts_ContributionId" ON "Receipts" ("ContributionId");
            CREATE INDEX IF NOT EXISTS "IX_Receipts_TenantId" ON "Receipts" ("TenantId");
            CREATE INDEX IF NOT EXISTS "IX_Receipts_EventId" ON "Receipts" ("EventId");
            CREATE INDEX IF NOT EXISTS "IX_Receipts_RecipientFundId" ON "Receipts" ("RecipientFundId");
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        logger.LogInformation("Receipt schema bootstrap completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
