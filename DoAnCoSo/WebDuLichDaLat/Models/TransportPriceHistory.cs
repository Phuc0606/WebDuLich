using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    public class TransportPriceHistory
    {
        [Key]
        public int Id { get; set; }

        // Khóa ngoại đến loại phương tiện
        [Required]
        public int TransportOptionId { get; set; }
        public TransportOption TransportOption { get; set; }

        // Khóa ngoại đến địa điểm cũ
        [Required]
        public int LegacyLocationId { get; set; }
        public LegacyLocation LegacyLocation { get; set; }

        // Giá vé áp dụng riêng cho địa điểm đó
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
    }
}
