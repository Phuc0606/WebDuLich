using WebDuLichDaLat.Models;
using System.Collections.Generic;
using System.Linq;

namespace WebDuLichDaLat.Services
{
    public class MinCostMaxFlowService
    {
        public class FlowEdge
        {
            public int From { get; set; }
            public int To { get; set; }
            public int Capacity { get; set; }
            public decimal Cost { get; set; }
            public int Flow { get; set; }
            public FlowEdge Reverse { get; set; }
        }

        public class FlowResult
        {
            public int MaxFlow { get; set; }
            public decimal MinCost { get; set; }
            public Dictionary<int, List<int>> Assignments { get; set; } = new Dictionary<int, List<int>>(); // VehicleId -> List of PassengerIds
        }

        public FlowResult Solve(List<Vehicle> vehicles, List<Passenger> passengers, List<PassengerGroup> groups)
        {
            // Tạo graph: Source -> Vehicles -> Passengers/Groups -> Sink
            int source = 0;
            int sink = 1;
            int vehicleStart = 2;
            int passengerStart = vehicleStart + vehicles.Count;
            int groupStart = passengerStart + passengers.Count;

            var graph = new Dictionary<int, List<FlowEdge>>();
            var allEdges = new List<FlowEdge>();

            // Khởi tạo graph (cần thêm sink node)
            int totalNodes = groupStart + groups.Count + 2; // +2 for source and sink
            for (int i = 0; i < totalNodes; i++)
            {
                graph[i] = new List<FlowEdge>();
            }

            // Source -> Vehicles
            for (int i = 0; i < vehicles.Count; i++)
            {
                int vehicleNode = vehicleStart + i;
                var edge = CreateEdge(source, vehicleNode, vehicles[i].AvailableSeats, 0);
                graph[source].Add(edge);
                graph[vehicleNode].Add(edge.Reverse);
                allEdges.Add(edge);
            }

            // Vehicles -> Passengers (individual)
            for (int v = 0; v < vehicles.Count; v++)
            {
                int vehicleNode = vehicleStart + v;
                var vehicle = vehicles[v];

                for (int p = 0; p < passengers.Count; p++)
                {
                    var passenger = passengers[p];
                    if (passenger.GroupId.HasValue) continue; // Bỏ qua nếu thuộc group

                    int passengerNode = passengerStart + p;
                    decimal cost = CalculateCost(vehicle, passenger);
                    var edge = CreateEdge(vehicleNode, passengerNode, 1, cost);
                    graph[vehicleNode].Add(edge);
                    graph[passengerNode].Add(edge.Reverse);
                    allEdges.Add(edge);
                }
            }

            // Vehicles -> Groups
            for (int v = 0; v < vehicles.Count; v++)
            {
                int vehicleNode = vehicleStart + v;
                var vehicle = vehicles[v];

                for (int g = 0; g < groups.Count; g++)
                {
                    var group = groups[g];
                    if (vehicle.AvailableSeats < group.RequiredSeats) continue;

                    int groupNode = groupStart + g;
                    decimal cost = CalculateGroupCost(vehicle, group);
                    var edge = CreateEdge(vehicleNode, groupNode, 1, cost);
                    graph[vehicleNode].Add(edge);
                    graph[groupNode].Add(edge.Reverse);
                    allEdges.Add(edge);
                }
            }

            // Groups -> Group Passengers
            for (int g = 0; g < groups.Count; g++)
            {
                int groupNode = groupStart + g;
                var group = groups[g];
                var groupPassengers = passengers.Where(p => p.GroupId == group.Id).ToList();

                foreach (var passenger in groupPassengers)
                {
                    int passengerNode = passengerStart + passengers.IndexOf(passenger);
                    var edge = CreateEdge(groupNode, passengerNode, 1, 0);
                    graph[groupNode].Add(edge);
                    graph[passengerNode].Add(edge.Reverse);
                    allEdges.Add(edge);
                }
            }

            // Passengers -> Sink
            for (int p = 0; p < passengers.Count; p++)
            {
                int passengerNode = passengerStart + p;
                var edge = CreateEdge(passengerNode, sink, 1, 0);
                graph[passengerNode].Add(edge);
                graph[sink].Add(edge.Reverse);
                allEdges.Add(edge);
            }

            // Chạy Min-Cost Max-Flow (Bellman-Ford + DFS)
            return MinCostMaxFlow(graph, source, sink, vehicleStart, passengerStart, groupStart, vehicles, passengers, groups);
        }

        private FlowEdge CreateEdge(int from, int to, int capacity, decimal cost)
        {
            var edge = new FlowEdge
            {
                From = from,
                To = to,
                Capacity = capacity,
                Cost = cost,
                Flow = 0
            };

            var reverse = new FlowEdge
            {
                From = to,
                To = from,
                Capacity = 0,
                Cost = -cost,
                Flow = 0
            };

            edge.Reverse = reverse;
            reverse.Reverse = edge;

            return edge;
        }

