using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkforceMapper.Server.Migrations
{
    /// <inheritdoc />
    public partial class RenameActivitiesAndProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "CoreDataWarehouse - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Minor Changes", "CoreDataWarehouse - Minor Changes" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "APIGateway - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "SurveyEngine - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "AuthService - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "ObservabilityStack - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P01 CoreDataWarehouse Cloud Migration - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P02 AuthService Replacement - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P03 Security & Compliance Programme - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P04 DevPortal Rebuild - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "InsightHub - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Minor Changes", "InsightHub - Minor Changes" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "DataMapper - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "QuotaManager - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "SegmentBuilder - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "PanelIQ - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P05 ML Forecasting Platform - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P06 Real-Time Analytics Initiative - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P07 SegmentBuilder Enhancement - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "FieldOps - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "WebCollect - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "SampleRouter - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "CallCentre Suite - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 24,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "DiaryTracker - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 25,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P08 CallCentre Suite Replacement - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 26,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P09 DiaryTracker Mobile Rewrite - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 27,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P10 Panel Automation Programme - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 28,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "ClientPortal - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 29,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "ReportFactory - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 30,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Minor Changes", "ReportFactory - Minor Changes" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 31,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Support", "DeliveryTracker - Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P11 Client Onboarding Automation - Project" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 33,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project", "P12 API Delivery Programme - Project" });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "P01 CoreDataWarehouse Cloud Migration");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "P02 AuthService Replacement");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "P03 Security & Compliance Programme");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "P04 DevPortal Rebuild");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "P05 ML Forecasting Platform");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "Name",
                value: "P06 Real-Time Analytics Initiative");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "Name",
                value: "P07 SegmentBuilder Enhancement");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "Name",
                value: "P08 CallCentre Suite Replacement");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "Name",
                value: "P09 DiaryTracker Mobile Rewrite");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 10,
                column: "Name",
                value: "P10 Panel Automation Programme");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 11,
                column: "Name",
                value: "P11 Client Onboarding Automation");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 12,
                column: "Name",
                value: "P12 API Delivery Programme");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "CoreDataWarehouse — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Minor Changes", "CoreDataWarehouse — Application Minor Changes" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "APIGateway — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "SurveyEngine — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "AuthService — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "ObservabilityStack — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "CoreDataWarehouse Cloud Migration" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "AuthService Replacement" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "Security & Compliance Programme" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "DevPortal Rebuild" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "InsightHub — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Minor Changes", "InsightHub — Application Minor Changes" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "DataMapper — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "QuotaManager — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "SegmentBuilder — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "PanelIQ — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "ML Forecasting Platform" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "Real-Time Analytics Initiative" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "SegmentBuilder Enhancement" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "FieldOps — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "WebCollect — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "SampleRouter — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "CallCentre Suite — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 24,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "DiaryTracker — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 25,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "CallCentre Suite Replacement" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 26,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "DiaryTracker Mobile Rewrite" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 27,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "Panel Automation Programme" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 28,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "ClientPortal — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 29,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "ReportFactory — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 30,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Minor Changes", "ReportFactory — Application Minor Changes" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 31,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Application Support", "DeliveryTracker — Application Support" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "Client Onboarding Automation" });

            migrationBuilder.UpdateData(
                table: "Activities",
                keyColumn: "Id",
                keyValue: 33,
                columns: new[] { "ActivityType", "Name" },
                values: new object[] { "Project Changes", "API Delivery Programme" });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "CoreDataWarehouse Cloud Migration");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "AuthService Replacement");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "Security & Compliance Programme");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "DevPortal Rebuild");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "ML Forecasting Platform");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "Name",
                value: "Real-Time Analytics Initiative");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "Name",
                value: "SegmentBuilder Enhancement");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "Name",
                value: "CallCentre Suite Replacement");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "Name",
                value: "DiaryTracker Mobile Rewrite");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 10,
                column: "Name",
                value: "Panel Automation Programme");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 11,
                column: "Name",
                value: "Client Onboarding Automation");

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 12,
                column: "Name",
                value: "API Delivery Programme");
        }
    }
}
