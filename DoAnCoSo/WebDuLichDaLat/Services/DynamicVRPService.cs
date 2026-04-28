using WebDuLichDaLat.Models;
using System.Collections.Generic;
using System.Linq;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Dynamic Vehicle Routing Problem Service
    /// Sử dụng Genetic Algorithm để tối ưu route động khi có khách mới
    /// </summary>
    public class DynamicVRPService
    {
        private readonly OsrmRouteService _osrmService;
        private const int PopulationSize = 50;
        private const int MaxGenerations = 100;
        private const double MutationRate = 0.1;
        private const double CrossoverRate = 0.8;

        public DynamicVRPService(OsrmRouteService osrmService)
        {
            _osrmService = osrmService;
        }

        /// <summary>
        /// Tối ưu route động cho xe khi có khách mới
        /// </summary>
        public async Task<DynamicRouteResult> OptimizeDynamicRouteAsync(
            Vehicle vehicle,
            List<Passenger> existingPassengers,
            Passenger newPassenger)
        {
            // Tạo danh sách tất cả hành khách (bao gồm khách mới)
            var allPassengers = new List<Passenger>(existingPassengers) { newPassenger };

            // Chạy Genetic Algorithm để tìm route tối ưu
            var bestRoute = await GeneticAlgorithmOptimization(vehicle, allPassengers);

            return bestRoute;
        }

        /// <summary>
        /// Genetic Algorithm để tối ưu route
        /// </summary>
        private async Task<DynamicRouteResult> GeneticAlgorithmOptimization(
            Vehicle vehicle,
            List<Passenger> passengers)
        {
            // Tạo nodes: pickup và dropoff cho mỗi hành khách
            var nodes = new List<RouteNode>();
            foreach (var passenger in passengers)
            {
                nodes.Add(new RouteNode
                {
                    Type = "pickup",
                    PassengerId = passenger.Id,
                    Latitude = passenger.PickupLatitude,
                    Longitude = passenger.PickupLongitude,
                    Address = passenger.PickupAddress ?? "",
                    EarliestTime = passenger.PreferredDepartureTime.AddMinutes(-30),
                    LatestTime = passenger.PreferredDepartureTime.AddHours(2)
                });

                nodes.Add(new RouteNode
                {
                    Type = "dropoff",
                    PassengerId = passenger.Id,
                    Latitude = passenger.DropoffLatitude,
                    Longitude = passenger.DropoffLongitude,
                    Address = passenger.DropoffAddress ?? "",
                    EarliestTime = passenger.PreferredArrivalTime?.AddMinutes(-30) ?? passenger.PreferredDepartureTime.AddHours(1),
                    LatestTime = passenger.PreferredArrivalTime?.AddHours(2) ?? passenger.PreferredDepartureTime.AddHours(4)
                });
            }

            // Khởi tạo population
            var population = new List<List<RouteNode>>();
            for (int i = 0; i < PopulationSize; i++)
            {
                population.Add(GenerateRandomRoute(nodes, vehicle));
            }

            DynamicRouteResult? bestSolution = null;
            double bestFitness = double.MaxValue;

            // Evolution loop
            for (int generation = 0; generation < MaxGenerations; generation++)
            {
                // Đánh giá fitness
                var fitnessScores = new List<double>();
                foreach (var route in population)
                {
                    var fitness = await CalculateFitnessAsync(route, vehicle);
                    fitnessScores.Add(fitness);

                    if (fitness < bestFitness)
                    {
                        bestFitness = fitness;
                        bestSolution = new DynamicRouteResult
                        {
                            Route = route,
                            TotalDistance = await CalculateTotalDistanceAsync(route),
                            TotalTime = await CalculateTotalTimeAsync(route, vehicle),
                            Fitness = fitness
                        };
                    }
                }

                // Selection và tạo thế hệ mới
                var newPopulation = new List<List<RouteNode>>();

                // Elitism: giữ lại best solutions
                var sortedIndices = fitnessScores
                    .Select((score, idx) => (score, idx))
                    .OrderBy(x => x.score)
                    .Select(x => x.idx)
                    .ToList();

                int eliteCount = PopulationSize / 10; // Giữ lại 10% tốt nhất
                for (int i = 0; i < eliteCount; i++)
                {
                    newPopulation.Add(new List<RouteNode>(population[sortedIndices[i]]));
                }

                // Crossover và Mutation
                while (newPopulation.Count < PopulationSize)
                {
                    // Tournament selection
                    var parent1 = TournamentSelection(population, fitnessScores);
                    var parent2 = TournamentSelection(population, fitnessScores);

                    List<RouteNode> child;
                    if (Random.Shared.NextDouble() < CrossoverRate)
                    {
                        child = OrderCrossover(parent1, parent2, nodes);
                    }
                    else
                    {
                        child = new List<RouteNode>(parent1);
                    }

                    // Mutation
                    if (Random.Shared.NextDouble() < MutationRate)
                    {
                        child = SwapMutation(child);
                    }

                    // Repair: đảm bảo route hợp lệ
                    child = RepairRoute(child, nodes, vehicle);

                    newPopulation.Add(child);
                }

                population = newPopulation;
            }

            return bestSolution ?? new DynamicRouteResult();
        }

        /// <summary>
        /// Tạo route ngẫu nhiên hợp lệ
        /// </summary>
        private List<RouteNode> GenerateRandomRoute(List<RouteNode> nodes, Vehicle vehicle)
        {
            var route = new List<RouteNode>();
            var unvisited = new List<RouteNode>(nodes);
            var pickedUpPassengers = new HashSet<int>();

            // Bắt đầu từ điểm xuất phát của xe
            double currentLat = vehicle.PickupLatitude;
            double currentLng = vehicle.PickupLongitude;
            DateTime currentTime = vehicle.DepartureTime;

            while (unvisited.Any())
            {
                // Chỉ chọn nodes hợp lệ
                var candidates = unvisited.Where(n =>
                {
                    if (n.Type == "dropoff" && !pickedUpPassengers.Contains(n.PassengerId))
                        return false;
                    return true;
                }).ToList();

                if (!candidates.Any())
                    break;

                // Chọn ngẫu nhiên
                var nextNode = candidates[Random.Shared.Next(candidates.Count)];
                route.Add(nextNode);
                unvisited.Remove(nextNode);

                if (nextNode.Type == "pickup")
                    pickedUpPassengers.Add(nextNode.PassengerId);
                else if (nextNode.Type == "dropoff")
                    pickedUpPassengers.Remove(nextNode.PassengerId);
            }

            return route;
        }

        /// <summary>
        /// Tính fitness: tổng khoảng cách + penalty cho vi phạm ràng buộc
        /// </summary>
        private async Task<double> CalculateFitnessAsync(List<RouteNode> route, Vehicle vehicle)
        {
            double totalDistance = await CalculateTotalDistanceAsync(route);
            double penalty = 0;

            // Kiểm tra ràng buộc
            var pickedUpPassengers = new HashSet<int>();
            DateTime currentTime = vehicle.DepartureTime;
            double currentLat = vehicle.PickupLatitude;
            double currentLng = vehicle.PickupLongitude;
            int currentLoad = 0;

            foreach (var node in route)
            {
                // Kiểm tra pickup trước dropoff
                if (node.Type == "dropoff" && !pickedUpPassengers.Contains(node.PassengerId))
                {
                    penalty += 10000; // Penalty lớn
                }

                // Tính thời gian đến node
                var distance = CalculateHaversineDistance(currentLat, currentLng, node.Latitude, node.Longitude);
                var travelTime = distance / 50.0 * 60; // Giả sử 50km/h
                currentTime = currentTime.AddMinutes(travelTime);

                // Kiểm tra time window
                if (currentTime < node.EarliestTime)
                {
                    penalty += (node.EarliestTime - currentTime).TotalMinutes * 10;
                    currentTime = node.EarliestTime;
                }
                else if (currentTime > node.LatestTime)
                {
                    penalty += (currentTime - node.LatestTime).TotalMinutes * 100;
                }

                // Cập nhật load
                if (node.Type == "pickup")
                {
                    currentLoad += 1; // Giả sử mỗi hành khách = 1 ghế
                    pickedUpPassengers.Add(node.PassengerId);
                }
                else if (node.Type == "dropoff")
                {
                    currentLoad -= 1;
                    pickedUpPassengers.Remove(node.PassengerId);
                }

                // Kiểm tra capacity
                int maxPassengerSeats = vehicle.TotalSeats > 1 ? vehicle.TotalSeats - 1 : 0;
                if (currentLoad > maxPassengerSeats)
                {
                    penalty += (currentLoad - maxPassengerSeats) * 1000;
                }

                currentLat = node.Latitude;
                currentLng = node.Longitude;
            }

            return totalDistance + penalty;
        }

        /// <summary>
        /// Order Crossover (OX) cho VRP
        /// </summary>
        private List<RouteNode> OrderCrossover(List<RouteNode> parent1, List<RouteNode> parent2, List<RouteNode> allNodes)
        {
            if (parent1.Count < 2)
                return new List<RouteNode>(parent1);

            int start = Random.Shared.Next(parent1.Count);
            int end = Random.Shared.Next(start, parent1.Count);

            var segment = parent1.Skip(start).Take(end - start + 1).ToList();
            var child = new List<RouteNode>();

            // Thêm các node từ parent2 không có trong segment
            foreach (var node in parent2)
            {
                if (!segment.Contains(node, new RouteNodeEqualityComparer()))
                {
                    child.Add(node);
                }
            }

            // Chèn segment vào vị trí ban đầu
            child.InsertRange(start, segment);

            return child;
        }

        /// <summary>
        /// Swap Mutation: đổi chỗ 2 nodes
        /// </summary>
        private List<RouteNode> SwapMutation(List<RouteNode> route)
        {
            if (route.Count < 2)
                return route;

            var mutated = new List<RouteNode>(route);
            int i = Random.Shared.Next(mutated.Count);
            int j = Random.Shared.Next(mutated.Count);

            if (i != j)
            {
                (mutated[i], mutated[j]) = (mutated[j], mutated[i]);
            }

            return mutated;
        }

        /// <summary>
        /// Repair route để đảm bảo hợp lệ
        /// </summary>
        private List<RouteNode> RepairRoute(List<RouteNode> route, List<RouteNode> allNodes, Vehicle vehicle)
        {
            // Đảm bảo mỗi hành khách có cả pickup và dropoff
            var passengerIds = allNodes.Where(n => n.Type == "pickup").Select(n => n.PassengerId).Distinct().ToList();
            var routePassengerIds = route.Select(n => n.PassengerId).Distinct().ToList();

            var repaired = new List<RouteNode>(route);
            var pickedUp = new HashSet<int>();

            // Thêm các node còn thiếu
            foreach (var passengerId in passengerIds)
            {
                var pickup = allNodes.FirstOrDefault(n => n.Type == "pickup" && n.PassengerId == passengerId);
                var dropoff = allNodes.FirstOrDefault(n => n.Type == "dropoff" && n.PassengerId == passengerId);

                if (pickup != null && !repaired.Contains(pickup, new RouteNodeEqualityComparer()))
                {
                    repaired.Add(pickup);
                }
                if (dropoff != null && !repaired.Contains(dropoff, new RouteNodeEqualityComparer()))
                {
                    repaired.Add(dropoff);
                }
            }

            // Sắp xếp lại để đảm bảo pickup trước dropoff
            var finalRoute = new List<RouteNode>();
            var unvisited = new List<RouteNode>(repaired);

            while (unvisited.Any())
            {
                var candidates = unvisited.Where(n =>
                {
                    if (n.Type == "dropoff" && !pickedUp.Contains(n.PassengerId))
                        return false;
                    return true;
                }).ToList();

                if (!candidates.Any())
                    break;

                var next = candidates[Random.Shared.Next(candidates.Count)];
                finalRoute.Add(next);
                unvisited.Remove(next);

                if (next.Type == "pickup")
                    pickedUp.Add(next.PassengerId);
                else if (next.Type == "dropoff")
                    pickedUp.Remove(next.PassengerId);
            }

            return finalRoute;
        }

        /// <summary>
        /// Tournament Selection
        /// </summary>
        private List<RouteNode> TournamentSelection(List<List<RouteNode>> population, List<double> fitnessScores, int tournamentSize = 3)
        {
            var tournament = new List<(List<RouteNode> route, double fitness)>();
            
            for (int i = 0; i < tournamentSize; i++)
            {
                int idx = Random.Shared.Next(population.Count);
                tournament.Add((population[idx], fitnessScores[idx]));
            }

            return tournament.OrderBy(t => t.fitness).First().route;
        }

        private async Task<double> CalculateTotalDistanceAsync(List<RouteNode> route)
        {
            if (!route.Any())
                return 0;

            double total = 0;
            for (int i = 0; i < route.Count - 1; i++)
            {
                total += CalculateHaversineDistance(
                    route[i].Latitude, route[i].Longitude,
                    route[i + 1].Latitude, route[i + 1].Longitude);
            }
            return total;
        }

        private async Task<double> CalculateTotalTimeAsync(List<RouteNode> route, Vehicle vehicle)
        {
            DateTime currentTime = vehicle.DepartureTime;
            double currentLat = vehicle.PickupLatitude;
            double currentLng = vehicle.PickupLongitude;

            foreach (var node in route)
            {
                var distance = CalculateHaversineDistance(currentLat, currentLng, node.Latitude, node.Longitude);
                var travelTime = distance / 50.0 * 60; // 50km/h
                currentTime = currentTime.AddMinutes(travelTime);
                
                if (currentTime < node.EarliestTime)
                    currentTime = node.EarliestTime;
                
                currentTime = currentTime.AddMinutes(5); // Service time
                currentLat = node.Latitude;
                currentLng = node.Longitude;
            }

            return (currentTime - vehicle.DepartureTime).TotalMinutes;
        }

        private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
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
    }

    public class RouteNode
    {
        public string Type { get; set; } = ""; // "pickup" or "dropoff"
        public int PassengerId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; } = "";
        public DateTime EarliestTime { get; set; }
        public DateTime LatestTime { get; set; }
    }

    public class DynamicRouteResult
    {
        public List<RouteNode> Route { get; set; } = new();
        public double TotalDistance { get; set; }
        public double TotalTime { get; set; }
        public double Fitness { get; set; }
    }

    public class RouteNodeEqualityComparer : IEqualityComparer<RouteNode>
    {
        public bool Equals(RouteNode? x, RouteNode? y)
        {
            if (x == null || y == null)
                return false;
            return x.Type == y.Type && x.PassengerId == y.PassengerId;
        }

        public int GetHashCode(RouteNode obj)
        {
            return HashCode.Combine(obj.Type, obj.PassengerId);
        }
    }
}



















