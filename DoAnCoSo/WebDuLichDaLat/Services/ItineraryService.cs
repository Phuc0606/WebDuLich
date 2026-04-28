using System;
using System.Collections.Generic;
using System.Linq;
using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Service chịu trách nhiệm tạo lịch trình: Clustering, TSP (Traveling Salesman Problem), sắp xếp lịch trình
    /// Tách logic lịch trình ra khỏi Controller
    /// </summary>
    public class ItineraryService
    {
        private readonly RecommendationService _recommendationService;

        public ItineraryService(RecommendationService recommendationService)
        {
            _recommendationService = recommendationService;
        }

        /// <summary>
        /// Cluster địa điểm theo khoảng cách để tạo các nhóm gần nhau
        /// </summary>
        public List<PlaceCluster> ClusterPlacesByDistance(List<TouristPlace> places, int totalDays)
        {
            if (!places.Any()) return new List<PlaceCluster>();

            var clusters = new List<PlaceCluster>();
            var remaining = new List<TouristPlace>(places);

            // Tính số cluster dựa trên số ngày và số địa điểm
            int recommendedClusters;

            if (places.Count == 1)
            {
                recommendedClusters = 1;
            }
            else if (totalDays <= 2)
            {
                recommendedClusters = Math.Min(1, places.Count);
            }
            else if (totalDays <= 4)
            {
                recommendedClusters = Math.Min(2, places.Count);
            }
            else
            {
                recommendedClusters = Math.Min(Math.Max(2, totalDays / 2), places.Count);
            }

            // Force tạo nhiều clusters nếu có nhiều địa điểm
            if (places.Count >= 5 && totalDays >= 4)
            {
                recommendedClusters = Math.Min(3, places.Count);
            }

            int placesPerCluster = (int)Math.Ceiling((double)places.Count / recommendedClusters);

            // Tạo clusters
            for (int c = 0; c < recommendedClusters && remaining.Any(); c++)
            {
                var cluster = new PlaceCluster { Places = new List<TouristPlace>() };

                var centerPlace = remaining.First();
                cluster.Places.Add(centerPlace);
                remaining.Remove(centerPlace);

                int targetPlacesForCluster = remaining.Count < recommendedClusters - c - 1
                    ? 1
                    : placesPerCluster - 1;

                var nearbyPlaces = remaining
                    .Where(p => _recommendationService.GetDistance(centerPlace.Latitude, centerPlace.Longitude,
                                p.Latitude, p.Longitude) <= 20)
                    .OrderBy(p => _recommendationService.GetDistance(centerPlace.Latitude, centerPlace.Longitude,
                                p.Latitude, p.Longitude))
                    .Take(targetPlacesForCluster)
                    .ToList();

                if (!nearbyPlaces.Any() && remaining.Any())
                {
                    nearbyPlaces = remaining.Take(targetPlacesForCluster).ToList();
                }

                cluster.Places.AddRange(nearbyPlaces);
                remaining.RemoveAll(p => nearbyPlaces.Contains(p));
                clusters.Add(cluster);
            }

            // Phân phối địa điểm còn lại
            int clusterIndex = 0;
            while (remaining.Any())
            {
                var place = remaining.First();
                clusters[clusterIndex % clusters.Count].Places.Add(place);
                remaining.Remove(place);
                clusterIndex++;
            }

            // Phân bổ số đêm hợp lý
            int totalNights = Math.Max(0, totalDays - 1);

            if (clusters.Count == 1)
            {
                clusters[0].RecommendedNights = totalNights;
            }
            else if (clusters.Count > totalNights)
            {
                for (int i = 0; i < clusters.Count; i++)
                {
                    clusters[i].RecommendedNights = (i < totalNights) ? 1 : 0;
                }
            }
            else
            {
                // Phân bổ tối thiểu 1 đêm cho mỗi cluster
                int nightsRemaining = totalNights;
                for (int i = 0; i < clusters.Count; i++)
                {
                    clusters[i].RecommendedNights = 1;
                    nightsRemaining--;
                }

                // Phân phối số đêm dư đều hơn
                if (nightsRemaining > 0)
                {
                    int avgNightsPerCluster = totalNights / clusters.Count;
                    int maxNightsPerCluster = Math.Max(avgNightsPerCluster + 1, 2);

                    clusterIndex = 0;
                    while (nightsRemaining > 0)
                    {
                        if (clusters[clusterIndex].RecommendedNights < maxNightsPerCluster)
                        {
                            clusters[clusterIndex].RecommendedNights++;
                            nightsRemaining--;
                        }

                        clusterIndex = (clusterIndex + 1) % clusters.Count;

                        if (clusters.All(c => c.RecommendedNights >= maxNightsPerCluster))
                        {
                            break;
                        }
                    }

                    // Phân bổ đêm dư còn lại
                    if (nightsRemaining > 0)
                    {
                        var clustersBelowMax = clusters
                            .Select((c, idx) => new { Cluster = c, Index = idx })
                            .Where(x => x.Cluster.RecommendedNights < maxNightsPerCluster)
                            .OrderBy(x => x.Cluster.RecommendedNights)
                            .ToList();

                        foreach (var item in clustersBelowMax)
                        {
                            if (nightsRemaining <= 0) break;
                            item.Cluster.RecommendedNights++;
                            nightsRemaining--;
                        }
                    }
                }

                // Validation: Đảm bảo tổng = totalNights
                int finalSum = clusters.Sum(c => c.RecommendedNights);
                if (finalSum != totalNights)
                {
                    int diff = totalNights - finalSum;
                    var lastCluster = clusters.Last();
                    lastCluster.RecommendedNights = Math.Max(1, lastCluster.RecommendedNights + diff);
                }
            }

            return clusters;
        }

        /// <summary>
        /// Tối ưu lộ trình sử dụng Nearest Neighbor + 2-Opt
        /// </summary>
        public List<TouristPlace> OptimizeRoute(double startLat, double startLng, List<TouristPlace> places)
        {
            if (places == null || !places.Any())
                return new List<TouristPlace>();

            // Bước 1: Nearest Neighbor
            var route = new List<TouristPlace>();
            var remaining = new List<TouristPlace>(places);

            double currentLat = startLat;
            double currentLng = startLng;

            while (remaining.Any())
            {
                var next = remaining
                    .OrderBy(p => _recommendationService.GetDistance(currentLat, currentLng, p.Latitude, p.Longitude))
                    .First();

                route.Add(next);
                remaining.Remove(next);

                currentLat = next.Latitude;
                currentLng = next.Longitude;
            }

            // Bước 2: Áp dụng 2-Opt để tối ưu
            if (route.Count > 2)
            {
                route = OptimizeRoute2Opt(startLat, startLng, route);
            }

            return route;
        }

        /// <summary>
        /// Tối ưu lộ trình bằng thuật toán 2-Opt
        /// </summary>
        public List<TouristPlace> OptimizeRoute2Opt(
            double startLat,
            double startLng,
            List<TouristPlace> route)
        {
            if (route == null || route.Count <= 2)
                return route;

            var optimizedRoute = new List<TouristPlace>(route);
            bool improved = true;
            int maxIterations = 100;
            int iterations = 0;

            while (improved && iterations < maxIterations)
            {
                improved = false;
                iterations++;

                for (int i = 1; i < optimizedRoute.Count - 2; i++)
                {
                    for (int k = i + 1; k < optimizedRoute.Count - 1; k++)
                    {
                        double oldDist = _recommendationService.GetDistance(
                            optimizedRoute[i - 1].Latitude, optimizedRoute[i - 1].Longitude,
                            optimizedRoute[i].Latitude, optimizedRoute[i].Longitude
                        ) + _recommendationService.GetDistance(
                            optimizedRoute[k].Latitude, optimizedRoute[k].Longitude,
                            optimizedRoute[k + 1].Latitude, optimizedRoute[k + 1].Longitude
                        );

                        double newDist = _recommendationService.GetDistance(
                            optimizedRoute[i - 1].Latitude, optimizedRoute[i - 1].Longitude,
                            optimizedRoute[k].Latitude, optimizedRoute[k].Longitude
                        ) + _recommendationService.GetDistance(
                            optimizedRoute[i].Latitude, optimizedRoute[i].Longitude,
                            optimizedRoute[k + 1].Latitude, optimizedRoute[k + 1].Longitude
                        );

                        if (newDist < oldDist)
                        {
                            optimizedRoute.Reverse(i, k - i + 1);
                            improved = true;
                            break;
                        }
                    }
                    if (improved) break;
                }
            }

            return optimizedRoute;
        }

        /// <summary>
        /// Phân bổ địa điểm vào các ngày
        /// </summary>
        public List<List<TouristPlace>> DistributePlacesAcrossDays(List<TouristPlace> places, int days)
        {
            var result = new List<List<TouristPlace>>();

            for (int i = 0; i < days; i++)
            {
                result.Add(new List<TouristPlace>());
            }

            if (!places.Any())
                return result;

            if (places.Count == days)
            {
                // Mỗi ngày 1 địa điểm
                for (int i = 0; i < places.Count; i++)
                {
                    result[i].Add(places[i]);
                }
            }
            else if (places.Count < days)
            {
                // Ít địa điểm hơn số ngày: Mỗi địa điểm 1 lần, các ngày còn lại nghỉ
                for (int i = 0; i < places.Count && i < days; i++)
                {
                    result[i].Add(places[i]);
                }
            }
            else
            {
                // Nhiều địa điểm hơn số ngày: Phân bổ đều
                int placesPerDay = (int)Math.Ceiling((double)places.Count / days);
                int placeIndex = 0;

                for (int day = 0; day < days && placeIndex < places.Count; day++)
                {
                    int placesForThisDay = Math.Min(placesPerDay, places.Count - placeIndex);
                    for (int j = 0; j < placesForThisDay && placeIndex < places.Count; j++)
                    {
                        result[day].Add(places[placeIndex]);
                        placeIndex++;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Tạo lịch trình hàng ngày từ các địa điểm, khách sạn và clusters
        /// </summary>
        public (List<DailyItinerary> Itinerary, List<string> Warnings) BuildDailyItinerary(
            List<TouristPlace> selectedPlaces,
            int days,
            List<Hotel> selectedHotels,
            List<PlaceCluster> clusters)
        {
            var itinerary = new List<DailyItinerary>();

            int currentDay = 0;

            for (int clusterIdx = 0; clusterIdx < clusters.Count; clusterIdx++)
            {
                var cluster = clusters[clusterIdx];
                int daysInCluster;

                if (clusterIdx == clusters.Count - 1)
                {
                    daysInCluster = days - currentDay;
                    if (daysInCluster < 1) daysInCluster = 1;
                }
                else
                {
                    daysInCluster = cluster.RecommendedNights + 1;
                }

                var clusterPlaces = cluster.Places;
                var dailyPlacesInCluster = DistributePlacesAcrossDays(clusterPlaces, daysInCluster);

                var hotel = selectedHotels.ElementAtOrDefault(clusterIdx);

                for (int d = 0; d < daysInCluster && currentDay < days; d++)
                {
                    itinerary.Add(new DailyItinerary
                    {
                        DayNumber = currentDay + 1,
                        Places = dailyPlacesInCluster[d],
                        Hotel = hotel,
                        ClusterIndex = clusterIdx
                    });
                    currentDay++;
                }
            }

            // Kiểm tra và điều chỉnh khoảng cách
            var (optimizedItinerary, warnings) = OptimizeDailyItineraryDistance(itinerary);

            return (optimizedItinerary, warnings);
        }

        /// <summary>
        /// Kiểm tra và tự động điều chỉnh khoảng cách giữa các địa điểm trong cùng một ngày
        /// </summary>
        private (List<DailyItinerary> OptimizedItinerary, List<string> Warnings) OptimizeDailyItineraryDistance(
            List<DailyItinerary> itinerary)
        {
            var warnings = new List<string>();
            var optimizedItinerary = new List<DailyItinerary>();
            const double MAX_DISTANCE_PER_DAY = 30.0;

            var placesToRedistribute = new List<TouristPlace>();

            foreach (var dayPlan in itinerary)
            {
                var dayPlaces = dayPlan.Places.ToList();

                if (dayPlaces.Count <= 1)
                {
                    optimizedItinerary.Add(dayPlan);
                    continue;
                }

                var validPlaces = new List<TouristPlace>();
                var placesToMove = new List<TouristPlace>();

                validPlaces.Add(dayPlaces[0]);

                for (int i = 1; i < dayPlaces.Count; i++)
                {
                    var currentPlace = dayPlaces[i];
                    double maxDistanceToValid = validPlaces
                        .Select(p => _recommendationService.GetDistance(
                            p.Latitude, p.Longitude,
                            currentPlace.Latitude, currentPlace.Longitude))
                        .Max();

                    if (maxDistanceToValid <= MAX_DISTANCE_PER_DAY)
                    {
                        validPlaces.Add(currentPlace);
                    }
                    else
                    {
                        placesToMove.Add(currentPlace);
                        warnings.Add($"⚠️ Địa điểm '{currentPlace.Name}' cách các địa điểm khác trong ngày quá xa (>30km), sẽ được di chuyển sang ngày khác.");
                    }
                }

                // Cập nhật lịch trình với các địa điểm hợp lệ
                var updatedDayPlan = new DailyItinerary
                {
                    DayNumber = dayPlan.DayNumber,
                    Places = validPlaces,
                    Hotel = dayPlan.Hotel,
                    ClusterIndex = dayPlan.ClusterIndex
                };
                optimizedItinerary.Add(updatedDayPlan);

                placesToRedistribute.AddRange(placesToMove);
            }

            // Phân bổ lại các địa điểm còn lại vào các ngày tiếp theo
            if (placesToRedistribute.Any())
            {
                int dayIndex = 0;
                foreach (var place in placesToRedistribute)
                {
                    if (dayIndex < optimizedItinerary.Count)
                    {
                        optimizedItinerary[dayIndex].Places.Add(place);
                        dayIndex = (dayIndex + 1) % optimizedItinerary.Count;
                    }
                }
            }

            return (optimizedItinerary, warnings);
        }

        /// <summary>
        /// Tạo route details từ daily itinerary
        /// </summary>
        public string GenerateRouteDetailsFromItinerary(List<DailyItinerary> dailyItinerary)
        {
            if (dailyItinerary == null || !dailyItinerary.Any())
                return string.Empty;

            var seenPlaces = new HashSet<string>();
            var route = new List<TouristPlace>();

            foreach (var day in dailyItinerary.OrderBy(d => d.DayNumber))
            {
                foreach (var place in day.Places)
                {
                    if (!seenPlaces.Contains(place.Id))
                    {
                        route.Add(place);
                        seenPlaces.Add(place.Id);
                    }
                }
            }

            if (!route.Any())
                return string.Empty;

            return string.Join("<br/>", route.Select((p, idx) => $"{idx + 1}. {p.Name}"));
        }
    }
}



















