using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebDuLichDaLat.Migrations
{
    /// <inheritdoc />
    public partial class XoaIdlocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
            name: "FK_Attractions_Locations_LocationId",
            table: "Attractions");

            // 2. Xóa Index (Chỉ mục) của cột đó
            migrationBuilder.DropIndex(
                name: "IX_Attractions_LocationId",
                table: "Attractions");

            // 3. Cuối cùng mới xóa Cột dữ liệu
            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Attractions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
