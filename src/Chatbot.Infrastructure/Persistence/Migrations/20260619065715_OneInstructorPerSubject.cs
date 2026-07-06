using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OneInstructorPerSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UQ_SubjectInstructor_SubjectId",
                schema: "auth",
                table: "SubjectInstructor",
                column: "SubjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_SubjectInstructor_SubjectId",
                schema: "auth",
                table: "SubjectInstructor");
        }
    }
}
