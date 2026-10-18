using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PS.Wallet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerEntryPaginationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_WalletId_CreatedAtUtc",
                table: "LedgerEntries");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_WalletId_CreatedAtUtc_Id",
                table: "LedgerEntries",
                columns: new[] { "WalletId", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_WalletId_CreatedAtUtc_Id",
                table: "LedgerEntries");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_WalletId_CreatedAtUtc",
                table: "LedgerEntries",
                columns: new[] { "WalletId", "CreatedAtUtc" });
        }
    }
}
