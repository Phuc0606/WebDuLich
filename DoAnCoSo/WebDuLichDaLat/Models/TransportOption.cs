using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    public class TransportOption
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } // Ví dụ: "Xe khách", "Xe limousine", "Xe máy cá nhân"

        [Required]
        [StringLength(20)]
        public string Type { get; set; } // "Public" hoặc "Private"

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } // Giá mặc định (nếu không tra theo địa điểm)

        [Column(TypeName = "decimal(18,2)")]
        public decimal FixedPrice { get; set; } // Giá cố định (public transport)

        public bool IsSelfDrive { get; set; } // true nếu xe cá nhân

        [Column(TypeName = "decimal(18,2)")]
        public decimal FuelConsumption { get; set; } // lít/100km

        [Column(TypeName = "decimal(18,2)")]
        public decimal FuelPrice { get; set; } // VNĐ/lít

        // Quan hệ: nhiều giá theo từng địa điểm
        public ICollection<TransportPriceHistory> PriceHistories { get; set; } = new List<TransportPriceHistory>();
    }
}
