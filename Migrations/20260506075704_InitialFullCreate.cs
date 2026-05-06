using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ProductionSystem.Migrations
{
    /// <inheritdoc />
    public partial class InitialFullCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "TEXT", nullable: false),
                    MinimalStock = table.Column<decimal>(type: "decimal(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Specifications = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    MinimalStock = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductionTimePerUnit = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductMaterials",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityNeeded = table.Column<decimal>(type: "decimal(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMaterials", x => new { x.ProductId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_ProductMaterials_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductMaterials_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductionLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    EfficiencyFactor = table.Column<float>(type: "REAL", nullable: false),
                    CurrentWorkOrderId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductionLineId = table.Column<int>(type: "INTEGER", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActualStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EstimatedEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_ProductionLines_ProductionLineId",
                        column: x => x.ProductionLineId,
                        principalTable: "ProductionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Materials",
                columns: new[] { "Id", "MinimalStock", "Name", "Quantity", "UnitOfMeasure" },
                values: new object[,]
                {
                    { 1, 100m, "Сталь листовая", 500m, "кг" },
                    { 2, 150m, "Алюминий", 80m, "кг" },
                    { 3, 500m, "Болты М8", 2000m, "шт" },
                    { 4, 50m, "Масло машинное", 30m, "литр" },
                    { 5, 100m, "Пластик АБС", 300m, "кг" }
                });

            migrationBuilder.InsertData(
                table: "ProductionLines",
                columns: new[] { "Id", "CurrentWorkOrderId", "EfficiencyFactor", "Name", "Status" },
                values: new object[,]
                {
                    { 1, null, 1f, "Линия А — Механообработка", "Active" },
                    { 2, null, 1.2f, "Линия Б — Сборка", "Stopped" },
                    { 3, null, 0.8f, "Линия В — Покраска", "Active" }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Category", "Description", "MinimalStock", "Name", "ProductionTimePerUnit", "Specifications" },
                values: new object[,]
                {
                    { 1, "Насосы", "Корпус центробежного насоса", 10, "Корпус насоса", 45, null },
                    { 2, "Редукторы", "Крышка цилиндрического редуктора", 5, "Крышка редуктора", 30, null },
                    { 3, "Конструкции", "Опорный кронштейн для оборудования", 20, "Кронштейн опорный", 15, null }
                });

            migrationBuilder.InsertData(
                table: "ProductMaterials",
                columns: new[] { "MaterialId", "ProductId", "QuantityNeeded" },
                values: new object[,]
                {
                    { 1, 1, 5.5m },
                    { 3, 1, 8m },
                    { 2, 2, 3.2m },
                    { 3, 2, 12m },
                    { 1, 3, 2.0m },
                    { 3, 3, 4m }
                });

            migrationBuilder.InsertData(
                table: "WorkOrders",
                columns: new[] { "Id", "ActualStartDate", "EstimatedEndDate", "ProductId", "ProductionLineId", "Progress", "Quantity", "StartDate", "Status" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2025, 1, 20, 9, 0, 0, 0, DateTimeKind.Unspecified), 1, 1, 0, 10, new DateTime(2025, 1, 10, 9, 0, 0, 0, DateTimeKind.Unspecified), "Pending" },
                    { 2, null, new DateTime(2025, 1, 18, 9, 0, 0, 0, DateTimeKind.Unspecified), 2, null, 0, 5, new DateTime(2025, 1, 15, 9, 0, 0, 0, DateTimeKind.Unspecified), "Pending" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionLines_CurrentWorkOrderId",
                table: "ProductionLines",
                column: "CurrentWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductMaterials_MaterialId",
                table: "ProductMaterials",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ProductId",
                table: "WorkOrders",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ProductionLineId",
                table: "WorkOrders",
                column: "ProductionLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionLines_WorkOrders_CurrentWorkOrderId",
                table: "ProductionLines",
                column: "CurrentWorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionLines_WorkOrders_CurrentWorkOrderId",
                table: "ProductionLines");

            migrationBuilder.DropTable(
                name: "ProductMaterials");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropTable(
                name: "ProductionLines");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
