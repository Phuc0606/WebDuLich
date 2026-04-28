using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// Attraction - Điểm tham quan
    /// Attraction PHẢI tham chiếu Location (không tham chiếu trực tiếp Area)
    /// </summary>
    public class Attraction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TicketPrice { get; set; }

        // ✅ LocationId đã bị xóa khỏi database (Migration 20260102165526_XoaIdlocation)
        // Attraction không còn relationship với Location

        // Giữ lại TouristPlaceId để tương thích ngược (có thể xóa sau khi migrate data)
        [StringLength(6)]
        public string? TouristPlaceId { get; set; }
        public TouristPlace? TouristPlace { get; set; }
    }
} 