using WebDuLichDaLat.Models;
using System.Collections.Generic;
using System.Linq;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Split Delivery Vehicle Routing Problem Service
    /// Tối ưu phân chia nhóm lớn thành nhiều xe với cân bằng tải
    /// </summary>
    public class SplitDeliveryVRPService
    {
        private static readonly int[] SupportedCapacities = new[] { 4, 7, 9 };

        /// <summary>
        /// Giải SDVRP bằng heuristic cân bằng tải
        /// </summary>
        public SplitDeliveryResult OptimizeGroupSplit(
            int totalPassengers,
            List<Vehicle> availableVehicles)
        {
            var result = new SplitDeliveryResult();

            if (totalPassengers <= 0)
                return result;

            // Sắp xếp xe theo sức chứa (từ nhỏ đến lớn)
            var sortedVehicles = availableVehicles
                .OrderBy(v => v.TotalSeats)
                .ToList();

            // Sắp xếp theo số ghế trống (từ ít đến nhiều) để ưu tiên xe đã có khách
            sortedVehicles = sortedVehicles
                .OrderBy(v => v.TotalSeats - v.AvailableSeats)
                .ThenBy(v => v.TotalSeats)
                .ToList();

            var assignments = new Dictionary<int, List<VehicleAssignment>>();
            var vehicleLoads = new Dictionary<int, int>();

            int remainingPassengers = totalPassengers;

            // Bước 1: Phân bổ vào các xe đã có sẵn (có khách)
            foreach (var vehicle in sortedVehicles.Where(v => v.AvailableSeats > 0))
            {
                if (remainingPassengers <= 0)
                    break;

                int availableSeats = vehicle.AvailableSeats;
                int assignToThisVehicle = Math.Min(remainingPassengers, availableSeats);

                if (assignToThisVehicle > 0)
                {
                    if (!assignments.ContainsKey(vehicle.Id))
                        assignments[vehicle.Id] = new List<VehicleAssignment>();

                    assignments[vehicle.Id].Add(new VehicleAssignment
                    {
                        VehicleId = vehicle.Id,
                        PassengersAssigned = assignToThisVehicle,
                        VehicleCapacity = vehicle.TotalSeats,
                        AvailableSeatsBefore = vehicle.AvailableSeats
                    });

                    vehicleLoads[vehicle.Id] = assignToThisVehicle;
                    remainingPassengers -= assignToThisVehicle;
                }
            }

            // Bước 2: Phân bổ phần còn lại vào xe mới với cân bằng tải
            if (remainingPassengers > 0)
            {
                var newVehicleAssignments = BalancedBinPacking(remainingPassengers);
                
                foreach (var assignment in newVehicleAssignments)
                {
                    if (!assignments.ContainsKey(assignment.VehicleId))
                        assignments[assignment.VehicleId] = new List<VehicleAssignment>();

                    assignments[assignment.VehicleId].Add(assignment);
                    vehicleLoads[assignment.VehicleId] = assignment.PassengersAssigned;
                }
            }

            result.VehicleAssignments = assignments;
            result.TotalVehiclesUsed = assignments.Count;
            result.LoadBalance = CalculateLoadBalance(vehicleLoads.Values.ToList());

            return result;
        }

        /// <summary>
        /// Balanced Bin Packing: phân chia cân bằng vào các xe
        /// </summary>
        private List<VehicleAssignment> BalancedBinPacking(int totalPassengers)
        {
            var assignments = new List<VehicleAssignment>();
            
            // Tính số xe cần thiết và phân chia cân bằng
            int vehiclesNeeded = CalculateOptimalVehicleCount(totalPassengers);
            int basePassengersPerVehicle = totalPassengers / vehiclesNeeded;
            int remainder = totalPassengers % vehiclesNeeded;

            // Xác định loại xe phù hợp
            int vehicleCapacity = DetermineOptimalVehicleCapacity(basePassengersPerVehicle + remainder);

            int vehicleIdCounter = -1; // ID tạm cho xe mới

            for (int i = 0; i < vehiclesNeeded; i++)
            {
                int passengersForThisVehicle = basePassengersPerVehicle;
                if (i < remainder)
                    passengersForThisVehicle++;

                assignments.Add(new VehicleAssignment
                {
                    VehicleId = vehicleIdCounter--,
                    PassengersAssigned = passengersForThisVehicle,
                    VehicleCapacity = vehicleCapacity,
                    AvailableSeatsBefore = vehicleCapacity - 1 // Trừ ghế tài xế
                });
            }

            return assignments;
        }

        /// <summary>
        /// Tính số xe tối ưu để cân bằng tải
        /// </summary>
        private int CalculateOptimalVehicleCount(int totalPassengers)
        {
            // Ưu tiên: 2 xe 7 chỗ (mỗi xe 5 người) thay vì 1 xe 9 chỗ + 1 xe 4 chỗ
            if (totalPassengers <= 8)
                return 1;
            
            if (totalPassengers <= 14)
                return 2; // 2 xe 7 chỗ
            
            if (totalPassengers <= 16)
                return 2; // 2 xe 9 chỗ hoặc mix

            // Với số lượng lớn hơn, tính toán động
            int vehiclesNeeded = (int)Math.Ceiling((double)totalPassengers / 8);
            return vehiclesNeeded;
        }

        /// <summary>
        /// Xác định loại xe phù hợp với số lượng hành khách
        /// </summary>
        private int DetermineOptimalVehicleCapacity(int passengersPerVehicle)
        {
            if (passengersPerVehicle <= 3)
                return 4;
            if (passengersPerVehicle <= 6)
                return 7;
            return 9;
        }

        /// <summary>
        /// Tính độ cân bằng tải (standard deviation của tải các xe)
        /// </summary>
        private double CalculateLoadBalance(List<int> vehicleLoads)
        {
            if (!vehicleLoads.Any())
                return 0;

            double mean = vehicleLoads.Average();
            double variance = vehicleLoads.Sum(load => Math.Pow(load - mean, 2)) / vehicleLoads.Count;
            return Math.Sqrt(variance);
        }

        /// <summary>
        /// Multi-objective optimization: cân bằng giữa số xe và cân bằng tải
        /// </summary>
        public SplitDeliveryResult OptimizeMultiObjective(
            int totalPassengers,
            List<Vehicle> availableVehicles,
            double weightVehicles = 0.6,
            double weightBalance = 0.4)
        {
            var bestResult = new SplitDeliveryResult();
            double bestScore = double.MaxValue;

            // Thử các cách phân chia khác nhau
            for (int numVehicles = 1; numVehicles <= Math.Min(5, totalPassengers); numVehicles++)
            {
                var testResult = TestVehicleConfiguration(totalPassengers, numVehicles, availableVehicles);
                
                // Tính score: minimize (weightVehicles * numVehicles + weightBalance * loadBalance)
                double score = weightVehicles * testResult.TotalVehiclesUsed + 
                              weightBalance * testResult.LoadBalance;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestResult = testResult;
                }
            }

            return bestResult;
        }

        /// <summary>
        /// Test một cấu hình cụ thể
        /// </summary>
        private SplitDeliveryResult TestVehicleConfiguration(
            int totalPassengers,
            int numVehicles,
            List<Vehicle> availableVehicles)
        {
            int basePassengersPerVehicle = totalPassengers / numVehicles;
            int remainder = totalPassengers % numVehicles;

            var assignments = new Dictionary<int, List<VehicleAssignment>>();
            var vehicleLoads = new List<int>();

            int vehicleIdCounter = -1;

            for (int i = 0; i < numVehicles; i++)
            {
                int passengersForThisVehicle = basePassengersPerVehicle;
                if (i < remainder)
                    passengersForThisVehicle++;

                int vehicleCapacity = DetermineOptimalVehicleCapacity(passengersForThisVehicle);

                int vehicleId = vehicleIdCounter--;

                if (!assignments.ContainsKey(vehicleId))
                    assignments[vehicleId] = new List<VehicleAssignment>();

                assignments[vehicleId].Add(new VehicleAssignment
                {
                    VehicleId = vehicleId,
                    PassengersAssigned = passengersForThisVehicle,
                    VehicleCapacity = vehicleCapacity,
                    AvailableSeatsBefore = vehicleCapacity - 1
                });

                vehicleLoads.Add(passengersForThisVehicle);
            }

            return new SplitDeliveryResult
            {
                VehicleAssignments = assignments,
                TotalVehiclesUsed = numVehicles,
                LoadBalance = CalculateLoadBalance(vehicleLoads)
            };
        }
    }

    /// <summary>
    /// Kết quả phân chia nhóm
    /// </summary>
    public class SplitDeliveryResult
    {
        public Dictionary<int, List<VehicleAssignment>> VehicleAssignments { get; set; } = new();
        public int TotalVehiclesUsed { get; set; }
        public double LoadBalance { get; set; } // Standard deviation của tải các xe
    }

    /// <summary>
    /// Assignment: số hành khách được phân vào xe
    /// </summary>
    public class VehicleAssignment
    {
        public int VehicleId { get; set; }
        public int PassengersAssigned { get; set; }
        public int VehicleCapacity { get; set; }
        public int AvailableSeatsBefore { get; set; }
    }
}



















