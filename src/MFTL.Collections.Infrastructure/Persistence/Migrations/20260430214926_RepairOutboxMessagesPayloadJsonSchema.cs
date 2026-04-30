using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairOutboxMessagesPayloadJsonSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$ 
BEGIN 
    -- 1. Fix PayloadJson column mismatch
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'PayloadJson') THEN
        ALTER TABLE ""OutboxMessages"" ADD COLUMN ""PayloadJson"" text NOT NULL DEFAULT '{}';
        
        -- Migrate data if the old 'Payload' column exists
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'Payload') THEN
            UPDATE ""OutboxMessages"" SET ""PayloadJson"" = ""Payload""::text;
            -- Drop the old column to avoid NotNull violations during EF inserts
            ALTER TABLE ""OutboxMessages"" DROP COLUMN ""Payload"";
        END IF;
    ELSIF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'Payload') THEN
        -- PayloadJson exists, but so does Payload. Cleanup.
        ALTER TABLE ""OutboxMessages"" DROP COLUMN ""Payload"";
    END IF;

    -- 2. Ensure Status is integer (Enum) not varchar
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'Status' AND data_type = 'character varying') THEN
        ALTER TABLE ""OutboxMessages"" ALTER COLUMN ""Status"" TYPE integer USING (
            CASE ""Status""
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

ALTER TABLE ""OutboxMessages"" ALTER COLUMN ""PayloadJson"" DROP DEFAULT;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse is complex and potentially destructive, so we keep it minimal
            migrationBuilder.Sql(@"
DO $$ 
BEGIN 
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'PayloadJson') THEN
        -- We don't drop PayloadJson to avoid data loss, but we could rename if absolutely needed
        NULL;
    END IF;
END $$;
");
        }
    }
}
