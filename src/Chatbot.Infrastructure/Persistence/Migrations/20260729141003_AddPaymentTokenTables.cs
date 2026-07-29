using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTokenTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentTokenWallet",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    AvailableTokens = table.Column<int>(type: "int", nullable: false),
                    UsedTokens = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentTokenWallet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentTokenWallet_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TokenPackage",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TokenAmount = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    ValidityDays = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenPackage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenTransaction",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    ChatMessageId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenTransaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokenTransaction_StudentTokenWallet_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "dbo",
                        principalTable: "StudentTokenWallet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentTokenOrder",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    PackageId = table.Column<long>(type: "bigint", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    TokenAmount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrderRef = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    VnpayTransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VnpayResponseCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VnpayBankCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VnpayCardType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    VnpayRawResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    WalletId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentTokenOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentTokenOrder_StudentTokenWallet_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "dbo",
                        principalTable: "StudentTokenWallet",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentTokenOrder_TokenPackage_PackageId",
                        column: x => x.PackageId,
                        principalSchema: "dbo",
                        principalTable: "TokenPackage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentTokenOrder_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentTokenOrder_PackageId",
                schema: "dbo",
                table: "StudentTokenOrder",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTokenOrder_PaidAtUtc",
                schema: "dbo",
                table: "StudentTokenOrder",
                column: "PaidAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTokenOrder_Status",
                schema: "dbo",
                table: "StudentTokenOrder",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTokenOrder_UserId",
                schema: "dbo",
                table: "StudentTokenOrder",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTokenOrder_WalletId",
                schema: "dbo",
                table: "StudentTokenOrder",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "UQ_StudentTokenOrder_OrderRef",
                schema: "dbo",
                table: "StudentTokenOrder",
                column: "OrderRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_StudentTokenWallet_UserId",
                schema: "dbo",
                table: "StudentTokenWallet",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransaction_CreatedAtUtc",
                schema: "dbo",
                table: "TokenTransaction",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransaction_Type",
                schema: "dbo",
                table: "TokenTransaction",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransaction_UserId",
                schema: "dbo",
                table: "TokenTransaction",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransaction_WalletId",
                schema: "dbo",
                table: "TokenTransaction",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentTokenOrder",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TokenTransaction",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TokenPackage",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "StudentTokenWallet",
                schema: "dbo");
        }
    }
}
