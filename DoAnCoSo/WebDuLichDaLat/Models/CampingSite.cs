using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// CampingSite - Khu cắm trại
    /// CampingSite PHẢI tham chiếu Location (không tham chiếu trực tiếp Area)
    /// Lat/Lng lấy từ Location
    /// </summary>
    public class CampingSite
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        [StringLength(300)]
        public string Address { get; set; }

        [StringLength(15)]
        public string Phone { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerNight { get; set; }

        /// <summary>
        /// Số lượng chỗ cắm trại có sẵn
        /// </summary>
        public int? AvailableSpots { get; set; }

        /// <summary>
        /// Tiện ích: Nhà vệ sinh, Nước, Điện, WiFi, Bãi đỗ xe, etc.
        /// </summary>
        [StringLength(500)]
        public string? Amenities { get; set; }

        /// <summary>
        /// Mô tả chi tiết về khu cắm trại
        /// </summary>
        [Column(TypeName = "ntext")]
        public string? Description { get; set; }

        /// <summary>
        /// URL hình ảnh chính
        /// </summary>
        [StringLength(500)]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Danh sách URL hình ảnh khác
        /// </summary>
        public List<string>? ImageUrls { get; set; }

        /// <summary>
        /// LocationId - CampingSite PHẢI thuộc 1 Location
        /// Lat/Lng lấy từ Location, không lưu trực tiếp trong CampingSite
        /// </summary>
        [Required(ErrorMessage = "CampingSite phải thuộc một Location")]
        public int LocationId { get; set; }

        [ForeignKey(nameof(LocationId))]
        public virtual Location Location { get; set; } = null!;

        // Giữ lại TouristPlaceId để tương thích ngược (có thể xóa sau khi migrate data)
        [StringLength(6)]
        public string? TouristPlaceId { get; set; }
        public TouristPlace? TouristPlace { get; set; }
    }
}





















