using System.ComponentModel.DataAnnotations;

namespace WebDuLichDaLat.Models
{
    public class Contact
    {
        [Key] 
        public int Id { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Chủ đề không được để trống")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Nội dung không được để trống")]
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

    }
}
