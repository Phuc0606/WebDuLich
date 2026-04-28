using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    public class PassengerGroup
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string GroupName { get; set; }

        [Required]
        public int RequiredSeats { get; set; } // Số ghế bắt buộc (ví dụ: 4 người)

        // Nhóm riêng: không cho phép ghép thêm người lạ vào xe
        public bool PrivateGroup { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual ICollection<Passenger> Passengers { get; set; } = new List<Passenger>();
    }
}














































