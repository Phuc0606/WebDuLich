using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// Bảng lưu các yêu cầu ghép xe đang chờ (chưa ghép thành công hoặc đã hủy)
    /// Người dùng có thể hủy nếu thấy giá không ổn
    /// </summary>
    public class PendingCarpoolRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string PassengerName { get; set; }

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

        [Required]
        public DateTime PreferredDepartureTime { get; set; }

        public DateTime? PreferredArrivalTime { get; set; }

        [Required]
        public int RequiredSeats { get; set; } = 1; // Số ghế yêu cầu

        [Required]
        public int RequestedVehicleSeats { get; set; } = 4; // Loại xe mong muốn (4, 7, 9 chỗ)

        public bool IsGroup { get; set; } = false; // Nhóm bắt buộc đi chung

        public bool PrivateGroup { get; set; } = false; // Nhóm riêng (không ghép thêm người lạ)

        public int? GroupId { get; set; } // Nếu thuộc nhóm

        // Chi phí dự kiến (để người dùng xem và quyết định)
        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedCost { get; set; } // Chi phí ước tính

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CostPerSeat { get; set; } // Chi phí mỗi ghế

        public double? EstimatedDistance { get; set; } // Khoảng cách ước tính (km)

        // Trạng thái yêu cầu
        public RequestStatus Status { get; set; } = RequestStatus.Pending; // Pending, Matched, Cancelled, Expired

        // ID xe đã được ghép (nếu có)
        public int? MatchedVehicleId { get; set; }

        // ID hành khách đã được tạo trong bảng Passengers (nếu có)
        public int? PassengerId { get; set; }

        // Thời gian
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? MatchedAt { get; set; } // Thời gian được ghép thành công

        public DateTime? CancelledAt { get; set; } // Thời gian hủy

        public string? CancellationReason { get; set; } // Lý do hủy (tùy chọn)

        // Navigation property
        [ForeignKey("GroupId")]
        public virtual PassengerGroup? Group { get; set; }
    }

    /// <summary>
    /// Trạng thái yêu cầu ghép xe
    /// </summary>
    public enum RequestStatus
    {
        Pending = 0,        // Đang chờ ghép
        Matched = 1,        // Đã ghép thành công
        Cancelled = 2,      // Đã hủy (người dùng hủy hoặc giá không ổn)
        Expired = 3         // Đã hết hạn (quá thời gian khởi hành)
    }
}
































