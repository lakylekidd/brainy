using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BrainClientOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hostname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    operating_system = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    architecture = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    client_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    runtime_agent_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    registered_by = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    agent_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    credential_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                    table.ForeignKey(
                        name: "FK_devices_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_runtimes",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    operating_system = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    architecture = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    docker_available = table.Column<bool>(type: "boolean", nullable: false),
                    commands_json = table.Column<string>(type: "jsonb", nullable: false),
                    healthy = table.Column<bool>(type: "boolean", nullable: false),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_runtimes", x => new { x.device_id, x.environment_id });
                    table.ForeignKey(
                        name: "FK_device_runtimes_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_skills",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    harness = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    skill_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_skills", x => new { x.device_id, x.harness, x.skill_id });
                    table.ForeignKey(
                        name: "FK_device_skills_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provisioning_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    package_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    package_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    wheel_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operating_system = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    architecture = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    approved_by = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provisioning_plans", x => x.id);
                    table.ForeignKey(
                        name: "FK_provisioning_plans_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_devices_agent_id",
                table: "devices",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_devices_credential_hash",
                table: "devices",
                column: "credential_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_name",
                table: "devices",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provisioning_plans_device_id_status",
                table: "provisioning_plans",
                columns: new[] { "device_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_runtimes");

            migrationBuilder.DropTable(
                name: "device_skills");

            migrationBuilder.DropTable(
                name: "provisioning_plans");

            migrationBuilder.DropTable(
                name: "devices");
        }
    }
}
