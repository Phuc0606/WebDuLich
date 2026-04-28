using WebDuLichDaLat.Models;
using System.Collections.Generic;
using System.Linq;

namespace WebDuLichDaLat.Services
{
    public class CarpoolMatchingService
    {
        private readonly KMeansClusteringService _kmeansService;
        private readonly MinCostMaxFlowService _flowService;
        private readonly PDPTWService _pdptwService;

        public CarpoolMatchingService()
        {
            _kmeansService = new KMeansClusteringService();
            _flowService = new MinCostMaxFlowService();
            _pdptwService = new PDPTWService();
        }

        public List<CarpoolMatchResult> MatchPassengersToVehicles(
            List<Vehicle> vehicles,
            List<Passenger> passengers,
            List<PassengerGroup> groups)
        {
            var results = new List<CarpoolMatchResult>();

            if (!vehicles.Any() || !passengers.Any())
                return results;

            // Bước 1: K-Means grouping
            int k = Math.Min(5, Math.Max(1, passengers.Count / 3));
            var clusters = _kmeansService.ClusterPassengers(passengers, k);

            // Bước 2: Min-Cost Max-Flow
            var flowResult = _flowService.Solve(vehicles, passengers, groups);

            // Bước 3: Tối ưu route cho từng xe
            foreach (var assignment in flowResult.Assignments)
            {
                int vehicleId = assignment.Key;
                var vehicle = vehicles.FirstOrDefault(v => v.Id == vehicleId);
                if (vehicle == null) continue;

                // Danh sách khách theo flow (raw)
                var rawVehiclePassengers = passengers
                    .Where(p => assignment.Value.Contains(p.Id))
                    .ToList();

                // ===== FIX LỖI 26 GHẾ TRÊN XE 7 CHỖ =====
                // Chỉ nhận khách cho đến khi hết ghế
                var vehiclePassengers = new List<Passenger>();
                int usedSeats = 0;

                foreach (var p in rawVehiclePassengers)
                {
                    int seatsForPassenger = 1;

                    if (p.GroupId.HasValue)
                    {
                        var g = groups.FirstOrDefault(x => x.Id == p.GroupId.Value);
                        seatsForPassenger = g?.RequiredSeats ?? 1;
                    }

                    // Nếu thêm khách vượt sức chứa → bỏ qua khách đó
                    if (usedSeats + seatsForPassenger > vehicle.TotalSeats)
                        continue;

                    vehiclePassengers.Add(p);
                    usedSeats += seatsForPassenger;
                }

                if (!vehiclePassengers.Any())
                    continue;
                // =========================================

                // Tối ưu route
                var optimizedRoute = _pdptwService.OptimizeRoute(vehicle, vehiclePassengers);

                // Tính tổng chi phí
                decimal totalCost = vehicle.FixedPrice.HasValue && vehicle.FixedPrice.Value > 0
                    ? vehicle.FixedPrice.Value
                    : optimizedRoute.TotalCost;

                // Tổng ghế khách thực sự sử dụng (đã giới hạn)
                int occupiedSeats = usedSeats;

                // Số ghế trống
                int availableSeats = Math.Max(0, vehicle.TotalSeats - occupiedSeats);

                // Chi phí mỗi ghế
                decimal costPerSeat = occupiedSeats > 0
                    ? totalCost / occupiedSeats
                    : 0;

                // Tạo kết quả
                var matchResult = new CarpoolMatchResult
                {
                    VehicleId = vehicle.Id,
                    DriverName = vehicle.DriverName,
                    LicensePlate = vehicle.LicensePlate,
                    VehicleType = vehicle.VehicleType,
                    TotalSeats = vehicle.TotalSeats,
                    AvailableSeats = availableSeats,     // ĐÃ FIX
                    OccupiedSeats = occupiedSeats,       // ĐÃ FIX
                    TotalCost = totalCost,
                    CostPerPassenger = costPerSeat,
                    TotalDistance = optimizedRoute.TotalDistance,
                    EstimatedDepartureTime = optimizedRoute.StartTime,
                    EstimatedArrivalTime = optimizedRoute.EndTime,
                    PickupAddress = vehicle.PickupAddress ?? "",
                    DropoffAddress = vehicle.DropoffAddress ?? ""
                };

                // Danh sách matched passengers
                int sequence = 1;

                foreach (var node in optimizedRoute.Nodes)
                {
                    if (node.PassengerId.HasValue)
                    {
                        var passenger = vehiclePassengers
                            .FirstOrDefault(p => p.Id == node.PassengerId.Value);

                        if (passenger != null)
                        {
                            var existingMatch = matchResult.MatchedPassengers
                                .FirstOrDefault(m => m.PassengerId == passenger.Id);

                            if (existingMatch == null)
                            {
                                int seatsForPassenger = 1;

                                if (passenger.GroupId.HasValue)
                                {
                                    var g = groups.FirstOrDefault(x => x.Id == passenger.GroupId.Value);
                                    seatsForPassenger = g?.RequiredSeats ?? 1;
                                }

                                decimal passengerCost = seatsForPassenger * costPerSeat;

                                existingMatch = new PassengerMatch
                                {
                                    PassengerId = passenger.Id,
                                    PassengerName = passenger.Name,
                                    PickupAddress = passenger.PickupAddress ?? "",
                                    DropoffAddress = passenger.DropoffAddress ?? "",
                                    Cost = passengerCost,
                                    SequenceOrder = sequence++
                                };

                                matchResult.MatchedPassengers.Add(existingMatch);
                            }

                            if (node.Type == "pickup")
                                existingMatch.PickupTime = node.EarliestTime;

                            if (node.Type == "dropoff")
                                existingMatch.DropoffTime = node.LatestTime;
                        }
                    }
                }

                // Lưu route points
                sequence = 1;
                foreach (var node in optimizedRoute.Nodes)
                {
                    matchResult.OptimizedRoute.Add(new RoutePoint
                    {
                        Latitude = node.Latitude,
                        Longitude = node.Longitude,
                        Address = node.Address,
                        Type = node.Type,
                        PassengerId = node.PassengerId,
                        Time = node.EarliestTime,
                        Sequence = sequence++
                    });
                }

                results.Add(matchResult);
            }

            return results;
        }
    }
}































