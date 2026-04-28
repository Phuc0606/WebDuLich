namespace WebDuLichDaLat.Models
{
    public class CarpoolMatchResult
    {
        public int VehicleId { get; set; }
        public string DriverName { get; set; }
        public string LicensePlate { get; set; }
        public string VehicleType { get; set; }
        public string DriverPhone { get; set; }
        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
        public int OccupiedSeats { get; set; } // Số ghế đã sử dụng
        public List<PassengerMatch> MatchedPassengers { get; set; } = new List<PassengerMatch>();
        public List<RoutePoint> OptimizedRoute { get; set; } = new List<RoutePoint>();
        public decimal TotalCost { get; set; } // Tổng chi phí chuyến đi
        public decimal CostPerPassenger { get; set; } // Chi phí mỗi người phải trả
        public double TotalDistance { get; set; }
        public DateTime EstimatedDepartureTime { get; set; }
        public DateTime EstimatedArrivalTime { get; set; }
        public string PickupAddress { get; set; }
        public string DropoffAddress { get; set; }
    }

    public class PassengerMatch
    {
        public int PassengerId { get; set; }
        public string PassengerName { get; set; }
        public string PickupAddress { get; set; }
        public string DropoffAddress { get; set; }
        public DateTime PickupTime { get; set; }
        public DateTime DropoffTime { get; set; }
        public decimal Cost { get; set; }
        public int SequenceOrder { get; set; } // Thứ tự đón/trả
    }

    public class RoutePoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; }
        public string Type { get; set; } // "pickup" or "dropoff"
        public int? PassengerId { get; set; }
        public DateTime Time { get; set; }
        public int Sequence { get; set; }
    }
}
































