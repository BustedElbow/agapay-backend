using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace agapay_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddConditionCategoryAndTherapistOthers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OtherConditionsTreated",
                table: "PhysicalTherapists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "ConditionsTreated",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtherConditionsTreated",
                table: "PhysicalTherapists");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ConditionsTreated");
        }
    }
}
