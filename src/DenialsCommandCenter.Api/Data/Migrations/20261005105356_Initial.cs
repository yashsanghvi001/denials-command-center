using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DenialsCommandCenter.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    ClaimId = table.Column<string>(type: "text", nullable: false),
                    PatientFirst = table.Column<string>(type: "text", nullable: false),
                    PatientLast = table.Column<string>(type: "text", nullable: false),
                    PatientDob = table.Column<DateOnly>(type: "date", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: false),
                    Payer = table.Column<string>(type: "text", nullable: false),
                    PayerId = table.Column<string>(type: "text", nullable: false),
                    DateOfService = table.Column<DateOnly>(type: "date", nullable: false),
                    SubmittedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RenderingNpi = table.Column<string>(type: "text", nullable: false),
                    RenderingProvider = table.Column<string>(type: "text", nullable: false),
                    Facility = table.Column<string>(type: "text", nullable: false),
                    PlaceOfService = table.Column<string>(type: "text", nullable: false),
                    CoderId = table.Column<string>(type: "text", nullable: false),
                    PrebillReviewed = table.Column<bool>(type: "boolean", nullable: false),
                    TotalCharge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    LinesJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.ClaimId);
                });

            migrationBuilder.CreateTable(
                name: "ClaimStates",
                columns: table => new
                {
                    ClaimId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    BilledCharge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    NetPaid = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DeniedAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DenialDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastRemitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DenialLinesJson = table.Column<string>(type: "jsonb", nullable: false),
                    WasRecouped = table.Column<bool>(type: "boolean", nullable: false),
                    PossibleOverpayment = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EventCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimStates", x => x.ClaimId);
                });

            migrationBuilder.CreateTable(
                name: "IngestionIssues",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ClaimId = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionIssues", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "IngestionRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InputHash = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Events = table.Column<int>(type: "integer", nullable: false),
                    Issues = table.Column<int>(type: "integer", nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ReportJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemitEvents",
                columns: table => new
                {
                    EventKey = table.Column<string>(type: "text", nullable: false),
                    ClaimId = table.Column<string>(type: "text", nullable: true),
                    RawClaimRef = table.Column<string>(type: "text", nullable: false),
                    PayerId = table.Column<string>(type: "text", nullable: false),
                    PayerName = table.Column<string>(type: "text", nullable: false),
                    TraceNumber = table.Column<string>(type: "text", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceFile = table.Column<string>(type: "text", nullable: false),
                    FileOrder = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StatusCode = table.Column<string>(type: "text", nullable: false),
                    Charge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Paid = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PaymentJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemitEvents", x => x.EventKey);
                });

            migrationBuilder.CreateTable(
                name: "SourceFiles",
                columns: table => new
                {
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FileOrder = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    InterchangeControlNumber = table.Column<string>(type: "text", nullable: true),
                    InterchangeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaimPayments = table.Column<int>(type: "integer", nullable: false),
                    NewEvents = table.Column<int>(type: "integer", nullable: false),
                    DuplicateEvents = table.Column<int>(type: "integer", nullable: false),
                    PaymentTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    NewPaymentTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceFiles", x => x.FileName);
                });

            migrationBuilder.CreateTable(
                name: "WorklogEntries",
                columns: table => new
                {
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    ClaimId = table.Column<string>(type: "text", nullable: true),
                    LoggedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LoggedDateCandidates = table.Column<string>(type: "text", nullable: false),
                    Patient = table.Column<string>(type: "text", nullable: false),
                    Payer = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StatusRaw = table.Column<string>(type: "text", nullable: false),
                    SuspiciousText = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorklogEntries", x => x.RowNumber);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngestionIssues_ClaimId",
                table: "IngestionIssues",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_RemitEvents_ClaimId",
                table: "RemitEvents",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_WorklogEntries_ClaimId",
                table: "WorklogEntries",
                column: "ClaimId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "ClaimStates");

            migrationBuilder.DropTable(
                name: "IngestionIssues");

            migrationBuilder.DropTable(
                name: "IngestionRuns");

            migrationBuilder.DropTable(
                name: "RemitEvents");

            migrationBuilder.DropTable(
                name: "SourceFiles");

            migrationBuilder.DropTable(
                name: "WorklogEntries");
        }
    }
}
