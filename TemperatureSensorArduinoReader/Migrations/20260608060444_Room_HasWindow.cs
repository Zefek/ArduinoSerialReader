using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TemperatureSensorArduinoReader.Migrations
{
    /// <inheritdoc />
    public partial class Room_HasWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasWindow",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasWindow",
                table: "Rooms");
        }
    }
}
