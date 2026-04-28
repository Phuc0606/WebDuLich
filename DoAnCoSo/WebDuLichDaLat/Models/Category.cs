using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace WebDuLichDaLat.Models
{
    public class Category
    {
        [Column("CategoryId")]
        public int Id { get; set; }

        [Column("CategoryName")]
        [Required, StringLength(50)]
        public string Name { get; set; } = string.Empty;

        // Navigation properties
        public ICollection<TouristPlace> TouristPlaces { get; set; } = new List<TouristPlace>();
        
        /// <summary>
        /// Locations thuộc category này
        /// </summary>
        public ICollection<Location> Locations { get; set; } = new List<Location>();
    }

}
