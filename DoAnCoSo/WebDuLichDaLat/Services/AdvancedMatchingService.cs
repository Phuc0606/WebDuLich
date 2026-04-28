using WebDuLichDaLat.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Advanced Matching Service - Wrapper tích hợp tất cả các thuật toán nâng cao
    /// Service này kết hợp:
    /// 1. Online Bipartite Matching với Batch Window
    /// 2. Dynamic VRP với Genetic Algorithm
    /// 3. Cost Sharing với Shapley Value
    /// 4. Split Delivery VRP cho nhóm lớn
    /// 5. Robust Optimization cho stochastic times
    /// </summary>
    public class AdvancedMatchingService
    {
        private readonly OnlineBipartiteMatchingService _bipartiteMatching;
        private readonly DynamicVRPService _dynamicVRP;
        private readonly CostSharingService _costSharing;
        private readonly SplitDeliveryVRPService _splitDelivery;
        private readonly RobustOptimizationService _robustOptimization;
        private readonly RouteMatchingService _routeMatching;
        private readonly ApplicationDbContext _context;

        public AdvancedMatchingService(
            OnlineBipartiteMatchingService bipartiteMatching,
            DynamicVRPService dynamicVRP,
            CostSharingService costSharing,
            SplitDeliveryVRPService splitDelivery,
            RobustOptimizationService robustOptimization,
            RouteMatchingService routeMatching,
            ApplicationDbContext context)
        {
            _bipartiteMatching = bipartiteMatching;
            _dynamicVRP = dynamicVRP;
            _costSharing = costSharing;
            _splitDelivery = splitDelivery;
            _robustOptimization = robustOptimization;
            _routeMatching = routeMatching;
            _context = context;
        }

        /// <summary>
        /// Matching nâng cao với tất cả các thuật toán
        /// </summary>
        public async Task<AdvancedMatchingResult> MatchAdvancedAsync(
            CarpoolRequest request,
            bool useBatchMatching = true,
            bool useDynamicRouting = true,
            bool useCostSharing = true,
            bool useRobustOptimization = false)
        {
            var result = new AdvancedMatchingResult();

            // 1. Lấy danh sách xe có sẵn
            var windowStart = request.DepartureTime.AddHours(-1);
            var windowEnd = request.DepartureTime.AddHours(1);

            var availableVehicles = await _context.Vehicles
                .Where(v => v.IsActive &&
                           v.DepartureTime >= windowStart &&
                           v.DepartureTime <= windowEnd &&
                           v.VehicleType == "Ghép xe tự động")
                .ToListAsync();

            // 2. Xử lý nhóm lớn (>16 người) với Split Delivery VRP
            if (request.SeatsNeeded > 16)
            {
                var splitResult = _splitDelivery.OptimizeGroupSplit(
                    request.SeatsNeeded,
                    availableVehicles);

                result.SplitDeliveryResult = splitResult;
                // Xử lý từng phần của nhóm lớn
                // (Có thể gọi đệ quy hoặc xử lý tuần tự)
            }

            // 3. Batch Matching với Online Bipartite Matching
            if (useBatchMatching && availableVehicles.Any())
            {
                var passengerRequest = new PassengerRequest
                {
                    Id = request.Id,
                    PickupLat = request.PickupLatitude,
                    PickupLon = request.PickupLongitude,
                    DropoffLat = request.DropoffLatitude,
                    DropoffLon = request.DropoffLongitude,
                    SeatsNeeded = request.SeatsNeeded,
                    DepartureTime = request.DepartureTime,
                    PickupAddress = request.PickupAddress,
                    DropoffAddress = request.DropoffAddress
                };

                var batchResult = await _bipartiteMatching.MatchBatchAsync(
                    new List<PassengerRequest> { passengerRequest },
                    availableVehicles);

                if (batchResult.Assignments.Any())
                {
                    var assignment = batchResult.Assignments.First();
                    result.MatchedVehicle = assignment.Vehicle;
                    result.MatchingCost = assignment.Cost;
                }
            }

            // 4. Dynamic Routing nếu có xe được match
            if (useDynamicRouting && result.MatchedVehicle != null)
            {
                var existingPassengers = await _context.Passengers
                    .Where(p => p.MatchedVehicleId == result.MatchedVehicle.Id && p.IsMatched)
                    .ToListAsync();

                var newPassenger = new Passenger
                {
                    Id = request.Id,
                    PickupLatitude = request.PickupLatitude,
                    PickupLongitude = request.PickupLongitude,
                    DropoffLatitude = request.DropoffLatitude,
                    DropoffLongitude = request.DropoffLongitude,
                    PreferredDepartureTime = request.DepartureTime,
                    PickupAddress = request.PickupAddress,
                    DropoffAddress = request.DropoffAddress
                };

                var dynamicRoute = await _dynamicVRP.OptimizeDynamicRouteAsync(
                    result.MatchedVehicle,
                    existingPassengers,
                    newPassenger);

                result.DynamicRouteResult = dynamicRoute;
            }

            // 5. Cost Sharing với Shapley Value
            if (useCostSharing && result.MatchedVehicle != null)
            {
                var allPassengers = await _context.Passengers
                    .Where(p => p.MatchedVehicleId == result.MatchedVehicle.Id && p.IsMatched)
                    .ToListAsync();

                // Thêm passenger mới vào danh sách
                var newPassenger = new Passenger
                {
                    Id = request.Id,
                    PickupLatitude = request.PickupLatitude,
                    PickupLongitude = request.PickupLongitude,
                    DropoffLatitude = request.DropoffLatitude,
                    DropoffLongitude = request.DropoffLongitude
                };
                allPassengers.Add(newPassenger);

                // Tính Shapley Value với cost function
                var shapleyValues = await CalculateShapleyValueAsync(
                    result.MatchedVehicle,
                    allPassengers);

                result.CostSharing = shapleyValues;
            }

            // 6. Robust Optimization (optional)
            if (useRobustOptimization && result.MatchedVehicle != null)
            {
                var allPassengers = await _context.Passengers
                    .Where(p => p.MatchedVehicleId == result.MatchedVehicle.Id && p.IsMatched)
                    .ToListAsync();

                var robustRoute = await _robustOptimization.OptimizeRobustRouteAsync(
                    result.MatchedVehicle,
                    allPassengers);

                result.RobustRouteResult = robustRoute;
            }

            return result;
        }

        /// <summary>
        /// Helper method để tính Shapley Value với async cost function
        /// </summary>
        private async Task<Dictionary<int, decimal>> CalculateShapleyValueAsync(
            Vehicle vehicle,
            List<Passenger> passengers)
        {
            // Tạo cost function wrapper
            Func<List<Passenger>, decimal> costFunction = (passengers) =>
            {
                // Synchronous wrapper - có thể cần cache kết quả
                return _costSharing.CalculateRouteCostAsync(passengers, vehicle).Result;
            };

            return _costSharing.CalculateShapleyValue(vehicle, passengers, costFunction);
        }
    }

    /// <summary>
    /// Request từ người dùng
    /// </summary>
    public class CarpoolRequest
    {
        public int Id { get; set; }
        public double PickupLatitude { get; set; }
        public double PickupLongitude { get; set; }
        public double DropoffLatitude { get; set; }
        public double DropoffLongitude { get; set; }
        public int SeatsNeeded { get; set; }
        public DateTime DepartureTime { get; set; }
        public string? PickupAddress { get; set; }
        public string? DropoffAddress { get; set; }
    }

    /// <summary>
    /// Kết quả matching nâng cao
    /// </summary>
    public class AdvancedMatchingResult
    {
        public Vehicle? MatchedVehicle { get; set; }
        public double MatchingCost { get; set; }
        public DynamicRouteResult? DynamicRouteResult { get; set; }
        public Dictionary<int, decimal>? CostSharing { get; set; }
        public SplitDeliveryResult? SplitDeliveryResult { get; set; }
        public RobustRouteResult? RobustRouteResult { get; set; }
    }
}

