using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LayoutManagementAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    LicenseNumber = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ClientType = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LayoutPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", nullable: false),
                    UploadedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerPayable = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SurveyorAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TipperCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TipperCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MiscellaneousAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DocumentJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LayoutPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlotId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    ReminderDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SketchName = table.Column<string>(type: "TEXT", nullable: false),
                    PlotNumber = table.Column<string>(type: "TEXT", nullable: false),
                    AdvanceReceived = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AdvanceReceivedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RegistrationOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReadOn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationReminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    IsBuiltInAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    LoginRestricted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    StreetAddress = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PropertyType = table.Column<int>(type: "INTEGER", nullable: false),
                    ListingType = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Bedrooms = table.Column<int>(type: "INTEGER", nullable: false),
                    Bathrooms = table.Column<decimal>(type: "TEXT", precision: 4, scale: 1, nullable: false),
                    SquareFeet = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ListedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AgentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Properties_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Inquiries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    LayoutPlanId = table.Column<int>(type: "INTEGER", nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inquiries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inquiries_LayoutPlans_LayoutPlanId",
                        column: x => x.LayoutPlanId,
                        principalTable: "LayoutPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LayoutAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LayoutPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LayoutAccesses_LayoutPlans_LayoutPlanId",
                        column: x => x.LayoutPlanId,
                        principalTable: "LayoutPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LayoutImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LayoutPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LayoutImages_LayoutPlans_LayoutPlanId",
                        column: x => x.LayoutPlanId,
                        principalTable: "LayoutPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OwnerPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LayoutPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaidOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OwnerPayments_LayoutPlans_LayoutPlanId",
                        column: x => x.LayoutPlanId,
                        principalTable: "LayoutPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Plots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LayoutPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlotNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Area = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    LengthFeet = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BreadthFeet = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TopFeet = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RightFeet = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BottomFeet = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    LeftFeet = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Facing = table.Column<string>(type: "TEXT", nullable: true),
                    VerticesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientName = table.Column<string>(type: "TEXT", nullable: true),
                    ClientMobile = table.Column<string>(type: "TEXT", nullable: true),
                    AdvanceReceived = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AdvanceReceivedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RegistrationOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BlockedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plots_LayoutPlans_LayoutPlanId",
                        column: x => x.LayoutPlanId,
                        principalTable: "LayoutPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlotSurveys",
                columns: table => new
                {
                    PlotId = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlotSurveys", x => x.PlotId);
                    table.ForeignKey(
                        name: "FK_PlotSurveys_Plots_PlotId",
                        column: x => x.PlotId,
                        principalTable: "Plots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inquiries_LayoutPlanId",
                table: "Inquiries",
                column: "LayoutPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutAccesses_LayoutPlanId_UserId",
                table: "LayoutAccesses",
                columns: new[] { "LayoutPlanId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LayoutImages_LayoutPlanId",
                table: "LayoutImages",
                column: "LayoutPlanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnerPayments_LayoutPlanId",
                table: "OwnerPayments",
                column: "LayoutPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Plots_LayoutPlanId_PlotNumber",
                table: "Plots",
                columns: new[] { "LayoutPlanId", "PlotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_AgentId",
                table: "Properties",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationReminders_PlotId_ReminderDate",
                table: "RegistrationReminders",
                columns: new[] { "PlotId", "ReminderDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUserName",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Inquiries");

            migrationBuilder.DropTable(
                name: "LayoutAccesses");

            migrationBuilder.DropTable(
                name: "LayoutImages");

            migrationBuilder.DropTable(
                name: "OwnerPayments");

            migrationBuilder.DropTable(
                name: "PlotSurveys");

            migrationBuilder.DropTable(
                name: "Properties");

            migrationBuilder.DropTable(
                name: "RegistrationReminders");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Plots");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "LayoutPlans");
        }
    }
}
