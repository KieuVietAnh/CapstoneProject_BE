using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddServicesAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "services",
                columns: table => new
                {
                    service_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    operator_id = table.Column<int>(type: "integer", nullable: false),
                    service_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_system_service = table.Column<bool>(type: "boolean", nullable: false),
                    base_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValueSql: "'VND'::character varying"),
                    external_service_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("services_pkey", x => x.service_id);
                    table.ForeignKey(
                        name: "fk_service_category",
                        column: x => x.category_id,
                        principalTable: "urban_service_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_operator",
                        column: x => x.operator_id,
                        principalTable: "service_operators",
                        principalColumn: "operator_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_payments",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValueSql: "'VND'::character varying"),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'Pending'::character varying"),
                    transaction_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("service_payments_pkey", x => x.payment_id);
                    table.ForeignKey(
                        name: "fk_service_payment_service",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "service_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_payment_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_payments_service_id",
                table: "service_payments",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_payments_user_id",
                table: "service_payments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_service_payment_transaction_reference",
                table: "service_payments",
                column: "transaction_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_services_category_id",
                table: "services",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "uq_service_operator_name",
                table: "services",
                columns: new[] { "operator_id", "service_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_payments");

            migrationBuilder.DropTable(
                name: "services");
        }
    }
}
