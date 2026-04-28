namespace WebDuLichDaLat.Models
{
    public class PlaceCluster
    {
        public List<TouristPlace> Places { get; set; } = new List<TouristPlace>();
        public int RecommendedNights { get; set; }
    }

    // ✅ GIẢI PHÁP: Tạo 1 DailyItinerary duy nhất, dùng chung cho tất cả
    public class DailyItinerary
    {
        public int DayNumber { get; set; }
        public List<TouristPlace> Places { get; set; } = new List<TouristPlace>();
        public Hotel Hotel { get; set; }
        public int ClusterIndex { get; set; } // Thuộc cluster nào
    }

}