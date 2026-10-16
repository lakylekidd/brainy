using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Brain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revision_token = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    embedding_provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    embedding_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    embedding_dimensions = table.Column<int>(type: "integer", nullable: false),
                    chunker_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    heading = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "character varying(1800)", maxLength: 1800, nullable: false),
                    start_line = table.Column<int>(type: "integer", nullable: false),
                    end_line = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(384)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunks", x => x.id);
                    table.CheckConstraint("ck_knowledge_chunk_lines", "start_line > 0 AND end_line >= start_line");
                    table.ForeignKey(
                        name: "FK_knowledge_chunks_knowledge_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "knowledge_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_document_id_ordinal",
                table: "knowledge_chunks",
                columns: new[] { "document_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_path",
                table: "knowledge_documents",
                column: "path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_chunks");

            migrationBuilder.DropTable(
                name: "knowledge_documents");
        }
    }
}
