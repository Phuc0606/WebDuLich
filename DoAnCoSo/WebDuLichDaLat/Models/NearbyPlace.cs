using System.ComponentModel.DataAnnotations;

namespace WebDuLichDaLat.Models
{
    public class NearbyPlace
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // "Hotel" hoặc "Restaurant"
        public string Address { get; set; }
        public string Phone { get; set; }
        public decimal Price { get; set; } // Giá phòng hoặc giá trung bình/người
        public string PriceUnit { get; set; } // "per_night", "per_person", "per_meal"
        public double Distance { get; set; } // Khoảng cách từ địa điểm chính (km)
        public double Rating { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        
        // Liên kết với địa điểm du lịch
        public string TouristPlaceId { get; set; }
        public TouristPlace TouristPlace { get; set; }
    }
} 