using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace agapay_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTherapistAvailabilityAndPatientPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    PreferredDayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    PreferredStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    PreferredEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    PreferredSessionDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    SpecialRequirements = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientPreferences_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TherapistAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhysicalTherapistId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    SpecificDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TherapistAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TherapistAvailabilities_PhysicalTherapists_PhysicalTherapis~",
                        column: x => x.PhysicalTherapistId,
                        principalTable: "PhysicalTherapists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPreferences_PatientId",
                table: "PatientPreferences",
                column: "PatientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TherapistAvailabilities_PhysicalTherapistId",
                table: "TherapistAvailabilities",
                column: "PhysicalTherapistId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientPreferences");

            migrationBuilder.DropTable(
                name: "TherapistAvailabilities");
        }
    }
}
