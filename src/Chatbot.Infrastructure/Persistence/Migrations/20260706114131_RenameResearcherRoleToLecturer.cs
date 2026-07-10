using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameResearcherRoleToLecturer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename the seeded role in place so user assignments and role-permission
            // mappings (both keyed by RoleId) carry over. Guarded in case a Lecturer
            // role already exists or Researcher was never seeded.
            migrationBuilder.Sql(
                """
                UPDATE auth.Role
                SET Name = 'Lecturer', NormalizedName = 'LECTURER'
                WHERE NormalizedName = 'RESEARCHER'
                  AND NOT EXISTS (SELECT 1 FROM auth.Role WHERE NormalizedName = 'LECTURER');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE auth.Role
                SET Name = 'Researcher', NormalizedName = 'RESEARCHER'
                WHERE NormalizedName = 'LECTURER'
                  AND NOT EXISTS (SELECT 1 FROM auth.Role WHERE NormalizedName = 'RESEARCHER');
                """);
        }
    }
}
