using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WorkforceMapper.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingMinorChangesActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Activities",
                columns: new[] { "Id", "ActivityType", "ApplicationId", "Division", "Name", "ProjectId" },
                values: new object[,]
                {
                    { 34, "Minor Changes", 2, "Platform Engineering", "APIGateway - Minor Changes", null },
                    { 35, "Minor Changes", 3, "Platform Engineering", "SurveyEngine - Minor Changes", null },
                    { 36, "Minor Changes", 4, "Platform Engineering", "AuthService - Minor Changes", null },
                    { 37, "Minor Changes", 5, "Platform Engineering", "ObservabilityStack - Minor Changes", null },
                    { 38, "Minor Changes", 7, "Analytics", "DataMapper - Minor Changes", null },
                    { 39, "Minor Changes", 8, "Analytics", "QuotaManager - Minor Changes", null },
                    { 40, "Minor Changes", 9, "Analytics", "SegmentBuilder - Minor Changes", null },
                    { 41, "Minor Changes", 18, "Analytics", "PanelIQ - Minor Changes", null },
                    { 42, "Minor Changes", 10, "Data Collection", "FieldOps - Minor Changes", null },
                    { 43, "Minor Changes", 11, "Data Collection", "WebCollect - Minor Changes", null },
                    { 44, "Minor Changes", 12, "Data Collection", "SampleRouter - Minor Changes", null },
                    { 45, "Minor Changes", 13, "Data Collection", "CallCentre Suite - Minor Changes", null },
                    { 46, "Minor Changes", 14, "Data Collection", "DiaryTracker - Minor Changes", null },
                    { 47, "Minor Changes", 15, "Client Services", "ClientPortal - Minor Changes", null },
                    { 48, "Minor Changes", 17, "Client Services", "DeliveryTracker - Minor Changes", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 48);
        }
    }
}
