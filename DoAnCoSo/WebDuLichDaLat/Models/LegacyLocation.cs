using System.ComponentModel.DataAnnotations;

namespace WebDuLichDaLat.Models
{
    public class LegacyLocation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string OldName { get; set; }  // Ví dụ: "Long An"

        [Required]
        [StringLength(100)]
        public string CurrentName { get; set; } // Ví dụ: "Tây Ninh"

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // ✅ THÊM MỚI: Thông tin sáp nhập tỉnh thành
        public bool IsMergedLocation { get; set; }     // true nếu là tỉnh bị sáp nhập
        
        public DateTime? MergeDate { get; set; }       // Ngày sáp nhập (01/07/2025)
        
        [StringLength(255)]
        public string? MergeNote { get; set; }         // Ghi chú về sáp nhập
        
        public bool IsActive { get; set; } = true;     // false nếu không còn dùng

        // Quan hệ ngược
        public virtual ICollection<TransportPriceHistory> PriceHistories { get; set; } = new List<TransportPriceHistory>();
    }
}
