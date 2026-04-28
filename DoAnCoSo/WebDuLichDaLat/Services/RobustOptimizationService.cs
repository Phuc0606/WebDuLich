using WebDuLichDaLat.Models;
using System.Collections.Generic;
using System.Linq;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Robust Optimization Service
    /// Xử lý tính bất định trong thời gian di chuyển và hủy chuyến
    /// </summary>
    public class RobustOptimizationService
    {
        private readonly OsrmRouteService _osrmService;

        // Phân phối thời gian di chuyển: mean và std deviation (phút)
        private readonly Dictionary<string, (double mean, double std)> _timeDistributions;

        public RobustOptimizationService(OsrmRouteService osrmService)
        {
            _osrmService = osrmService;
            
            // Khởi tạo phân phối thời gian (có thể load từ database hoặc config)
            _timeDistributions = new Dictionary<string, (double, double)>
            {
                // Key: "distance_range", Value: (mean, std)
                { "short", (30, 10) },      // < 50km: mean 30 phút, std 10 phút
                { "medium", (120, 30) },    // 50-200km: mean 120 phút, std 30 phút
                { "long", (300, 60) }       // > 200km: mean 300 phút, std 60 phút
            };
        }

        /// <summary>
        /// Robust route optimization: tối ưu với worst-case scenario
        /// </summary>
        public async Task<RobustRouteResult> OptimizeRobustRouteAsync(
            Vehicle vehicle,
            List<Passenger> passengers,
            double confidenceLevel = 0.95) // 95% confidence
        {
            var result = new RobustRouteResult();

            // Tính worst-case travel times
            var worstCaseTimes = await CalculateWorstCaseTimesAsync(passengers, confidenceLevel);

            // Tối ưu route với worst-case times
            var optimizedRoute = await OptimizeWithUncertaintyAsync(
                vehicle, passengers, worstCaseTimes);

            result.Route = optimizedRoute.Route;
            result.ExpectedTime = optimizedRoute.ExpectedTime;
            result.WorstCaseTime = optimizedRoute.WorstCaseTime;
            result.ConfidenceLevel = confidenceLevel;

            return result;
        }

        /// <summary>
        /// Tính worst-case travel times với confidence level
        /// </summary>
        private async Task<Dictionary<string, double>> CalculateWorstCaseTimesAsync(
            List<Passenger> passengers,
            double confidenceLevel)
        {
            var worstCaseTimes = new Dictionary<string, double>();

            foreach (var passenger in passengers)
            {
                var distance = await _osrmService.GetDistanceKmAsync(
                    passenger.PickupLatitude, passenger.PickupLongitude,
                    passenger.DropoffLatitude, passenger.DropoffLongitude);

                if (!distance.HasValue)
                    continue;

                string range = GetDistanceRange(distance.Value);
                if (!_timeDistributions.ContainsKey(range))
                    continue;

                var (mean, std) = _timeDistributions[range];
                
                // Worst-case với confidence level (sử dụng z-score)
                // 95% confidence ≈ z = 1.96
                double zScore = GetZScore(confidenceLevel);
                double worstCaseTime = mean + zScore * std;

                string key = $"{passenger.Id}_pickup";
                worstCaseTimes[key] = worstCaseTime / 2; // Chia đôi cho pickup và dropoff
                
                key = $"{passenger.Id}_dropoff";
                worstCaseTimes[key] = worstCaseTime / 2;
            }

            return worstCaseTimes;
        }

        /// <summary>
        /// Tối ưu route với uncertainty
        /// </summary>
        private async Task<RobustRouteResult> OptimizeWithUncertaintyAsync(
            Vehicle vehicle,
            List<Passenger> passengers,
            Dictionary<string, double> worstCaseTimes)
        {
            // Sắp xếp passengers theo worst-case arrival time
            var sortedPassengers = passengers.OrderBy(p =>
            {
                string key = $"{p.Id}_pickup";
                return worstCaseTimes.ContainsKey(key) ? worstCaseTimes[key] : 0;
            }).ToList();

            var route = new List<RouteNode>();
            DateTime currentTime = vehicle.DepartureTime.AddMinutes(
                worstCaseTimes.Values.Sum() * 0.1); // Buffer time

            double totalExpectedTime = 0;
            double totalWorstCaseTime = 0;

            foreach (var passenger in sortedPassengers)
            {
                // Pickup
                string pickupKey = $"{passenger.Id}_pickup";
                double pickupTime = worstCaseTimes.ContainsKey(pickupKey) 
                    ? worstCaseTimes[pickupKey] 
                    : 30;

                route.Add(new RouteNode
                {
                    Type = "pickup",
                    PassengerId = passenger.Id,
                    Latitude = passenger.PickupLatitude,
                    Longitude = passenger.PickupLongitude,
                    Address = passenger.PickupAddress ?? "",
                    EarliestTime = currentTime,
                    LatestTime = currentTime.AddMinutes(pickupTime * 2)
                });

                currentTime = currentTime.AddMinutes(pickupTime);
                totalExpectedTime += pickupTime * 0.7; // Expected ≈ 70% của worst-case
                totalWorstCaseTime += pickupTime;

                // Dropoff
                string dropoffKey = $"{passenger.Id}_dropoff";
                double dropoffTime = worstCaseTimes.ContainsKey(dropoffKey)
                    ? worstCaseTimes[dropoffKey]
                    : 30;

                route.Add(new RouteNode
                {
                    Type = "dropoff",
                    PassengerId = passenger.Id,
                    Latitude = passenger.DropoffLatitude,
                    Longitude = passenger.DropoffLongitude,
                    Address = passenger.DropoffAddress ?? "",
                    EarliestTime = currentTime,
                    LatestTime = currentTime.AddMinutes(dropoffTime * 2)
                });

                currentTime = currentTime.AddMinutes(dropoffTime);
                totalExpectedTime += dropoffTime * 0.7;
                totalWorstCaseTime += dropoffTime;
            }

            return new RobustRouteResult
            {
                Route = route,
                ExpectedTime = totalExpectedTime,
                WorstCaseTime = totalWorstCaseTime
            };
        }

        /// <summary>
        /// Stochastic matching với cancellation probability
        /// </summary>
        public StochasticMatchingResult MatchWithCancellationRisk(
            List<PassengerRequest> passengerRequests,
            List<Vehicle> vehicles,
            Dictionary<int, double> cancellationProbabilities)
        {
            var result = new StochasticMatchingResult();

            // Tính expected revenue và cost cho mỗi matching
            var matchings = new List<StochasticAssignment>();

            foreach (var request in passengerRequests)
            {
                double cancelProb = cancellationProbabilities.ContainsKey(request.Id)
                    ? cancellationProbabilities[request.Id]
                    : 0.1; // Default 10%

                foreach (var vehicle in vehicles)
                {
                    if (vehicle.AvailableSeats < request.SeatsNeeded)
                        continue;

                    // Tính expected revenue
                    decimal revenue = CalculateExpectedRevenue(request, vehicle, cancelProb);
                    decimal cost = CalculateExpectedCost(vehicle, request);

                    matchings.Add(new StochasticAssignment
                    {
                        PassengerRequest = request,
                        Vehicle = vehicle,
                        ExpectedRevenue = revenue,
                        ExpectedCost = cost,
                        ExpectedProfit = revenue - cost,
                        CancellationProbability = cancelProb
                    });
                }
            }

            // Chọn matching tối ưu: maximize expected profit
            var bestMatchings = matchings
                .OrderByDescending(m => m.ExpectedProfit)
                .Take(vehicles.Count)
                .ToList();

            result.Assignments = bestMatchings;
            result.TotalExpectedRevenue = bestMatchings.Sum(m => m.ExpectedRevenue);
            result.TotalExpectedCost = bestMatchings.Sum(m => m.ExpectedCost);
            result.TotalExpectedProfit = result.TotalExpectedRevenue - result.TotalExpectedCost;

            return result;
        }

        /// <summary>
        /// Tính expected revenue với cancellation probability
        /// </summary>
        private decimal CalculateExpectedRevenue(
            PassengerRequest request,
            Vehicle vehicle,
            double cancellationProbability)
        {
            // Giả sử giá cố định
            decimal basePrice = 200000m; // Có thể tính từ request
            
            // Expected revenue = basePrice * (1 - cancellationProbability)
            return basePrice * (decimal)(1 - cancellationProbability);
        }

        /// <summary>
        /// Tính expected cost
        /// </summary>
        private decimal CalculateExpectedCost(Vehicle vehicle, PassengerRequest request)
        {
            // Chi phí cố định của xe
            return vehicle.FixedPrice ?? 1000000m;
        }

        /// <summary>
        /// Lấy distance range
        /// </summary>
        private string GetDistanceRange(double distanceKm)
        {
            if (distanceKm < 50)
                return "short";
            if (distanceKm < 200)
                return "medium";
            return "long";
        }

        /// <summary>
        /// Lấy z-score cho confidence level
        /// </summary>
        private double GetZScore(double confidenceLevel)
        {
            // Z-scores cho các confidence levels phổ biến
            return confidenceLevel switch
            {
                0.90 => 1.645,
                0.95 => 1.96,
                0.99 => 2.576,
                _ => 1.96 // Default 95%
            };
        }
    }

    public class RobustRouteResult
    {
        public List<RouteNode> Route { get; set; } = new();
        public double ExpectedTime { get; set; }
        public double WorstCaseTime { get; set; }
        public double ConfidenceLevel { get; set; }
    }

    public class StochasticMatchingResult
    {
        public List<StochasticAssignment> Assignments { get; set; } = new();
        public decimal TotalExpectedRevenue { get; set; }
        public decimal TotalExpectedCost { get; set; }
        public decimal TotalExpectedProfit { get; set; }
    }

    public class StochasticAssignment
    {
        public PassengerRequest PassengerRequest { get; set; } = null!;
        public Vehicle Vehicle { get; set; } = null!;
        public decimal ExpectedRevenue { get; set; }
        public decimal ExpectedCost { get; set; }
        public decimal ExpectedProfit { get; set; }
        public double CancellationProbability { get; set; }
    }
}


