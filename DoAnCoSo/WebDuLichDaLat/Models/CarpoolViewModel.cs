using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebDuLichDaLat.Models
{
    public class CarpoolViewModel
    {
        // Thông tin khách hàng
        [Required(ErrorMessage = "Vui lòng nhập tên")]
        [Display(Name = "Tên khách hàng")]
        public string PassengerName { get; set; }

        [Display(Name = "Số điện thoại")]
        [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự")]
        [RegularExpression(@"^[\d\s\+\-\(\)]*$", ErrorMessage = "Số điện thoại chỉ được chứa số, khoảng trắng, dấu +, -, ( và )")]
        public string PhoneNumber { get; set; }

        // Điểm đón
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ đón")]
        [Display(Name = "Địa chỉ đón")]
        public string PickupAddress { get; set; }

        public double PickupLatitude { get; set; }
        public double PickupLongitude { get; set; }

        // Điểm đến
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ đến")]
        [Display(Name = "Địa chỉ đến")]
        public string DropoffAddress { get; set; }

        public double DropoffLatitude { get; set; }
        public double DropoffLongitude { get; set; }

        // Thời gian
        [Required(ErrorMessage = "Vui lòng chọn thời gian khởi hành")]
        [Display(Name = "Thời gian khởi hành mong muốn")]
        public DateTime PreferredDepartureTime { get; set; } = DateTime.Now.AddHours(1);

        // Số lượng khách
        [Required(ErrorMessage = "Vui lòng nhập số lượng khách")]
        [Range(1, 16, ErrorMessage = "Số lượng khách phải từ 1 đến 16")]
        [Display(Name = "Số lượng khách")]
        public int NumberOfPassengers { get; set; } = 1;

        [Display(Name = "Loại xe mong muốn")]
        public int RequestedVehicleSeats { get; set; } = 4;

        public IReadOnlyList<int> SeatOptions { get; } = new List<int> { 4, 7, 9 }; // Khớp với SupportedCapacities

        [Display(Name = "Nhóm bắt buộc đi chung (ví dụ: 4 người)")]
        public bool IsGroup { get; set; } = false;

        [Display(Name = "Nhóm riêng (không ghép thêm người lạ)")]
        public bool PrivateGroup { get; set; } = false;

        // Trạng thái & kết quả
        public string? StatusMessage { get; set; }
        public CarpoolTripInfo? AssignedTrip { get; set; } // Giữ lại để tương thích
        public List<CarpoolTripInfo> AssignedTrips { get; set; } = new List<CarpoolTripInfo>(); // Danh sách các chuyến đã ghép (có thể nhiều chuyến nếu chia nhóm)
        public List<CarpoolTripInfo> OpenTrips { get; set; } = new List<CarpoolTripInfo>();

        public bool HasAssignment => AssignedTrip != null || AssignedTrips.Any();
    }

    public class CarpoolTripInfo
    {
        public int TripId { get; set; }
        public string PickupAddress { get; set; }
        public string DropoffAddress { get; set; }
        public DateTime DepartureTime { get; set; }
        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
        public decimal TotalCost { get; set; }
        public decimal CostPerSeat { get; set; }
        public string DriverName { get; set; }
        public string DriverPhone { get; set; }
        public string LicensePlate { get; set; }
        public string VehicleType { get; set; }
        public double? TripDistance { get; set; } // Khoảng cách của chuyến (km)
        public List<CarpoolPassengerInfo> Passengers { get; set; } = new List<CarpoolPassengerInfo>();
        public int? CurrentPassengerId { get; set; }
        public decimal? CurrentPassengerCost { get; set; }
        public bool IsFull => AvailableSeats <= 0;
        public bool CanCancel => CurrentPassengerId.HasValue;
    }

    public class CarpoolPassengerInfo
    {
        public int PassengerId { get; set; }
        public string Name { get; set; }
        public int Seats { get; set; }
        public decimal Cost { get; set; }
        public double? DistanceKm { get; set; } // Khoảng cách đường đi (km)
        public string DistanceDisplay => DistanceKm.HasValue ? $"{DistanceKm.Value:F1} km" : "Đang tính...";
    }
}

