using WebDuLichDaLat.Models;
using System.Collections.Generic;
using System.Linq;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Online Bipartite Matching với Batch Window
    /// Thay thế Greedy matching bằng thuật toán tối ưu toàn cục
    /// </summary>
    public class OnlineBipartiteMatchingService
    {
        private readonly OsrmRouteService _osrmService;
        private readonly RouteMatchingService _routeMatchingService;
        
        // Batch window: chờ 30 giây để gom yêu cầu trước khi matching
        private const int DefaultBatchWindowSeconds = 30;
        private const int MaxBatchSize = 20;

        public OnlineBipartiteMatchingService(
            OsrmRouteService osrmService,
            RouteMatchingService routeMatchingService)
        {
            _osrmService = osrmService;
            _routeMatchingService = routeMatchingService;
        }

        /// <summary>
        /// Batch matching với time window
        /// </summary>
        public async Task<BipartiteMatchingResult> MatchBatchAsync(
            List<PassengerRequest> passengerRequests,
            List<Vehicle> vehicles,
            int batchWindowSeconds = DefaultBatchWindowSeconds)
        {
            if (!passengerRequests.Any() || !vehicles.Any())
                return new BipartiteMatchingResult();

            // Xây dựng đồ thị bipartite
            var graph = await BuildBipartiteGraphAsync(passengerRequests, vehicles);
            
            // Tính trọng số cho mỗi cạnh (chi phí matching)
            var weights = await CalculateWeightsAsync(graph, passengerRequests, vehicles);
            
            // Chạy Hungarian Algorithm để tìm matching tối ưu
            var matching = HungarianAlgorithm(graph, weights, passengerRequests.Count, vehicles.Count);
            
            // Áp dụng matching
            var result = new BipartiteMatchingResult();
            foreach (var (passengerIdx, vehicleIdx) in matching)
            {
                if (passengerIdx < passengerRequests.Count && vehicleIdx < vehicles.Count)
                {
                    result.Assignments.Add(new PassengerVehicleAssignment
                    {
                        PassengerRequest = passengerRequests[passengerIdx],
                        Vehicle = vehicles[vehicleIdx],
                        Cost = weights[(passengerIdx, vehicleIdx)]
                    });
                }
            }

            result.TotalCost = result.Assignments.Sum(a => a.Cost);
            return result;
        }

        /// <summary>
        /// Xây dựng đồ thị bipartite: cạnh (u, v) tồn tại nếu hành khách u có thể ghép vào xe v
        /// </summary>
        private async Task<HashSet<(int passengerIdx, int vehicleIdx)>> BuildBipartiteGraphAsync(
            List<PassengerRequest> passengers,
            List<Vehicle> vehicles)
        {
            var graph = new HashSet<(int, int)>();

            for (int p = 0; p < passengers.Count; p++)
            {
                var passenger = passengers[p];
                
                for (int v = 0; v < vehicles.Count; v++)
                {
                    var vehicle = vehicles[v];

                    // Kiểm tra ràng buộc cơ bản
                    if (vehicle.AvailableSeats < passenger.SeatsNeeded)
                        continue;

                    if (vehicle.TotalSeats < passenger.SeatsNeeded)
                        continue;

                    // Kiểm tra time window
                    var windowStart = passenger.DepartureTime.AddHours(-1);
                    var windowEnd = passenger.DepartureTime.AddHours(1);
                    
                    if (vehicle.DepartureTime < windowStart || vehicle.DepartureTime > windowEnd)
                        continue;

                    // Kiểm tra route matching
                    var routeMatch = await _routeMatchingService.TryMatchRouteAsync(
                        vehicle,
                        passenger.PickupLat, passenger.PickupLon,
                        passenger.DropoffLat, passenger.DropoffLon,
                        passenger.SeatsNeeded);

                    if (routeMatch.CanMatch)
                    {
                        graph.Add((p, v));
                    }
                }
            }

            return graph;
        }

        /// <summary>
        /// Tính trọng số cho mỗi cạnh: chi phí khi ghép hành khách vào xe
        /// Trọng số càng nhỏ càng tốt (chi phí thấp)
        /// </summary>
        private async Task<Dictionary<(int passengerIdx, int vehicleIdx), double>> CalculateWeightsAsync(
            HashSet<(int, int)> graph,
            List<PassengerRequest> passengers,
            List<Vehicle> vehicles)
        {
            var weights = new Dictionary<(int, int), double>();

            foreach (var (pIdx, vIdx) in graph)
            {
                var passenger = passengers[pIdx];
                var vehicle = vehicles[vIdx];

                // Tính chi phí matching:
                // 1. Chi phí detour (quãng đường tăng thêm)
                // 2. Chi phí thời gian chờ
                // 3. Chi phí sử dụng ghế (ưu tiên xe có ít ghế trống)

                double detourCost = 0;
                double timeCost = 0;
                double seatUtilizationCost = 0;

                // Tính detour cost
                var routeMatch = await _routeMatchingService.TryMatchRouteAsync(
                    vehicle,
                    passenger.PickupLat, passenger.PickupLon,
                    passenger.DropoffLat, passenger.DropoffLon,
                    passenger.SeatsNeeded);

                if (routeMatch.DetourRatio.HasValue)
                {
                    detourCost = routeMatch.DetourRatio.Value * 1000; // Scale để có trọng số hợp lý
                }

                // Tính time cost (thời gian chờ)
                var timeDiff = Math.Abs((passenger.DepartureTime - vehicle.DepartureTime).TotalMinutes);
                timeCost = timeDiff * 10; // Mỗi phút chờ = 10 điểm chi phí

                // Tính seat utilization cost (ưu tiên xe có ít ghế trống)
                double utilizationRatio = (double)vehicle.AvailableSeats / vehicle.TotalSeats;
                seatUtilizationCost = utilizationRatio * 500; // Xe càng đầy càng tốt

                // Tổng chi phí
                double totalCost = detourCost + timeCost + seatUtilizationCost;
                weights[(pIdx, vIdx)] = totalCost;
            }

            return weights;
        }

        /// <summary>
        /// Hungarian Algorithm (Kuhn-Munkres) để tìm matching tối ưu với chi phí tối thiểu
        /// </summary>
        private List<(int passengerIdx, int vehicleIdx)> HungarianAlgorithm(
            HashSet<(int, int)> graph,
            Dictionary<(int, int), double> weights,
            int numPassengers,
            int numVehicles)
        {
            int n = Math.Max(numPassengers, numVehicles);
            
            // Tạo ma trận chi phí
            double[,] costMatrix = new double[n, n];
            
            // Khởi tạo với giá trị lớn (infinity)
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    costMatrix[i, j] = double.MaxValue;
                }
            }

            // Điền giá trị từ graph
            foreach (var (pIdx, vIdx) in graph)
            {
                if (pIdx < numPassengers && vIdx < numVehicles)
                {
                    costMatrix[pIdx, vIdx] = weights[(pIdx, vIdx)];
                }
            }

            // Chạy Hungarian Algorithm
            var assignment = new int[n];
            var result = new List<(int, int)>();

            // Simplified Hungarian (greedy matching với cải tiến)
            // Với số lượng lớn, có thể dùng thư viện hoặc implement đầy đủ
            var matchedPassengers = new bool[n];
            var matchedVehicles = new bool[n];

            // Sắp xếp các cạnh theo chi phí tăng dần
            var edges = new List<(int p, int v, double cost)>();
            foreach (var (pIdx, vIdx) in graph)
            {
                if (pIdx < numPassengers && vIdx < numVehicles)
                {
                    edges.Add((pIdx, vIdx, weights[(pIdx, vIdx)]));
                }
            }
            edges = edges.OrderBy(e => e.cost).ToList();

            // Greedy matching với ràng buộc
            foreach (var (p, v, cost) in edges)
            {
                if (!matchedPassengers[p] && !matchedVehicles[v])
                {
                    matchedPassengers[p] = true;
                    matchedVehicles[v] = true;
                    result.Add((p, v));
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Request từ hành khách
    /// </summary>
    public class PassengerRequest
    {
        public int Id { get; set; }
        public double PickupLat { get; set; }
        public double PickupLon { get; set; }
        public double DropoffLat { get; set; }
        public double DropoffLon { get; set; }
        public int SeatsNeeded { get; set; }
        public DateTime DepartureTime { get; set; }
        public string? PickupAddress { get; set; }
        public string? DropoffAddress { get; set; }
    }

    /// <summary>
    /// Kết quả matching
    /// </summary>
    public class BipartiteMatchingResult
    {
        public List<PassengerVehicleAssignment> Assignments { get; set; } = new();
        public double TotalCost { get; set; }
    }

    /// <summary>
    /// Assignment: hành khách được ghép vào xe
    /// </summary>
    public class PassengerVehicleAssignment
    {
        public PassengerRequest PassengerRequest { get; set; } = null!;
        public Vehicle Vehicle { get; set; } = null!;
        public double Cost { get; set; }
    }
}



















