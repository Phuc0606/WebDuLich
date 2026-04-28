using System.Collections.Generic;
using WebDuLichDaLat.Controllers;

namespace WebDuLichDaLat.Models
{
    /// <summary>
    /// ViewModel chứa tất cả dữ liệu cần hiển thị cho một gợi ý du lịch
    /// </summary>
    public class SuggestionViewModel
    {
        // Thông tin cơ bản
        public string PlanName { get; set; }
        public BudgetPlanType? BudgetPlanType { get; set; }
        public int Days { get; set; }
        public int Nights => Days > 0 ? Days - 1 : 0;
        public bool BasicOnly { get; set; }

        // Thông tin phương tiện
        public TransportOption Transport { get; set; }
        public decimal TransportCost { get; set; }
        public bool IsTransportPriceCalculated { get; set; }
        public bool IsMergedLocation { get; set; }
        public string OldLocationName { get; set; }
        public string LocationName { get; set; }

        // Chi phí
        public decimal HotelCost { get; set; }
        public decimal FoodCost { get; set; }
        public decimal TicketCost { get; set; }
        public decimal LocalTransportCost { get; set; }
        public decimal MiscCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal Remaining { get; set; }

        // Chi tiết
        public string HotelDetails { get; set; }
        public string FoodDetails { get; set; }
        public List<string> TicketDetails { get; set; } = new List<string>();
        public List<string> LocalTransportDetails { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        // Lịch trình
        public string RouteDetails { get; set; }
        public string ClusterDetails { get; set; }
        public List<DailyItinerary> DailyItinerary { get; set; } = new List<DailyItinerary>();

        // Thống kê
        public int TotalPlaces { get; set; }
        public decimal TotalKm { get; set; }
        public List<TouristPlace> UniquePlaces { get; set; } = new List<TouristPlace>();
    }
}

