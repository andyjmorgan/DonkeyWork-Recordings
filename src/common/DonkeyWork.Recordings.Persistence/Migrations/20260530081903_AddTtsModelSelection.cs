using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonkeyWork.Recordings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTtsModelSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tts_model",
                schema: "recordings",
                table: "recordings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "chatterbox");

            migrationBuilder.AddColumn<string>(
                name: "default_tts_model",
                schema: "recordings",
                table: "collections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tts_model",
                schema: "recordings",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "default_tts_model",
                schema: "recordings",
                table: "collections");
        }
    }
}
