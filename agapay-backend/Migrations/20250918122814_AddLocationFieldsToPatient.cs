using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace agapay_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationFieldsToPatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Patients",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationDisplayName",
                table: "Patients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Patients",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LocationDisplayName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Patients");
        }
    }
}
