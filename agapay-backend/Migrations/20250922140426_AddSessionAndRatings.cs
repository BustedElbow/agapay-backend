using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace agapay_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionAndRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageRating",
                table: "PhysicalTherapists",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeePerSession",
                table: "PhysicalTherapists",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RatingCount",
                table: "PhysicalTherapists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TherapySessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    PhysicalTherapistId = table.Column<int>(type: "integer", nullable: false),
                    LocationAddress = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    DoctorReferralImageUrl = table.Column<string>(type: "text", nullable: true),
                    TotalFee = table.Column<decimal>(type: "numeric", nullable: false),
                    PatientFee = table.Column<decimal>(type: "numeric", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TherapySessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TherapySessions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TherapySessions_PhysicalTherapists_PhysicalTherapistId",
                        column: x => x.PhysicalTherapistId,
                        principalTable: "PhysicalTherapists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TherapistRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PhysicalTherapistId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    SessionId = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<byte>(type: "smallint", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TherapistRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TherapistRatings_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TherapistRatings_PhysicalTherapists_PhysicalTherapistId",
                        column: x => x.PhysicalTherapistId,
                        principalTable: "PhysicalTherapists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TherapistRatings_TherapySessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TherapySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TherapistRatings_PatientId",
                table: "TherapistRatings",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_TherapistRatings_PhysicalTherapistId",
                table: "TherapistRatings",
                column: "PhysicalTherapistId");

            migrationBuilder.CreateIndex(
                name: "IX_TherapistRatings_SessionId",
                table: "TherapistRatings",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TherapySessions_PatientId",
                table: "TherapySessions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_TherapySessions_PhysicalTherapistId",
                table: "TherapySessions",
                column: "PhysicalTherapistId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TherapistRatings");

            migrationBuilder.DropTable(
                name: "TherapySessions");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "PhysicalTherapists");

            migrationBuilder.DropColumn(
                name: "FeePerSession",
                table: "PhysicalTherapists");

            migrationBuilder.DropColumn(
                name: "RatingCount",
                table: "PhysicalTherapists");
        }
    }
}
