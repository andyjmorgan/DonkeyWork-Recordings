using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonkeyWork.Recordings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropModelTranscriptAndTone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "processed_transcript",
                schema: "recordings",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "tts_model",
                schema: "recordings",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "default_tts_model",
                schema: "recordings",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "tone",
                schema: "recordings",
                table: "collections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "processed_transcript",
                schema: "recordings",
                table: "recordings",
                type: "text",
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddColumn<string>(
                name: "tone",
                schema: "recordings",
                table: "collections",
                type: "text",
                nullable: true);
        }
    }
}
