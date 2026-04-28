using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// Location - Trái tim của hệ thống
    /// Mọi thứ hiển thị trên bản đồ đều là Location
    /// Location PHẢI thuộc 1 Area (Region)
    /// </summary>
    public class Location
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên địa điểm không được để trống")]
        [StringLength(150, ErrorMessage = "Tên địa điểm không được quá 150 kí tự")]
        public required string Name { get; set; }

        /// <summary>
        /// AreaId (RegionId) - Location PHẢI thuộc 1 Area
        /// Dùng để: Dropdown chọn khu, Giảm dataset khi query, Phân nhóm hiển thị
        /// KHÔNG dùng để tính "gần"
        /// </summary>
        [Required(ErrorMessage = "Location phải thuộc một khu vực")]
        public int AreaId { get; set; }

        [ForeignKey(nameof(AreaId))]
        public virtual Region Area { get; set; } = null!;

        /// <summary>
        /// Latitude - Vĩ độ (GPS)
        /// Dùng để tính khoảng cách, hiển thị trên bản đồ
        /// </summary>
        [Required]
        [Column(TypeName = "float")]
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude - Kinh độ (GPS)
        /// Dùng để tính khoảng cách, hiển thị trên bản đồ
        /// </summary>
        [Required]
        [Column(TypeName = "float")]
        public double Longitude { get; set; }

        /// <summary>
        /// CategoryId - Phân loại Location (Hotel, Restaurant, Cafe, TouristSpot, etc.)
        /// </summary>
        public int? CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public virtual Category? Category { get; set; }

        // Navigation properties - Các entity tham chiếu đến Location này
        // TouristPlace, Hotel và Restaurant giờ có tọa độ trực tiếp, không còn relationship với Location
        // Attraction không còn relationship với Location (đã xóa LocationId khỏi database)
        public ICollection<CampingSite> CampingSites { get; set; } = new List<CampingSite>();
    }
}




