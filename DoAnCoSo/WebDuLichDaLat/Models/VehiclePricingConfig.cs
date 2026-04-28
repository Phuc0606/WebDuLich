using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// Bảng cấu hình giá theo loại xe
    /// Mỗi loại xe (4, 7, 9 chỗ) có giá nhiên liệu và lương tài xế khác nhau
    /// </summary>
    [Table("VehiclePricingConfigs")]
    public class VehiclePricingConfig
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Số chỗ xe: 4, 7, 9
        /// </summary>
        [Required]
        public int SeatCapacity { get; set; }

        /// <summary>
        /// Tên loại xe: "Xe 4 chỗ", "Xe 7 chỗ", "Xe 9 chỗ"
        /// </summary>
        [Required]
        [StringLength(100)]
        public string VehicleTypeName { get; set; }

        /// <summary>
        /// Giá nhiên liệu/km (xe lớn hơn = tiêu hao nhiều hơn)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FuelPricePerKm { get; set; }

        /// <summary>
        /// Lương tài xế/chuyến (xe lớn hơn = tài xế phải giỏi hơn = lương cao hơn)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DriverSalaryPerTrip { get; set; }

        /// <summary>
        /// Phí cầu đường (xe lớn có thể tốn phí cao tốc nhiều hơn)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TollFee { get; set; }

        /// <summary>
        /// Hệ số lợi nhuận (ví dụ: 1.2 = 20% lợi nhuận)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal ProfitMargin { get; set; }

        /// <summary>
        /// Chi phí tối thiểu/chuyến (xe lớn chi phí cố định cao hơn)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumTripCost { get; set; }

        /// <summary>
        /// Còn áp dụng không? (có thể vô hiệu hóa cấu hình cũ)
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Thời gian tạo
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
































