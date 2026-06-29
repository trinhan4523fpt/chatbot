using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectInstructor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubjectInstructor",
                schema: "auth",
                columns: table => new
                {
                    SubjectId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AssignedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectInstructor", x => new { x.SubjectId, x.UserId });
                    table.ForeignKey(
                        name: "FK_SubjectInstructor_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "dbo",
                        principalTable: "Subject",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectInstructor_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubjectInstructor_UserId",
                schema: "auth",
                table: "SubjectInstructor",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubjectInstructor",
                schema: "auth");
        }
    }
}
