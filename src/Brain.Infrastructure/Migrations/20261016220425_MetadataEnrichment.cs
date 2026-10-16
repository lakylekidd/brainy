using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MetadataEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_proposal_operation",
                table: "knowledge_write_proposals");

            migrationBuilder.AddColumn<double>(
                name: "enrichment_confidence",
                table: "knowledge_write_proposals",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "enrichment_model",
                table: "knowledge_write_proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "enrichment_provider",
                table: "knowledge_write_proposals",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "generated_by",
                table: "knowledge_write_proposals",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "metadata_schema_version",
                table: "knowledge_write_proposals",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "metadata_scan_states",
                columns: table => new
                {
                    path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revision_token = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    policy_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_enriched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issues_json = table.Column<string>(type: "jsonb", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metadata_scan_states", x => x.path);
                    table.ForeignKey(
                        name: "FK_metadata_scan_states_knowledge_write_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "knowledge_write_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_proposal_confidence",
                table: "knowledge_write_proposals",
                sql: "enrichment_confidence IS NULL OR enrichment_confidence BETWEEN 0 AND 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_proposal_operation",
                table: "knowledge_write_proposals",
                sql: "operation IN ('append','replace_section','create_document','metadata_enrichment')");

            migrationBuilder.CreateIndex(
                name: "IX_metadata_scan_states_proposal_id",
                table: "metadata_scan_states",
                column: "proposal_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metadata_scan_states");

            migrationBuilder.DropCheckConstraint(
                name: "ck_proposal_confidence",
                table: "knowledge_write_proposals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_proposal_operation",
                table: "knowledge_write_proposals");

            migrationBuilder.DropColumn(
                name: "enrichment_confidence",
                table: "knowledge_write_proposals");

            migrationBuilder.DropColumn(
                name: "enrichment_model",
                table: "knowledge_write_proposals");

            migrationBuilder.DropColumn(
                name: "enrichment_provider",
                table: "knowledge_write_proposals");

            migrationBuilder.DropColumn(
                name: "generated_by",
                table: "knowledge_write_proposals");

            migrationBuilder.DropColumn(
                name: "metadata_schema_version",
                table: "knowledge_write_proposals");

            migrationBuilder.AddCheckConstraint(
                name: "ck_proposal_operation",
                table: "knowledge_write_proposals",
                sql: "operation IN ('append','replace_section','create_document')");
        }
    }
}
