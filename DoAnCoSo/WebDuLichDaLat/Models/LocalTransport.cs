using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebDuLichDaLat.Models
{
    public class LocalTransport
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public TransportType TransportType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PricePerKm { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PricePerDay { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PricePerTrip { get; set; }

        public int? HotelId { get; set; }

        [StringLength(6)] // MUST match TouristPlace.Id exactly
        public string TouristPlaceId { get; set; }

        [StringLength(255)]
        public string Note { get; set; }

        // Navigation properties
        [ForeignKey("HotelId")]
        public virtual Hotel Hotel { get; set; }

        [ForeignKey("TouristPlaceId")]
        public virtual TouristPlace TouristPlace { get; set; }
    }

    public enum TransportType
    {
        HotelShuttle = 1,      // Xe buýt khách sạn  
        ElectricShuttle = 2,   // Xe điện trung chuyển
        LocalTaxi = 3,         // Taxi nội thành
        MotorbikeRental = 4    // Xe máy thuê
    }
}