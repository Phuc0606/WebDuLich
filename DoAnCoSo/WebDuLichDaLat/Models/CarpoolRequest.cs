using System.ComponentModel.DataAnnotations;

namespace WebDuLichDaLat.Models
{
    public class CarpoolRequest
    {
        public string PickupAddress { get; set; }
        public double PickupLatitude { get; set; }
        public double PickupLongitude { get; set; }
        public string DropoffAddress { get; set; }
        public double DropoffLatitude { get; set; }
        public double DropoffLongitude { get; set; }
        public DateTime PreferredDepartureTime { get; set; }
        public DateTime? PreferredArrivalTime { get; set; }
        public int NumberOfPassengers { get; set; } = 1;
        public bool IsGroup { get; set; } = false; // Có phải nhóm bắt buộc đi chung không
        public string PassengerName { get; set; }
        public string PhoneNumber { get; set; }
    }
}
































