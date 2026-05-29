using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonkeyWork.Recordings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyLastUsedAndScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_used_at",
                schema: "recordings",
                table: "user_api_keys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "scope",
                schema: "recordings",
                table: "user_api_keys",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_used_at",
                schema: "recordings",
                table: "user_api_keys");

            migrationBuilder.DropColumn(
                name: "scope",
                schema: "recordings",
                table: "user_api_keys");
        }
    }
}
