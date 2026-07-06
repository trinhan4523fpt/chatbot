using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExperimentRunRetrievalMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AvgTokens",
                schema: "rbl",
                table: "ExperimentRunMetric",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChunkCount",
                schema: "rbl",
                table: "ExperimentRunMetric",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChunkingTimeMs",
                schema: "rbl",
                table: "ExperimentRunMetric",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Mrr",
                schema: "rbl",
                table: "ExperimentRunMetric",
                type: "decimal(7,6)",
                precision: 7,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Ndcg",
                schema: "rbl",
                table: "ExperimentRunMetric",
                type: "decimal(7,6)",
                precision: 7,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecisionAtK",
                schema: "rbl",
                table: "ExperimentRunMetric",
                type: "decimal(7,6)",
                precision: 7,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RecallAtK",
                schema: "rbl",
                table: "ExperimentRunMetric",
                type: "decimal(7,6)",
                precision: 7,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvgTokens",
                schema: "rbl",
                table: "ExperimentRunMetric");

            migrationBuilder.DropColumn(
                name: "ChunkCount",
                schema: "rbl",
                table: "ExperimentRunMetric");

            migrationBuilder.DropColumn(
                name: "ChunkingTimeMs",
                schema: "rbl",
                table: "ExperimentRunMetric");

            migrationBuilder.DropColumn(
                name: "Mrr",
                schema: "rbl",
                table: "ExperimentRunMetric");

            migrationBuilder.DropColumn(
                name: "Ndcg",
                schema: "rbl",
                table: "ExperimentRunMetric");

            migrationBuilder.DropColumn(
                name: "PrecisionAtK",
                schema: "rbl",
                table: "ExperimentRunMetric");

            migrationBuilder.DropColumn(
                name: "RecallAtK",
                schema: "rbl",
                table: "ExperimentRunMetric");
        }
    }
}
