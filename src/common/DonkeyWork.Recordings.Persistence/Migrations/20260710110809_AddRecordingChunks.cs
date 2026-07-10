using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonkeyWork.Recordings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recording_chunks",
                schema: "recordings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    recording_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    file_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recording_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_recording_chunks_recordings_recording_id",
                        column: x => x.recording_id,
                        principalSchema: "recordings",
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recording_chunks_recording_id_chunk_index",
                schema: "recordings",
                table: "recording_chunks",
                columns: new[] { "recording_id", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recording_chunks_user_id",
                schema: "recordings",
                table: "recording_chunks",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recording_chunks",
                schema: "recordings");
        }
    }
}
