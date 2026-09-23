using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tugu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_DeviceId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_WalletId",
                table: "transactions");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_DeviceId_CreatedAt",
                table: "transactions",
                columns: new[] { "DeviceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_WalletId_CreatedAt",
                table: "transactions",
                columns: new[] { "WalletId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_DeviceId_CreatedAt",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_WalletId_CreatedAt",
                table: "transactions");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_DeviceId",
                table: "transactions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_WalletId",
                table: "transactions",
                column: "WalletId");
        }
    }
}
