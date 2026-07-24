using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkSizeOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveChunkOverlap",
                schema: "dbo",
                table: "SystemConfiguration",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActiveChunkSize",
                schema: "dbo",
                table: "SystemConfiguration",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveChunkOverlap",
                schema: "dbo",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "ActiveChunkSize",
                schema: "dbo",
                table: "SystemConfiguration");
        }
    }
}
