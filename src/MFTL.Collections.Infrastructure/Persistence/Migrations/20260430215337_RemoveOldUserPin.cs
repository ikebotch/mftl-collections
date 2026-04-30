using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOldUserPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$ 
BEGIN 
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'Pin') THEN
        ALTER TABLE ""Users"" DROP COLUMN ""Pin"";
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$ 
BEGIN 
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'Pin') THEN
        ALTER TABLE ""Users"" ADD COLUMN ""Pin"" text;
    END IF;
END $$;
");
        }
    }
}
