using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    public class Passenger
    {
        [Key]
        public int Id { get; set; }

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

        [Required]
        public DateTime PreferredDepartureTime { get; set; }

        public DateTime? PreferredArrivalTime { get; set; }

        public int? GroupId { get; set; } // Nếu thuộc nhóm bắt buộc đi chung

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsMatched { get; set; } = false;

        public int? MatchedVehicleId { get; set; }

        // Thứ tự đón/trả trên tuyến chính (dùng cho ghép theo tuyến)
        public int? PickupOrder { get; set; } // Thứ tự điểm đón trên tuyến (1, 2, 3...)
        public int? DropoffOrder { get; set; } // Thứ tự điểm trả trên tuyến (1, 2, 3...)

        // Navigation property
        [ForeignKey("GroupId")]
        public virtual PassengerGroup? Group { get; set; }
    }
}














































