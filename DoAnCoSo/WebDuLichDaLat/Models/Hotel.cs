using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// Hotel - Khách sạn
    /// Hotel có tọa độ Latitude/Longitude trực tiếp
    /// </summary>
    public class Hotel
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

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

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

        // Giữ lại TouristPlaceId để tương thích ngược (có thể xóa sau khi migrate data)
        [StringLength(6)] 
        public string? TouristPlaceId { get; set; }
        public TouristPlace? TouristPlace { get; set; }
    }
}