using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TemperatureSensorArduinoReader.Migrations
{
    /// <inheritdoc />
    public partial class AbsoluteHumidityEma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HumidityEma",
                table: "SensorStates",
                newName: "AbsoluteHumidityEma");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AbsoluteHumidityEma",
                table: "SensorStates",
                newName: "HumidityEma");
        }
    }
}
