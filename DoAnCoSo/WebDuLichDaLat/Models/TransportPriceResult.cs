namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// Kết quả tính toán giá vận chuyển từ GPS đến Đà Lạt
    /// </summary>
    public class TransportPriceResult
    {
        /// <summary>
        /// Giá vận chuyển (VNĐ)
        /// </summary>
        public decimal Price { get; set; }
        
        /// <summary>
        /// "Fixed" = Giá cố định từ DB
        /// "Calculated" = Giá ước tính theo khoảng cách
        /// </summary>
        public string PriceType { get; set; } = "Calculated";
        
        /// <summary>
        /// Tên địa điểm gần nhất (VD: "Tây Ninh")
        /// </summary>
        public string? LocationName { get; set; }
        
        /// <summary>
        /// ID của địa điểm trong DB
        /// </summary>
        public int? LocationId { get; set; }
        
        /// <summary>
        /// Tên cũ của địa điểm (VD: "Long An" nếu IsMergedLocation=true)
        /// </summary>
        public string? OldLocationName { get; set; }
        
        /// <summary>
        /// Khoảng cách từ GPS người dùng đến địa điểm gần nhất (km)
        /// </summary>
        public double? DistanceFromLocation { get; set; }
        
        /// <summary>
        /// Khoảng cách từ GPS người dùng đến Đà Lạt (km)
        /// </summary>
        public double DistanceToDalat { get; set; }
        
        /// <summary>
        /// Ghi chú hiển thị cho user
        /// </summary>
        public string Note { get; set; } = string.Empty;
        
        /// <summary>
        /// Có phải địa điểm bị sáp nhập không
        /// </summary>
        public bool IsMergedLocation { get; set; }
    }
}

