using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmSamplingConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxOutputTokens",
                schema: "dbo",
                table: "SystemConfiguration",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Temperature",
                schema: "dbo",
                table: "SystemConfiguration",
                type: "decimal(7,6)",
                precision: 7,
                scale: 6,
                nullable: false,
                // Match the entity default so existing rows keep the prior hardcoded 0.2, not 0.
                defaultValue: 0.2m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxOutputTokens",
                schema: "dbo",
                table: "SystemConfiguration");

            migrationBuilder.DropColumn(
                name: "Temperature",
                schema: "dbo",
                table: "SystemConfiguration");
        }
    }
}
