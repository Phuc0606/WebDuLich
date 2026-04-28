using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    public class Vehicle
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string DriverName { get; set; }

        [StringLength(20)]
        public string LicensePlate { get; set; }

        [Required]
        public double PickupLatitude { get; set; }

        [Required]
        public double PickupLongitude { get; set; }

        [StringLength(200)]
        public string PickupAddress { get; set; }

        [Required]
        public double DropoffLatitude { get; set; }

        [Required]
        public double DropoffLongitude { get; set; }

        [StringLength(200)]
        public string DropoffAddress { get; set; }

        [Required]
        public DateTime DepartureTime { get; set; }

        [Required]
        public int TotalSeats { get; set; }

        [Required]
        public int AvailableSeats { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPerKm { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? FixedPrice { get; set; } // Giá cố định cho chuyến đi (nếu có)

        [StringLength(50)]
        public string VehicleType { get; set; } // "Xe 4 chỗ", "Xe 7 chỗ", "Xe 16 chỗ", etc.

        // Tuyến chính (polyline từ OSRM) - lưu dạng JSON array of [lon, lat]
        [Column(TypeName = "nvarchar(max)")]
        public string? RoutePolyline { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
    }
}
































