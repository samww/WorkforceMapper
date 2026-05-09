using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WorkforceMapper.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationRiskModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MinSupportFte",
                table: "Applications",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RecommendedSupportFte",
                table: "Applications",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "ApplicationDependencies",
                columns: table => new
                {
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    DependsOnApplicationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDependencies", x => new { x.ApplicationId, x.DependsOnApplicationId });
                    table.ForeignKey(
                        name: "FK_ApplicationDependencies_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationDependencies_Applications_DependsOnApplicationId",
                        column: x => x.DependsOnApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ApplicationDependencies",
                columns: new[] { "ApplicationId", "DependsOnApplicationId" },
                values: new object[,]
                {
                    { 1, 4 },
                    { 2, 4 },
                    { 3, 1 },
                    { 5, 4 },
                    { 6, 1 },
                    { 7, 4 },
                    { 8, 4 },
                    { 9, 1 },
                    { 10, 4 },
                    { 11, 4 },
                    { 12, 4 },
                    { 13, 4 },
                    { 14, 4 },
                    { 15, 4 },
                    { 16, 4 },
                    { 17, 4 },
                    { 18, 4 }
                });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 2.0, 3.0 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.5, 2.5 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 2.0, 3.0 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 4.0, 5.0 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.0, 2.0 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.5, 2.5 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.5, 2.5 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.0, 1.5 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.0, 2.0 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 2.0, 3.0 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.5, 2.5 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.0, 1.5 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.0, 1.5 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.0, 2.0 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.5, 2.0 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.0, 1.5 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 1.0, 1.5 });

            migrationBuilder.UpdateData(
                table: "Applications",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "MinSupportFte", "RecommendedSupportFte" },
                values: new object[] { 2.0, 3.0 });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDependencies_DependsOnApplicationId",
                table: "ApplicationDependencies",
                column: "DependsOnApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationDependencies");

            migrationBuilder.DropColumn(
                name: "MinSupportFte",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "RecommendedSupportFte",
                table: "Applications");
        }
    }
}
