using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Brain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    team_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    resource = table.Column<string>(type: "text", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    result = table.Column<string>(type: "text", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "memories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<string>(type: "character varying(100)", nullable: false),
                    memory_type = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    importance = table.Column<double>(type: "double precision", nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    visibility = table.Column<string>(type: "text", nullable: false),
                    team_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                    reference_count = table.Column<long>(type: "bigint", nullable: false),
                    last_accessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    promotion_status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memories", x => x.id);
                    table.CheckConstraint("ck_memory_scores", "importance BETWEEN 0 AND 1 AND confidence BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_memory_type", "memory_type IN ('observation','fact','decision','task_context','incident','preference','discovery')");
                    table.CheckConstraint("ck_memory_visibility", "visibility IN ('private','team','organization') AND (visibility <> 'team' OR team_id IS NOT NULL)");
                    table.CheckConstraint("ck_promotion_status", "promotion_status IN ('none','candidate','approved','rejected','promoted')");
                    table.ForeignKey(
                        name: "FK_memories_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_permissions",
                columns: table => new
                {
                    agent_id = table.Column<string>(type: "character varying(100)", nullable: false),
                    permission_id = table.Column<string>(type: "character varying(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_permissions", x => new { x.agent_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_agent_permissions_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_roles",
                columns: table => new
                {
                    agent_id = table.Column<string>(type: "character varying(100)", nullable: false),
                    role_id = table.Column<string>(type: "character varying(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_roles", x => new { x.agent_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_agent_roles_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    permission_id = table.Column<string>(type: "character varying(100)", nullable: false),
                    role_id = table.Column<string>(type: "character varying(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.permission_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memory_embeddings",
                columns: table => new
                {
                    memory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(384)", nullable: false),
                    provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    dimensions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_embeddings", x => x.memory_id);
                    table.CheckConstraint("ck_embedding_dimensions", "dimensions = 384");
                    table.ForeignKey(
                        name: "FK_memory_embeddings_memories_memory_id",
                        column: x => x.memory_id,
                        principalTable: "memories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_permissions_permission_id",
                table: "agent_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_roles_role_id",
                table: "agent_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_agent_id_timestamp",
                table: "audit_events",
                columns: new[] { "agent_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_memories_agent_id",
                table: "memories",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_memories_expires_at",
                table: "memories",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_memories_visibility_team_id",
                table: "memories",
                columns: new[] { "visibility", "team_id" });

            migrationBuilder.CreateIndex(
                name: "IX_memory_embeddings_provider_model",
                table: "memory_embeddings",
                columns: new[] { "provider", "model" });

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_role_id",
                table: "role_permissions",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_permissions");

            migrationBuilder.DropTable(
                name: "agent_roles");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "memory_embeddings");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "memories");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "agents");
        }
    }
}
