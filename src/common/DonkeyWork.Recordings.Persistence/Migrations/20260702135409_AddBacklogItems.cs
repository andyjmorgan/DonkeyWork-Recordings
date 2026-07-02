using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonkeyWork.Recordings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBacklogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backlog_items",
                schema: "recordings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consumed_by_recording_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backlog_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_backlog_items_collections_collection_id",
                        column: x => x.collection_id,
                        principalSchema: "recordings",
                        principalTable: "collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_backlog_items_recordings_consumed_by_recording_id",
                        column: x => x.consumed_by_recording_id,
                        principalSchema: "recordings",
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backlog_items_collection_id_status",
                schema: "recordings",
                table: "backlog_items",
                columns: new[] { "collection_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_backlog_items_consumed_by_recording_id",
                schema: "recordings",
                table: "backlog_items",
                column: "consumed_by_recording_id");

            migrationBuilder.CreateIndex(
                name: "IX_backlog_items_user_id",
                schema: "recordings",
                table: "backlog_items",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backlog_items",
                schema: "recordings");
        }
    }
}
