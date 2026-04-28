using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Service chịu trách nhiệm các thuật toán gợi ý: Recall, Rank, và Knapsack
    /// Tách logic gợi ý ra khỏi Controller
    /// </summary>
    public class RecommendationService
    {
        private readonly ApplicationDbContext _context;
        private readonly CostCalculationService _costCalculationService;

        public RecommendationService(
            ApplicationDbContext context,
            CostCalculationService costCalculationService)
        {
            _context = context;
            _costCalculationService = costCalculationService;
        }

        /// <summary>
        /// Helper: Tính khoảng cách Haversine
        /// </summary>
        public double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth radius in km
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double angle) => angle * Math.PI / 180;

        /// <summary>
        /// Recall: Lọc địa điểm phù hợp theo tiêu chí
        /// </summary>
        public List<TouristPlace> RecallPlaces(
            decimal budget,
            int days,
            int? categoryId = null,
            double? startLat = null,
            double? startLng = null,
            double maxDistanceKm = 50,
            int minRating = 3)
        {
            var allPlaces = _context.TouristPlaces
                .AsNoTracking()
                .AsQueryable();

            if (categoryId.HasValue)
            {
                allPlaces = allPlaces.Where(p => p.CategoryId == categoryId.Value);
            }

            allPlaces = allPlaces.Where(p => p.Rating >= minRating);

            var places = allPlaces.ToList();

            // Sắp xếp theo khoảng cách nếu có tọa độ
            if (startLat.HasValue && startLng.HasValue)
            {
                places = places
                    .Select(p => new
                    {
                        Place = p,
                        Distance = GetDistance(startLat.Value, startLng.Value,
                                              p.Latitude, p.Longitude)
                    })
                    .Where(x => x.Distance <= maxDistanceKm * 2)
                    .OrderBy(x => x.Distance)
                    .Select(x => x.Place)
                    .ToList();
            }

            // Lọc theo ngân sách
            var budgetFilteredPlaces = new List<TouristPlace>();
            decimal budgetPerPlace = budget / (days * 2);

            foreach (var place in places)
            {
                decimal estimatedCost = EstimatePlaceCost(place, days);

                if (estimatedCost <= budgetPerPlace * 1.5m)
                {
                    budgetFilteredPlaces.Add(place);
                }
            }

            // Fallback: Trả về top địa điểm phổ biến nếu không có địa điểm phù hợp ngân sách
            if (!budgetFilteredPlaces.Any() && places.Any())
            {
                budgetFilteredPlaces = places
                    .OrderByDescending(p => p.Rating)
                    .Take(Math.Max(10, days * 3))
                    .ToList();
            }

            return budgetFilteredPlaces;
        }

        /// <summary>
        /// Ước tính chi phí cho một địa điểm
        /// </summary>
        public decimal EstimatePlaceCost(TouristPlace place, int days)
        {
            decimal cost = 0;

            var ticketCost = _context.Attractions
                .AsNoTracking()
                .Where(a => a.TouristPlaceId == place.Id)
                .Sum(a => (decimal?)a.TicketPrice) ?? 0;
            cost += ticketCost;

            var avgPrice = _context.Restaurants
                .AsNoTracking()
                .Where(r => r.TouristPlaceId == place.Id)
                .Average(r => (decimal?)r.AveragePricePerPerson);

            if (avgPrice.HasValue)
            {
                cost += avgPrice.Value;
            }
            else
            {
                cost += 100000; // Ước tính mặc định
            }

            return cost;
        }

        /// <summary>
        /// Rank: Xếp hạng mức độ hấp dẫn của địa điểm
        /// </summary>
        public List<TouristPlace> RankPlaces(
            List<TouristPlace> places,
            int? preferredCategoryId = null,
            double? startLat = null,
            double? startLng = null)
        {
            if (!places.Any()) return new List<TouristPlace>();

            var placeIds = places.Select(p => p.Id).ToList();

            // Load tất cả dữ liệu một lần để tránh N+1 queries
            var reviewCounts = _context.Reviews
                .AsNoTracking()
                .Where(r => placeIds.Contains(r.TouristPlaceId))
                .GroupBy(r => r.TouristPlaceId)
                .ToDictionary(g => g.Key, g => g.Count());

            var hotelCounts = _context.Hotels
                .AsNoTracking()
                .Where(h => placeIds.Contains(h.TouristPlaceId))
                .GroupBy(h => h.TouristPlaceId)
                .ToDictionary(g => g.Key, g => g.Count());

            var restaurantCounts = _context.Restaurants
                .AsNoTracking()
                .Where(r => placeIds.Contains(r.TouristPlaceId))
                .GroupBy(r => r.TouristPlaceId)
                .ToDictionary(g => g.Key, g => g.Count());

            var rankedPlaces = places.Select(place =>
            {
                double score = 0;

                // 1. Đánh giá (Rating): 0-40 điểm
                score += (place.Rating / 5.0) * 40;

                // 2. Độ phổ biến: 0-30 điểm
                int reviewCount = reviewCounts.GetValueOrDefault(place.Id, 0);
                int hotelCount = hotelCounts.GetValueOrDefault(place.Id, 0);
                int restaurantCount = restaurantCounts.GetValueOrDefault(place.Id, 0);
                int popularityScore = reviewCount * 2 + hotelCount + restaurantCount;
                score += Math.Min(popularityScore / 10.0, 30);

                // 3. Sở thích người dùng (category match): 0-20 điểm
                if (preferredCategoryId.HasValue && place.CategoryId == preferredCategoryId.Value)
                {
                    score += 20;
                }
                else if (preferredCategoryId.HasValue)
                {
                    score += 5;
                }
                else
                {
                    score += 10;
                }

                // 4. Khoảng cách: 0-10 điểm
                if (startLat.HasValue && startLng.HasValue)
                {
                    double distance = GetDistance(startLat.Value, startLng.Value, place.Latitude, place.Longitude);
                    if (distance <= 5)
                        score += 10;
                    else if (distance <= 15)
                        score += 7;
                    else if (distance <= 30)
                        score += 4;
                    else
                        score += 1;
                }
                else
                {
                    score += 5;
                }

                return new
                {
                    Place = place,
                    Score = score
                };
            })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Place)
            .ToList();

            return rankedPlaces;
        }

        /// <summary>
        /// Lấy danh sách địa điểm "Must-go"
        /// </summary>
        public List<TouristPlace> GetMustGoPlaces(
            int count,
            int? categoryId = null,
            double? startLat = null,
            double? startLng = null,
            List<string> excludePlaceIds = null)
        {
            var query = _context.TouristPlaces
                .AsNoTracking()
                .AsQueryable();

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (excludePlaceIds != null && excludePlaceIds.Any())
            {
                query = query.Where(p => !excludePlaceIds.Contains(p.Id));
            }

            var places = query.ToList();

            var placeIds = places.Select(p => p.Id).ToList();

            var reviewCounts = _context.Reviews
                .AsNoTracking()
                .Where(r => placeIds.Contains(r.TouristPlaceId))
                .GroupBy(r => r.TouristPlaceId)
                .ToDictionary(g => g.Key, g => g.Count());

            var hotelCounts = _context.Hotels
                .AsNoTracking()
                .Where(h => placeIds.Contains(h.TouristPlaceId))
                .GroupBy(h => h.TouristPlaceId)
                .ToDictionary(g => g.Key, g => g.Count());

            var restaurantCounts = _context.Restaurants
                .AsNoTracking()
                .Where(r => placeIds.Contains(r.TouristPlaceId))
                .GroupBy(r => r.TouristPlaceId)
                .ToDictionary(g => g.Key, g => g.Count());

            var rankedPlaces = places.Select(place =>
            {
                double score = 0;

                score += (place.Rating / 5.0) * 50;

                int reviewCount = reviewCounts.GetValueOrDefault(place.Id, 0);
                int hotelCount = hotelCounts.GetValueOrDefault(place.Id, 0);
                int restaurantCount = restaurantCounts.GetValueOrDefault(place.Id, 0);
                int popularityScore = reviewCount * 2 + hotelCount + restaurantCount;
                score += Math.Min(popularityScore / 10.0, 50);

                return new
                {
                    Place = place,
                    Score = score
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Place.Rating)
            .Select(x => x.Place)
            .Take(count)
            .ToList();

            return rankedPlaces;
        }

        /// <summary>
        /// Helper: Kiểm tra 2 danh sách địa điểm có giống nhau không
        /// </summary>
        public bool AreSamePlaces(List<TouristPlace> list1, List<TouristPlace> list2)
        {
            if (list1.Count != list2.Count) return false;
            var ids1 = list1.Select(p => p.Id).OrderBy(id => id).ToList();
            var ids2 = list2.Select(p => p.Id).OrderBy(id => id).ToList();
            return ids1.SequenceEqual(ids2);
        }
    }
}