        private decimal CalculateCost(Vehicle vehicle, Passenger passenger)
        {
            // Tính chi phí dựa trên khoảng cách
            double distance = CalculateDistance(
                vehicle.PickupLatitude, vehicle.PickupLongitude,
                passenger.PickupLatitude, passenger.PickupLongitude);

            double dropoffDistance = CalculateDistance(
                passenger.PickupLatitude, passenger.PickupLongitude,
                passenger.DropoffLatitude, passenger.DropoffLongitude);

            double totalDistance = distance + dropoffDistance;
            return (decimal)totalDistance * vehicle.CostPerKm;
        }

        private decimal CalculateGroupCost(Vehicle vehicle, PassengerGroup group)
        {
            // Tính chi phí cho cả nhóm (có thể tối ưu hơn)
            var groupPassengers = group.Passengers.ToList();
            if (!groupPassengers.Any()) return decimal.MaxValue;

            decimal totalCost = 0;
            foreach (var passenger in groupPassengers)
            {
                totalCost += CalculateCost(vehicle, passenger);
            }

            return totalCost;
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double angle) => angle * Math.PI / 180;

        private FlowResult MinCostMaxFlow(
            Dictionary<int, List<FlowEdge>> graph,
            int source, int sink,
            int vehicleStart, int passengerStart, int groupStart,
            List<Vehicle> vehicles, List<Passenger> passengers, List<PassengerGroup> groups)
        {
            var result = new FlowResult();
            var assignments = new Dictionary<int, List<int>>();

            // Sử dụng Bellman-Ford để tìm đường đi có chi phí tối thiểu
            int maxIterations = 1000;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                var distances = new Dictionary<int, decimal>();
                var parents = new Dictionary<int, FlowEdge>();
                var inQueue = new HashSet<int>();

                for (int i = 0; i < graph.Count; i++)
                {
                    distances[i] = i == source ? 0 : decimal.MaxValue;
                }

                var queue = new Queue<int>();
                queue.Enqueue(source);
                inQueue.Add(source);

                while (queue.Count > 0)
                {
                    int u = queue.Dequeue();
                    inQueue.Remove(u);

                    foreach (var edge in graph[u])
                    {
                        if (edge.Capacity - edge.Flow > 0)
                        {
                            decimal newDist = distances[u] + edge.Cost;
                            if (newDist < distances[edge.To])
                            {
                                distances[edge.To] = newDist;
                                parents[edge.To] = edge;

                                if (!inQueue.Contains(edge.To))
                                {
                                    queue.Enqueue(edge.To);
                                    inQueue.Add(edge.To);
                                }
                            }
                        }
                    }
                }

                if (distances[sink] == decimal.MaxValue)
                    break;

                // Tìm đường đi từ source đến sink
                var path = new List<FlowEdge>();
                int current = sink;
                int minFlow = int.MaxValue;

                while (current != source)
                {
                    if (!parents.ContainsKey(current))
                        break;

                    var edge = parents[current];
                    path.Add(edge);
                    minFlow = Math.Min(minFlow, edge.Capacity - edge.Flow);
                    current = edge.From;
                }

                if (path.Count == 0)
                    break;

                // Cập nhật flow
                foreach (var edge in path)
                {
                    edge.Flow += minFlow;
                    edge.Reverse.Flow -= minFlow;
                }

                result.MaxFlow += minFlow;
                result.MinCost += distances[sink] * minFlow;

                // Cập nhật assignments
                foreach (var edge in path)
                {
                    if (edge.From >= vehicleStart && edge.From < passengerStart)
                    {
                        int vehicleIndex = edge.From - vehicleStart;
                        int vehicleId = vehicles[vehicleIndex].Id;

                        if (edge.To >= passengerStart && edge.To < groupStart)
                        {
                            int passengerIndex = edge.To - passengerStart;
                            int passengerId = passengers[passengerIndex].Id;

                            if (!assignments.ContainsKey(vehicleId))
                                assignments[vehicleId] = new List<int>();
                            assignments[vehicleId].Add(passengerId);
                        }
                        else if (edge.To >= groupStart)
                        {
                            int groupIndex = edge.To - groupStart;
                            var group = groups[groupIndex];
                            var groupPassengers = passengers.Where(p => p.GroupId == group.Id).ToList();

                            if (!assignments.ContainsKey(vehicleId))
                                assignments[vehicleId] = new List<int>();
                            foreach (var p in groupPassengers)
                            {
                                assignments[vehicleId].Add(p.Id);
                            }
                        }
                    }
                }
            }

            result.Assignments = assignments;
            return result;
        }
    }
}
































