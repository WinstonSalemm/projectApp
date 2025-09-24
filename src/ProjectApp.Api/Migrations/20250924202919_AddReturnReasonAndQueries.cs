using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnReasonAndQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Returns",
                type: "TEXT",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Returns");
        }
    }
}
