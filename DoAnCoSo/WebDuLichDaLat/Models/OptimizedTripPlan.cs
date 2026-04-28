using System.Collections.Generic;

namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// Kế hoạch du lịch tối ưu được tạo ra từ thuật toán Knapsack
    /// </summary>
    public class OptimizedTripPlan
    {
        public List<TouristPlace> SelectedPlaces { get; set; } = new List<TouristPlace>();
        public List<Hotel> SelectedHotels { get; set; } = new List<Hotel>();
        public decimal TotalCost { get; set; }
        public decimal RemainingBudget { get; set; }
        public int Days { get; set; }
        public double TotalDistance { get; set; }
        public int TotalTimeMinutes { get; set; }
    }

    /// <summary>
    /// Thông tin đa chiều cho mỗi địa điểm trong thuật toán Knapsack
    /// </summary>
    public class PlaceInfo
    {
        public TouristPlace Place { get; set; }
        public double Score { get; set; } // v_i: Điểm hấp dẫn
        public decimal Cost { get; set; } // w_1i: Chi phí tiền
        public int DurationMinutes { get; set; } // w_2i: Thời gian (phút)

        public PlaceInfo(TouristPlace place, double score, decimal cost, int durationMinutes)
        {
            Place = place;
            Score = score;
            Cost = cost;
            DurationMinutes = durationMinutes;
        }
    }
}



















