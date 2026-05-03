using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WorkforceMapper.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ArrUsd = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResourceId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", nullable: false),
                    Company = table.Column<string>(type: "TEXT", nullable: false),
                    Seniority = table.Column<string>(type: "TEXT", nullable: false),
                    RoleType = table.Column<string>(type: "TEXT", nullable: false),
                    Division = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: false),
                    MonthlyRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    Jan = table.Column<int>(type: "INTEGER", nullable: false),
                    Feb = table.Column<int>(type: "INTEGER", nullable: false),
                    Mar = table.Column<int>(type: "INTEGER", nullable: false),
                    Apr = table.Column<int>(type: "INTEGER", nullable: false),
                    May = table.Column<int>(type: "INTEGER", nullable: false),
                    Jun = table.Column<int>(type: "INTEGER", nullable: false),
                    Jul = table.Column<int>(type: "INTEGER", nullable: false),
                    Aug = table.Column<int>(type: "INTEGER", nullable: false),
                    Sep = table.Column<int>(type: "INTEGER", nullable: false),
                    Oct = table.Column<int>(type: "INTEGER", nullable: false),
                    Nov = table.Column<int>(type: "INTEGER", nullable: false),
                    Dec = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductApplications",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductApplications", x => new { x.ProductId, x.ApplicationId });
                    table.ForeignKey(
                        name: "FK_ProductApplications_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductApplications_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ActivityType = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    Division = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activities_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Activities_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectProducts",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectProducts", x => new { x.ProjectId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ProjectProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectProducts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mappings",
                columns: table => new
                {
                    ResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<string>(type: "TEXT", nullable: false),
                    Percent = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mappings", x => new { x.ResourceId, x.ActivityId, x.Month });
                    table.ForeignKey(
                        name: "FK_Mappings_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mappings_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Applications",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "CoreDataWarehouse" },
                    { 2, "APIGateway" },
                    { 3, "SurveyEngine" },
                    { 4, "AuthService" },
                    { 5, "ObservabilityStack" },
                    { 6, "InsightHub" },
                    { 7, "DataMapper" },
                    { 8, "QuotaManager" },
                    { 9, "SegmentBuilder" },
                    { 10, "FieldOps" },
                    { 11, "WebCollect" },
                    { 12, "SampleRouter" },
                    { 13, "CallCentre Suite" },
                    { 14, "DiaryTracker" },
                    { 15, "ClientPortal" },
                    { 16, "ReportFactory" },
                    { 17, "DeliveryTracker" },
                    { 18, "PanelIQ" }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "ArrUsd", "Name" },
                values: new object[,]
                {
                    { 1, 6200000m, "DataLicensing" },
                    { 2, 5100000m, "MediaMeasurement" },
                    { 3, 4600000m, "RetailInsights" },
                    { 4, 4200000m, "BrandTracking Platform" },
                    { 5, 3800000m, "CustomerSatisfaction Suite" },
                    { 6, 3400000m, "ConsumerPanel" },
                    { 7, 1600000m, "FieldworkServices" }
                });

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "CoreDataWarehouse Cloud Migration" },
                    { 2, "AuthService Replacement" },
                    { 3, "Security & Compliance Programme" },
                    { 4, "DevPortal Rebuild" },
                    { 5, "ML Forecasting Platform" },
                    { 6, "Real-Time Analytics Initiative" },
                    { 7, "SegmentBuilder Enhancement" },
                    { 8, "CallCentre Suite Replacement" },
                    { 9, "DiaryTracker Mobile Rewrite" },
                    { 10, "Panel Automation Programme" },
                    { 11, "Client Onboarding Automation" },
                    { 12, "API Delivery Programme" }
                });

            migrationBuilder.InsertData(
                table: "Activities",
                columns: new[] { "Id", "ActivityType", "ApplicationId", "Division", "Name", "ProjectId" },
                values: new object[,]
                {
                    { 1, "Application Support", 1, "Platform Engineering", "CoreDataWarehouse — Application Support", null },
                    { 2, "Application Minor Changes", 1, "Platform Engineering", "CoreDataWarehouse — Application Minor Changes", null },
                    { 3, "Application Support", 2, "Platform Engineering", "APIGateway — Application Support", null },
                    { 4, "Application Support", 3, "Platform Engineering", "SurveyEngine — Application Support", null },
                    { 5, "Application Support", 4, "Platform Engineering", "AuthService — Application Support", null },
                    { 6, "Application Support", 5, "Platform Engineering", "ObservabilityStack — Application Support", null },
                    { 7, "Project Changes", null, "Platform Engineering", "CoreDataWarehouse Cloud Migration", 1 },
                    { 8, "Project Changes", null, "Platform Engineering", "AuthService Replacement", 2 },
                    { 9, "Project Changes", null, "Platform Engineering", "Security & Compliance Programme", 3 },
                    { 10, "Project Changes", null, "Platform Engineering", "DevPortal Rebuild", 4 },
                    { 11, "Application Support", 6, "Analytics", "InsightHub — Application Support", null },
                    { 12, "Application Minor Changes", 6, "Analytics", "InsightHub — Application Minor Changes", null },
                    { 13, "Application Support", 7, "Analytics", "DataMapper — Application Support", null },
                    { 14, "Application Support", 8, "Analytics", "QuotaManager — Application Support", null },
                    { 15, "Application Support", 9, "Analytics", "SegmentBuilder — Application Support", null },
                    { 16, "Application Support", 18, "Analytics", "PanelIQ — Application Support", null },
                    { 17, "Project Changes", null, "Analytics", "ML Forecasting Platform", 5 },
                    { 18, "Project Changes", null, "Analytics", "Real-Time Analytics Initiative", 6 },
                    { 19, "Project Changes", null, "Analytics", "SegmentBuilder Enhancement", 7 },
                    { 20, "Application Support", 10, "Data Collection", "FieldOps — Application Support", null },
                    { 21, "Application Support", 11, "Data Collection", "WebCollect — Application Support", null },
                    { 22, "Application Support", 12, "Data Collection", "SampleRouter — Application Support", null },
                    { 23, "Application Support", 13, "Data Collection", "CallCentre Suite — Application Support", null },
                    { 24, "Application Support", 14, "Data Collection", "DiaryTracker — Application Support", null },
                    { 25, "Project Changes", null, "Data Collection", "CallCentre Suite Replacement", 8 },
                    { 26, "Project Changes", null, "Data Collection", "DiaryTracker Mobile Rewrite", 9 },
                    { 27, "Project Changes", null, "Data Collection", "Panel Automation Programme", 10 },
                    { 28, "Application Support", 15, "Client Services", "ClientPortal — Application Support", null },
                    { 29, "Application Support", 16, "Client Services", "ReportFactory — Application Support", null },
                    { 30, "Application Minor Changes", 16, "Client Services", "ReportFactory — Application Minor Changes", null },
                    { 31, "Application Support", 17, "Client Services", "DeliveryTracker — Application Support", null },
                    { 32, "Project Changes", null, "Client Services", "Client Onboarding Automation", 11 },
                    { 33, "Project Changes", null, "Client Services", "API Delivery Programme", 12 }
                });

            migrationBuilder.InsertData(
                table: "ProductApplications",
                columns: new[] { "ApplicationId", "ProductId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 15, 1 },
                    { 1, 2 },
                    { 6, 2 },
                    { 7, 2 },
                    { 14, 2 },
                    { 1, 3 },
                    { 3, 3 },
                    { 6, 3 },
                    { 9, 3 },
                    { 1, 4 },
                    { 3, 4 },
                    { 6, 4 },
                    { 18, 4 },
                    { 1, 5 },
                    { 3, 5 },
                    { 11, 5 },
                    { 16, 5 },
                    { 7, 6 },
                    { 11, 6 },
                    { 12, 6 },
                    { 18, 6 },
                    { 8, 7 },
                    { 10, 7 },
                    { 12, 7 },
                    { 13, 7 }
                });

            migrationBuilder.InsertData(
                table: "ProjectProducts",
                columns: new[] { "ProductId", "ProjectId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 4, 1 },
                    { 5, 1 },
                    { 1, 2 },
                    { 2, 2 },
                    { 3, 2 },
                    { 4, 2 },
                    { 5, 2 },
                    { 6, 2 },
                    { 7, 2 },
                    { 1, 3 },
                    { 2, 3 },
                    { 3, 3 },
                    { 4, 3 },
                    { 5, 3 },
                    { 6, 3 },
                    { 7, 3 },
                    { 2, 5 },
                    { 3, 5 },
                    { 4, 5 },
                    { 1, 6 },
                    { 2, 6 },
                    { 3, 7 },
                    { 7, 8 },
                    { 2, 9 },
                    { 4, 10 },
                    { 6, 10 },
                    { 1, 11 },
                    { 5, 11 },
                    { 1, 12 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ApplicationId",
                table: "Activities",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ProjectId",
                table: "Activities",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Mappings_ActivityId",
                table: "Mappings",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductApplications_ApplicationId",
                table: "ProductApplications",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProducts_ProductId",
                table: "ProjectProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ResourceId",
                table: "Resources",
                column: "ResourceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mappings");

            migrationBuilder.DropTable(
                name: "ProductApplications");

            migrationBuilder.DropTable(
                name: "ProjectProducts");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
