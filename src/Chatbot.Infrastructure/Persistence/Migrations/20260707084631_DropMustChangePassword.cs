using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropMustChangePassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "auth",
                table: "User");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "auth",
                table: "User",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
