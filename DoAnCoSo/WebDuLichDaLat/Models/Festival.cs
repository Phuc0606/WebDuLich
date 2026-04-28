using System;
using System.ComponentModel.DataAnnotations;

namespace WebDuLichDaLat.Models
{
    public class Festival
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        public string? ImageUrl { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Ngày kết thúc")]
        public DateTime? EndDate { get; set; }

        [StringLength(200)]
        [Display(Name = "Địa điểm")]
        public string? Location { get; set; }

        [StringLength(100)]
        [Display(Name = "Loại sự kiện")]
        public string? EventType { get; set; } // Lễ hội, Sự kiện văn hóa, Sự kiện du lịch, etc.

        [Display(Name = "Thời gian")]
        public string? Time { get; set; } // Ví dụ: "08:00 - 22:00"

        [Display(Name = "Giá vé")]
        public string? TicketPrice { get; set; } // Ví dụ: "Miễn phí" hoặc "50,000 VNĐ"

        [Display(Name = "Liên hệ")]
        public string? ContactInfo { get; set; }

        [Display(Name = "Website")]
        [Url]
        public string? Website { get; set; }

        [Display(Name = "Nội dung chi tiết")]
        public string? Content { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [DataType(DataType.DateTime)]
        public DateTime? UpdatedDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}























































