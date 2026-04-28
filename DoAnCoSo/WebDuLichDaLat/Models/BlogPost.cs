using System;
using System.ComponentModel.DataAnnotations;

namespace WebDuLichDaLat.Models
{
    public class BlogPost
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public string? ImageUrl { get; set; }

        [DataType(DataType.Date)]
        public DateTime PostedDate { get; set; } = DateTime.Now;

        public string? Author { get; set; }
    }
}
