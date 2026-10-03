using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AllowCompletionDocumentWithoutProvider : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Sự vụ do Staff tự xử lý không có đơn vị bên thứ ba, nên minh chứng của nó
        /// không gắn được vào provider report nào. Hai cột này vì vậy phải cho phép
        /// null; minh chứng khi đó chỉ liên kết qua incident_id.
        ///
        /// Chỉ nới lỏng ràng buộc, không đụng tới dữ liệu đang có.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "provider_report_id",
                table: "completion_documents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "coordinator_id",
                table: "completion_documents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Schema cũ không có chỗ biểu diễn minh chứng không thuộc đơn vị nào, nên
        /// quay lui bắt buộc phải xóa đúng những dòng đó. Mặc định của EF là đặt
        /// khóa ngoại về 0, nhưng không có provider report nào mang id 0 nên lệnh
        /// đó sẽ vi phạm khóa ngoại và migration hỏng giữa chừng.
        ///
        /// Hãy sao lưu trước khi quay lui: thao tác này mất dữ liệu.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM completion_documents
                WHERE provider_report_id IS NULL
                   OR coordinator_id IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "provider_report_id",
                table: "completion_documents",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "coordinator_id",
                table: "completion_documents",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
