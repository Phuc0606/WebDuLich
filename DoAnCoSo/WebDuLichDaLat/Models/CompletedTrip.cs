using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// Bảng lưu lịch sử các chuyến đã ghép thành công và hoàn thành
    /// Khi chuyến đầy (ví dụ 4/4 ghế), thông tin sẽ được copy vào bảng này
    /// </summary>
    public class CompletedTrip
    {
        [Key]
        public int Id { get; set; }

        // Thông tin xe gốc (VehicleId để tham chiếu)
        public int VehicleId { get; set; }

        [Required]
        [StringLength(100)]
        public string DriverName { get; set; }

        [StringLength(20)]
        public string DriverPhone { get; set; }

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

        public DateTime? ActualDepartureTime { get; set; } // Thời gian khởi hành thực tế

        public DateTime? ActualArrivalTime { get; set; } // Thời gian đến nơi thực tế

        [Required]
        public int TotalSeats { get; set; }

        [Required]
        public int OccupiedSeats { get; set; } // Số ghế đã sử dụng (không tính tài xế)

        [StringLength(50)]
        public string VehicleType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; } // Tổng chi phí chuyến đi

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CostPerSeat { get; set; } // Chi phí mỗi ghế (nếu chia đều)

        // Tuyến chính (polyline từ OSRM) - lưu dạng JSON array of [lon, lat]
        [Column(TypeName = "nvarchar(max)")]
        public string? RoutePolyline { get; set; }

        // Trạng thái chuyến
        public TripStatus Status { get; set; } = TripStatus.Completed; // Đã hoàn thành, đang đi, đã hủy

        // Thời gian ghi nhận
        public DateTime CompletedAt { get; set; } = DateTime.Now; // Thời điểm chuyến được đánh dấu hoàn thành

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Thời điểm tạo record

        // Navigation properties
        public virtual ICollection<CompletedTripPassenger> Passengers { get; set; } = new List<CompletedTripPassenger>();
    }

    /// <summary>
    /// Trạng thái chuyến
    /// </summary>
    public enum TripStatus
    {
        Pending = 0,        // Đang chờ
        InProgress = 1,     // Đang đi
        Completed = 2,      // Đã hoàn thành
        Cancelled = 3       // Đã hủy
    }

    /// <summary>
    /// Bảng lưu danh sách hành khách trong chuyến đã hoàn thành
    /// </summary>
    public class CompletedTripPassenger
    {
        [Key]
        public int Id { get; set; }

        public int CompletedTripId { get; set; }

        public int OriginalPassengerId { get; set; } // ID hành khách gốc trong bảng Passengers

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

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

        public int? PickupOrder { get; set; } // Thứ tự điểm đón

        public int? DropoffOrder { get; set; } // Thứ tự điểm trả

        public int RequiredSeats { get; set; } = 1; // Số ghế yêu cầu

        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; } // Chi phí hành khách phải trả

        public int? GroupId { get; set; } // Nếu thuộc nhóm

        [StringLength(100)]
        public string GroupName { get; set; } // Tên nhóm (lưu snapshot)

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("CompletedTripId")]
        public virtual CompletedTrip CompletedTrip { get; set; }
    }
}
































