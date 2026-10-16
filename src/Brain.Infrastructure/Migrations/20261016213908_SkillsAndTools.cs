using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Brain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SkillsAndTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registry_entries",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(106)", maxLength: 106, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(171)", maxLength: 171, nullable: false),
                    kind = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    required_permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    required_tools = table.Column<string[]>(type: "text[]", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    capabilities = table.Column<string[]>(type: "text[]", nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    embedding = table.Column<Vector>(type: "vector(384)", nullable: false),
                    embedding_provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    embedding_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    embedding_dimensions = table.Column<int>(type: "integer", nullable: false),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registry_entries", x => x.key);
                    table.CheckConstraint("ck_registry_dimensions", "embedding_dimensions = 384");
                    table.CheckConstraint("ck_registry_kind", "kind IN ('skill','tool')");
                    table.CheckConstraint("ck_registry_provider", "(kind = 'skill' AND provider IS NULL) OR (kind = 'tool' AND provider IS NOT NULL AND provider IN ('mcp','http'))");
                });

            migrationBuilder.CreateIndex(
                name: "IX_registry_entries_fingerprint",
                table: "registry_entries",
                column: "fingerprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registry_entries");
        }
    }
}
