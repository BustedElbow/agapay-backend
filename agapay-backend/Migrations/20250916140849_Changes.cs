using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace agapay_backend.Migrations
{
    /// <inheritdoc />
    public partial class Changes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LicenseImageUrl",
                table: "PhysicalTherapists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "PhysicalTherapists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "PhysicalTherapists",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "PhysicalTherapists",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LicenseImageUrl",
                table: "PhysicalTherapists");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "PhysicalTherapists");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "PhysicalTherapists");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "PhysicalTherapists");
        }
    }
}
