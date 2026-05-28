using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DonkeyWork.Recordings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "recordings");

            migrationBuilder.CreateTable(
                name: "collections",
                schema: "recordings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    cover_image_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    default_voice = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    default_language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tone = table.Column<string>(type: "text", nullable: true),
                    author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    author_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    itunes_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_explicit = table.Column<bool>(type: "boolean", nullable: false),
                    link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_feed_settings",
                schema: "recordings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    author_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cover_image_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_explicit = table.Column<bool>(type: "boolean", nullable: false),
                    itunes_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_feed_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recordings",
                schema: "recordings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    transcript = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: false),
                    voice = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sequence_number = table.Column<int>(type: "integer", nullable: true),
                    chapter_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    progress = table.Column<double>(type: "double precision", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recordings", x => x.id);
                    table.ForeignKey(
                        name: "FK_recordings_collections_collection_id",
                        column: x => x.collection_id,
                        principalSchema: "recordings",
                        principalTable: "collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "playback",
                schema: "recordings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    recording_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_seconds = table.Column<double>(type: "double precision", nullable: false),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: false),
                    completed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    playback_speed = table.Column<double>(type: "double precision", nullable: false, defaultValue: 1.0),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playback", x => x.id);
                    table.ForeignKey(
                        name: "FK_playback_recordings_recording_id",
                        column: x => x.recording_id,
                        principalSchema: "recordings",
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collections_user_id",
                schema: "recordings",
                table: "collections",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_playback_recording_id",
                schema: "recordings",
                table: "playback",
                column: "recording_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_playback_user_id_recording_id",
                schema: "recordings",
                table: "playback",
                columns: new[] { "user_id", "recording_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recordings_collection_id_sequence_number",
                schema: "recordings",
                table: "recordings",
                columns: new[] { "collection_id", "sequence_number" });

            migrationBuilder.CreateIndex(
                name: "IX_recordings_user_id",
                schema: "recordings",
                table: "recordings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_feed_settings_user_id",
                schema: "recordings",
                table: "user_feed_settings",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "playback",
                schema: "recordings");

            migrationBuilder.DropTable(
                name: "user_feed_settings",
                schema: "recordings");

            migrationBuilder.DropTable(
                name: "recordings",
                schema: "recordings");

            migrationBuilder.DropTable(
                name: "collections",
                schema: "recordings");
        }
    }
}
