using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class MakeFeedbackCategoryAndPriorityOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "priority",
                table: "feedbacks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                defaultValueSql: "'Medium'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValueSql: "'Medium'::character varying");

            migrationBuilder.AlterColumn<int>(
                name: "category_id",
                table: "feedbacks",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "priority",
                table: "feedbacks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValueSql: "'Medium'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldDefaultValueSql: "'Medium'::character varying");

            migrationBuilder.AlterColumn<int>(
                name: "category_id",
                table: "feedbacks",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
