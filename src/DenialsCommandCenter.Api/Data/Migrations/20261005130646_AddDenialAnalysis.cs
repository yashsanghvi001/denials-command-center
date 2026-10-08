using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DenialsCommandCenter.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDenialAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppealDrafts",
                columns: table => new
                {
                    FactsHash = table.Column<string>(type: "text", nullable: false),
                    ClaimId = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StatusReason = table.Column<string>(type: "text", nullable: true),
                    LetterTemplate = table.Column<string>(type: "text", nullable: true),
                    CitationsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AiRootCause = table.Column<string>(type: "text", nullable: true),
                    AiOwningTeam = table.Column<string>(type: "text", nullable: true),
                    AiPreventable = table.Column<string>(type: "text", nullable: true),
                    AiConfidence = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealDrafts", x => x.FactsHash);
                });

            migrationBuilder.CreateTable(
                name: "DenialAnalyses",
                columns: table => new
                {
                    ClaimId = table.Column<string>(type: "text", nullable: false),
                    PayerId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DeniedAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DenialDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReasonCodes = table.Column<string>(type: "text", nullable: false),
                    RootCause = table.Column<string>(type: "text", nullable: false),
                    OwningTeam = table.Column<string>(type: "text", nullable: false),
                    Preventable = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    NextAction = table.Column<string>(type: "text", nullable: false),
                    CitationsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Confidence = table.Column<string>(type: "text", nullable: false),
                    ConfidenceReason = table.Column<string>(type: "text", nullable: true),
                    Bucket = table.Column<string>(type: "text", nullable: false),
                    Deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    DaysToDeadline = table.Column<int>(type: "integer", nullable: true),
                    ExpectedValue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PriorityScore = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DenialAnalyses", x => x.ClaimId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppealDrafts_ClaimId",
                table: "AppealDrafts",
                column: "ClaimId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealDrafts");

            migrationBuilder.DropTable(
                name: "DenialAnalyses");
        }
    }
}
