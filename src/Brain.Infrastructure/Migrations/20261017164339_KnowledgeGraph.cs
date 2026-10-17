using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "graph_references",
                table: "knowledge_documents",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<int>(
                name: "graph_version",
                table: "knowledge_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_graph_references",
                table: "knowledge_documents",
                column: "graph_references")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_knowledge_documents_graph_references",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "graph_references",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "graph_version",
                table: "knowledge_documents");
        }
    }
}
