using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// Region (Area) - Khu vực phân vùng
    /// Dùng để: Dropdown chọn khu, Giảm dataset khi query, Phân nhóm hiển thị
    /// ❌ KHÔNG dùng để tính "gần"
    /// ❌ KHÔNG lưu Lat/Lng (chỉ Location mới có Lat/Lng)
    /// </summary>
    public class Region
    {
        [Column("RegionId")]
        public int Id { get; set; }

        [Column("RegionName")]
        [Required, StringLength(50)]
        public string Name { get; set; } = string.Empty;

        // Navigation properties
        public ICollection<TouristPlace> TouristPlaces { get; set; } = new List<TouristPlace>();
        
        /// <summary>
        /// Locations thuộc khu vực này
        /// Area 1 ─── n Location
        /// </summary>
        public ICollection<Location> Locations { get; set; } = new List<Location>();
    }

}
