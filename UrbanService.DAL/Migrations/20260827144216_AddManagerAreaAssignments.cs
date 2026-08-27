using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerAreaAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manager_area_assignments",
                columns: table => new
                {
                    manager_area_assignment_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    manager_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("manager_area_assignments_pkey", x => x.manager_area_assignment_id);
                    table.ForeignKey(
                        name: "fk_manager_area_assignment_area",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_manager_area_assignment_created_by",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_manager_area_assignment_manager",
                        column: x => x.manager_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_manager_area_assignment_updated_by",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manager_area_assignments_area_id",
                table: "manager_area_assignments",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_manager_area_assignments_created_by_user_id",
                table: "manager_area_assignments",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_manager_area_assignments_updated_by_user_id",
                table: "manager_area_assignments",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_manager_area_assignment_scope",
                table: "manager_area_assignments",
                columns: new[] { "manager_user_id", "area_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manager_area_assignments");
        }
    }
}
