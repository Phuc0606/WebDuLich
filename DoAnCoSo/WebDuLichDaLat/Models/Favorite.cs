using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    public class Favorite
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!;
        public virtual User User { get; set; } = null!;

        [Required]
        [StringLength(6)]
        public string TouristPlaceId { get; set; } = null!;
        public virtual TouristPlace TouristPlace { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
