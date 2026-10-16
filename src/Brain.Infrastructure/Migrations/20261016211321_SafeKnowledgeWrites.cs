using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SafeKnowledgeWrites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_write_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<string>(type: "character varying(100)", nullable: false),
                    path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    revision_token = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    operation = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    section = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewer_id = table.Column<string>(type: "text", nullable: true),
                    review_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    git_branch = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    parent_commit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    git_commit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    git_blob = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    base_content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    result_content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    result_content = table.Column<string>(type: "text", nullable: true),
                    result_has_utf8_bom = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_write_proposals", x => x.id);
                    table.CheckConstraint("ck_proposal_operation", "operation IN ('append','replace_section','create_document')");
                    table.CheckConstraint("ck_proposal_status", "status IN ('pending','validating','approved','rejected','conflict','applied')");
                    table.CheckConstraint("ck_proposal_version", "version >= 0");
                    table.ForeignKey(
                        name: "FK_knowledge_write_proposals_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_write_proposals_agent_id_created_at",
                table: "knowledge_write_proposals",
                columns: new[] { "agent_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_write_proposals_status_created_at",
                table: "knowledge_write_proposals",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_write_proposals");
        }
    }
}
