using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DonkeyWork.Recordings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status_detail",
                schema: "recordings",
                table: "recordings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status_detail",
                schema: "recordings",
                table: "recordings");
        }
    }
}
