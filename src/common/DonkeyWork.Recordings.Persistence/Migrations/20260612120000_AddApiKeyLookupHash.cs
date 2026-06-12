using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonkeyWork.Recordings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyLookupHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "key_hash",
                schema: "recordings",
                table: "user_api_keys",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_api_keys_key_hash",
                schema: "recordings",
                table: "user_api_keys",
                column: "key_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_api_keys_key_hash",
                schema: "recordings",
                table: "user_api_keys");

            migrationBuilder.DropColumn(
                name: "key_hash",
                schema: "recordings",
                table: "user_api_keys");
        }
    }
}
