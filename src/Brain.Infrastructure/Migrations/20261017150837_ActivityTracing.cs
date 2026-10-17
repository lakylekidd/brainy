using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ActivityTracing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_traces",
                columns: table => new
                {
                    trace_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    agent_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    root_span_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    root_action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    has_failures = table.Column<bool>(type: "boolean", nullable: false),
                    has_cancellations = table.Column<bool>(type: "boolean", nullable: false),
                    active_roots = table.Column<int>(type: "integer", nullable: false),
                    duration_ms = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_traces", x => new { x.trace_id, x.agent_id });
                });

            migrationBuilder.CreateTable(
                name: "activity_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trace_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    agent_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    span_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    parent_span_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    source_entity_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    target_entity_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_ms = table.Column<double>(type: "double precision", nullable: true),
                    is_root = table.Column<bool>(type: "boolean", nullable: false),
                    error_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    error_message = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_events_activity_traces_trace_id_agent_id",
                        columns: x => new { x.trace_id, x.agent_id },
                        principalTable: "activity_traces",
                        principalColumns: new[] { "trace_id", "agent_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_source_entity_id",
                table: "activity_events",
                column: "source_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_target_entity_id",
                table: "activity_events",
                column: "target_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_trace_id_agent_id_timestamp",
                table: "activity_events",
                columns: new[] { "trace_id", "agent_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_traces_agent_id_started_at",
                table: "activity_traces",
                columns: new[] { "agent_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_traces_completed_at",
                table: "activity_traces",
                column: "completed_at");

            migrationBuilder.CreateIndex(
                name: "IX_activity_traces_started_at",
                table: "activity_traces",
                column: "started_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_events");

            migrationBuilder.DropTable(
                name: "activity_traces");
        }
    }
}
