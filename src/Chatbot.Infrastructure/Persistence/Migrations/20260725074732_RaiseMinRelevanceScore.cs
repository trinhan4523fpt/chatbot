using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RaiseMinRelevanceScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Raise the retrieval threshold on an existing database. Only touch the row still on the
            // old 0.30 default, so an operator who tuned it themselves is left alone.
            migrationBuilder.Sql(
                "UPDATE dbo.SystemConfiguration SET MinRelevanceScore = 0.83 " +
                "WHERE Id = 1 AND MinRelevanceScore = 0.30;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.SystemConfiguration SET MinRelevanceScore = 0.30 " +
                "WHERE Id = 1 AND MinRelevanceScore = 0.83;");
        }
    }
}
