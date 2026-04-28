using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Service chịu trách nhiệm tính toán các loại chi phí: Hotel, Food, Ticket, LocalTransport
    /// Tách logic tính toán chi phí ra khỏi Controller
    /// </summary>
    public class CostCalculationService
    {
        private readonly ApplicationDbContext _context;

        public CostCalculationService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Format currency
        /// </summary>
        public string FormatCurrency(decimal value) => $"{value:N0}đ";

        /// <summary>
        /// Tính chi phí khách sạn tối ưu
        /// </summary>
        public (decimal TotalCost, string Details, List<Hotel> SelectedHotels) CalculateOptimizedHotelCosts(
            List<string> selectedPlaceIds,
            decimal hotelBudget,
            int days,
            List<PlaceCluster> clusters = null) // Pass clusters từ bên ngoài để tránh dependency
        {
            int nights = days > 1 ? days - 1 : 0;
            if (nights == 0)
                return (0, "Chuyến đi trong ngày - không cần nghỉ đêm", new List<Hotel>());

            if (hotelBudget <= 0)
                return (0, "Không có ngân sách cho khách sạn", new List<Hotel>());

            var places = _context.TouristPlaces
                .AsNoTracking()
                .Where(p => selectedPlaceIds.Contains(p.Id))
                .ToList();

            if (!places.Any())
            {
                var details = new List<string>();
                var selectedHotels = new List<Hotel>();
                decimal totalCost = 0;
                decimal budgetPerNight = hotelBudget / nights;

                var candidates = _context.Hotels
                    .AsNoTracking()
                    .OrderBy(h => Math.Abs(h.PricePerNight - budgetPerNight))
                    .ThenBy(h => h.PricePerNight)
                    .Take(Math.Max(2, nights / 2))
                    .ToList();

                if (!candidates.Any())
                    return (0, "Không tìm thấy khách sạn phù hợp", new List<Hotel>());

                foreach (var hotel in candidates)
                {
                    int nightsForHotel = Math.Max(1, nights / candidates.Count);
                    decimal clusterCost = hotel.PricePerNight * nightsForHotel;
                    if (totalCost + clusterCost > hotelBudget)
                    {
                        nightsForHotel = (int)Math.Floor((hotelBudget - totalCost) / Math.Max(1, hotel.PricePerNight));
                        if (nightsForHotel <= 0) break;
                        clusterCost = hotel.PricePerNight * nightsForHotel;
                    }

                    selectedHotels.Add(hotel);
                    totalCost += clusterCost;
                    details.Add($"• {hotel.Name}: {nightsForHotel} đêm × {FormatCurrency(hotel.PricePerNight)}");

                    if (totalCost >= hotelBudget) break;
                }

                return (totalCost, string.Join("<br/>", details), selectedHotels);
            }

            // Nếu không có clusters được truyền vào, cần tính toán cơ bản
            if (clusters == null || !clusters.Any())
            {
                // Fallback: chọn khách sạn rẻ nhất
                var cheapestHotels = _context.Hotels
                    .AsNoTracking()
                    .OrderBy(h => h.PricePerNight)
                    .Take(Math.Max(1, nights))
                    .ToList();

                if (!cheapestHotels.Any())
                    return (0, "Không tìm thấy khách sạn", new List<Hotel>());

                var selectedHotels = new List<Hotel>();
                var details = new List<string>();
                decimal totalCost = 0;

                foreach (var hotel in cheapestHotels)
                {
                    int nightsForHotel = Math.Max(1, nights / cheapestHotels.Count);
                    decimal cost = hotel.PricePerNight * nightsForHotel;
                    if (totalCost + cost > hotelBudget)
                    {
                        nightsForHotel = (int)Math.Floor((hotelBudget - totalCost) / Math.Max(1, hotel.PricePerNight));
                        if (nightsForHotel <= 0) break;
                        cost = hotel.PricePerNight * nightsForHotel;
                    }

                    selectedHotels.Add(hotel);
                    totalCost += cost;
                    details.Add($"• {hotel.Name}: {nightsForHotel} đêm × {FormatCurrency(hotel.PricePerNight)}");

                    if (totalCost >= hotelBudget) break;
                }

                return (totalCost, string.Join("<br/>", details), selectedHotels);
            }

            var selectedHotels2 = new List<Hotel>();
            var details2 = new List<string>();
            decimal totalCost2 = 0;
            decimal budgetPerNight2 = hotelBudget / nights;

            foreach (var cluster in clusters)
            {
                int nightsForCluster = cluster.RecommendedNights;

                // Cluster cuối cùng nhận hết số đêm còn lại
                if (cluster == clusters.Last())
                {
                    int assignedNights = details2
                        .Select(line =>
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)\s+đêm");
                            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
                        })
                        .Sum();

                    nightsForCluster = nights - assignedNights;
                    if (nightsForCluster < 0) nightsForCluster = 0;
                }

                if (nightsForCluster <= 0) continue;

                decimal budgetForCluster = budgetPerNight2 * nightsForCluster;
                decimal maxPricePerNight = budgetForCluster / nightsForCluster;

                var hotel = FindBestHotelForCluster(cluster.Places, maxPricePerNight);

                if (hotel != null && !selectedHotels2.Contains(hotel))
                {
                    selectedHotels2.Add(hotel);
                    decimal clusterCost = hotel.PricePerNight * nightsForCluster;
                    totalCost2 += clusterCost;

                    string locationNames = GetLocationNamesDisplay(cluster.Places);

                    details2.Add($"• {hotel.Name} ({locationNames}): " +
                               $"{nightsForCluster} đêm × {FormatCurrency(hotel.PricePerNight)}");
                }
            }

            if (!selectedHotels2.Any())
            {
                var defaultHotel = _context.Hotels
                    .AsNoTracking()
                    .OrderBy(h => h.PricePerNight)
                    .FirstOrDefault();
                if (defaultHotel != null)
                {
                    selectedHotels2.Add(defaultHotel);
                    decimal defaultCost = Math.Min(defaultHotel.PricePerNight * nights, hotelBudget);
                    totalCost2 = defaultCost;
                    details2.Add($"• {defaultHotel.Name} (Mặc định): {nights} đêm × {FormatCurrency(defaultCost / nights)}");
                }
            }

            return (totalCost2, string.Join("<br/>", details2), selectedHotels2);
        }

        /// <summary>
        /// Tính chi phí vé tham quan
        /// </summary>
        public (decimal TotalCost, List<string> TicketDetails, List<string> Warnings) CalculateTicketCosts(
            List<string> selectedPlaceIds)
        {
            var details = new List<string>();
            var warnings = new List<string>();
            decimal cost = 0;

            if (!selectedPlaceIds.Any())
                return (cost, details, warnings);

            var places = _context.TouristPlaces
                .AsNoTracking()
                .Where(p => selectedPlaceIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToList();

            var attractionsByPlace = _context.Attractions
                .AsNoTracking()
                .Where(a => selectedPlaceIds.Contains(a.TouristPlaceId))
                .GroupBy(a => a.TouristPlaceId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var place in places)
            {
                if (!attractionsByPlace.TryGetValue(place.Id, out var attractions) || !attractions.Any())
                {
                    warnings.Add($"Không có thông tin vé cho địa điểm {place.Name}");
                    continue;
                }

                decimal sum = attractions.Sum(a => a.TicketPrice);
                cost += sum;
                details.Add($"• {place.Name}: Vé tham quan {FormatCurrency(sum)}");
            }

            if (cost == 0 && places.Any())
            {
                warnings.Add("Không tìm thấy thông tin vé tham quan cho các địa điểm đã chọn.");
            }

            return (cost, details, warnings);
        }

        // Helper methods
        private Hotel FindBestHotelForCluster(List<TouristPlace> places, decimal maxPricePerNight)
        {
            if (places == null || !places.Any())
                return null;

            // Lấy tất cả hotel IDs liên quan đến các địa điểm
            var placeIds = places.Select(p => p.Id).ToList();

            var hotels = _context.Hotels
                .AsNoTracking()
                .Where(h => placeIds.Contains(h.TouristPlaceId) || h.TouristPlaceId == null)
                .Where(h => h.PricePerNight <= maxPricePerNight)
                .OrderBy(h => Math.Abs(h.PricePerNight - maxPricePerNight))
                .ThenBy(h => h.PricePerNight)
                .FirstOrDefault();

            return hotels;
        }

        private string GetLocationNamesDisplay(List<TouristPlace> places)
        {
            if (places == null || !places.Any())
                return "";

            // TouristPlace giờ có tọa độ trực tiếp, không còn Location
            // Hiển thị tên địa điểm hoặc tên khu vực
            var locationNames = places
                .Select(p => p.Region?.Name ?? p.Name)
                .Distinct()
                .Take(3)
                .ToList();

            return string.Join(", ", locationNames);
        }
    }
}









