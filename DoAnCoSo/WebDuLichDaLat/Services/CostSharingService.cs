using WebDuLichDaLat.Models;
using System.Collections.Generic;
using System.Linq;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Cost Sharing Service sử dụng Game Theory
    /// Áp dụng Shapley Value và Nucleolus để phân chia chi phí công bằng
    /// </summary>
    public class CostSharingService
    {
        private readonly OsrmRouteService _osrmService;

        public CostSharingService(OsrmRouteService osrmService)
        {
            _osrmService = osrmService;
        }

        /// <summary>
        /// Tính Shapley Value cho phân chia chi phí
        /// </summary>
        public Dictionary<int, decimal> CalculateShapleyValue(
            Vehicle vehicle,
            List<Passenger> passengers,
            Func<List<Passenger>, decimal> costFunction)
        {
            int n = passengers.Count;
            var shapleyValues = new Dictionary<int, decimal>();

            foreach (var passenger in passengers)
            {
                shapleyValues[passenger.Id] = 0;
            }

            // Tính Shapley Value cho mỗi hành khách
            foreach (var passenger in passengers)
            {
                // Tạo tất cả các tập hợp con không có passenger này
                var subsets = GetAllSubsets(passengers.Where(p => p.Id != passenger.Id).ToList());

                foreach (var subset in subsets)
                {
                    int s = subset.Count;
                    
                    // Tính marginal contribution
                    var subsetWithPassenger = new List<Passenger>(subset) { passenger };
                    decimal costWith = costFunction(subsetWithPassenger);
                    decimal costWithout = costFunction(subset);
                    decimal marginal = costWith - costWithout;

                    // Tính weight: |S|!(n-|S|-1)! / n!
                    decimal weight = CalculateShapleyWeight(s, n);

                    shapleyValues[passenger.Id] += weight * marginal;
                }
            }

            return shapleyValues;
        }

        /// <summary>
        /// Tính Nucleolus (Monte Carlo approximation cho số lượng lớn)
        /// </summary>
        public Dictionary<int, decimal> CalculateNucleolus(
            Vehicle vehicle,
            List<Passenger> passengers,
            Func<List<Passenger>, decimal> costFunction)
        {
            // Với số lượng lớn, sử dụng Monte Carlo Shapley như approximation
            // Hoặc giải bài toán tối ưu đơn giản hóa
            int n = passengers.Count;
            decimal grandCoalitionCost = costFunction(passengers);

            // Khởi tạo: chia đều
            var nucleolusValues = passengers.ToDictionary(p => p.Id, p => grandCoalitionCost / n);

            // Iterative improvement để minimize maximal excess
            for (int iteration = 0; iteration < 100; iteration++)
            {
                var excesses = new Dictionary<string, decimal>();
                
                // Tính excess cho mỗi tập hợp con
                var subsets = GetAllSubsets(passengers);
                foreach (var subset in subsets)
                {
                    if (subset.Count == 0 || subset.Count == n)
                        continue;

                    decimal subsetCost = costFunction(subset);
                    decimal subsetPayment = subset.Sum(p => nucleolusValues[p.Id]);
                    decimal excess = subsetCost - subsetPayment;
                    
                    string subsetKey = string.Join(",", subset.OrderBy(p => p.Id).Select(p => p.Id));
                    excesses[subsetKey] = excess;
                }

                if (!excesses.Any())
                    break;

                // Tìm maximal excess
                var maxExcess = excesses.Values.Max();
                if (maxExcess <= 0)
                    break;

                // Điều chỉnh: giảm payment của các tập hợp có excess lớn
                foreach (var kvp in excesses.Where(e => e.Value > 0))
                {
                    var subsetIds = kvp.Key.Split(',').Select(int.Parse).ToList();
                    decimal adjustment = kvp.Value / subsetIds.Count / 10; // Điều chỉnh nhỏ

                    foreach (var id in subsetIds)
                    {
                        if (nucleolusValues.ContainsKey(id))
                        {
                            nucleolusValues[id] += adjustment;
                        }
                    }
                }

                // Đảm bảo efficiency: tổng = grand coalition cost
                decimal total = nucleolusValues.Values.Sum();
                decimal diff = grandCoalitionCost - total;
                if (Math.Abs(diff) > 0.01m)
                {
                    decimal perPassenger = diff / n;
                    foreach (var id in nucleolusValues.Keys.ToList())
                    {
                        nucleolusValues[id] += perPassenger;
                    }
                }
            }

            return nucleolusValues;
        }

        /// <summary>
        /// Tính chi phí cho một tập hợp hành khách
        /// </summary>
        public async Task<decimal> CalculateRouteCostAsync(
            List<Passenger> passengers,
            Vehicle vehicle)
        {
            if (!passengers.Any())
                return 0;

            // Tính route tối ưu cho tập hợp này
            double totalDistance = 0;

            if (passengers.Count == 1)
            {
                var p = passengers[0];
                var distance = await _osrmService.GetDistanceKmAsync(
                    p.PickupLatitude, p.PickupLongitude,
                    p.DropoffLatitude, p.DropoffLongitude);
                totalDistance = distance ?? 0;
            }
            else
            {
                // Tính route bao phủ tất cả điểm đón/trả
                var allPoints = new List<(double lat, double lon)>();
                foreach (var p in passengers)
                {
                    allPoints.Add((p.PickupLatitude, p.PickupLongitude));
                    allPoints.Add((p.DropoffLatitude, p.DropoffLongitude));
                }

                // Tính khoảng cách tổng (approximation)
                double minLat = allPoints.Min(p => p.lat);
                double maxLat = allPoints.Max(p => p.lat);
                double minLon = allPoints.Min(p => p.lon);
                double maxLon = allPoints.Max(p => p.lon);

                // Tính khoảng cách đường chéo + buffer
                totalDistance = CalculateHaversineDistance(minLat, minLon, maxLat, maxLon) * 1.5;
            }

            // Tính chi phí dựa trên khoảng cách và loại xe
            decimal fuelCost = (decimal)totalDistance * GetFuelPricePerKm(vehicle.TotalSeats);
            decimal driverCost = GetDriverCost(vehicle.TotalSeats);
            decimal tollCost = totalDistance > 50 ? 150000m : 0;

            return fuelCost + driverCost + tollCost;
        }

        /// <summary>
        /// Tính chi phí đi taxi riêng cho một hành khách (Individual Rationality)
        /// </summary>
        public async Task<decimal> CalculateIndividualTaxiCostAsync(Passenger passenger)
        {
            var distance = await _osrmService.GetDistanceKmAsync(
                passenger.PickupLatitude, passenger.PickupLongitude,
                passenger.DropoffLatitude, passenger.DropoffLongitude);

            if (!distance.HasValue)
                return 500000m; // Default

            // Giá taxi: ~15,000đ/km
            return (decimal)distance.Value * 15000m;
        }

        /// <summary>
        /// Kiểm tra Individual Rationality: mỗi hành khách phải trả ≤ chi phí đi riêng
        /// </summary>
        public async Task<bool> CheckIndividualRationalityAsync(
            Dictionary<int, decimal> costSharing,
            List<Passenger> passengers)
        {
            foreach (var passenger in passengers)
            {
                decimal individualCost = await CalculateIndividualTaxiCostAsync(passenger);
                if (costSharing.ContainsKey(passenger.Id) && costSharing[passenger.Id] > individualCost)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Tính Shapley Weight: |S|!(n-|S|-1)! / n!
        /// </summary>
        private decimal CalculateShapleyWeight(int s, int n)
        {
            decimal numerator = Factorial(s) * Factorial(n - s - 1);
            decimal denominator = Factorial(n);
            return numerator / denominator;
        }

        /// <summary>
        /// Tính giai thừa
        /// </summary>
        private decimal Factorial(int n)
        {
            if (n <= 1)
                return 1;
            
            decimal result = 1;
            for (int i = 2; i <= n; i++)
            {
                result *= i;
            }
            return result;
        }

        /// <summary>
        /// Tạo tất cả các tập hợp con
        /// </summary>
        private List<List<Passenger>> GetAllSubsets(List<Passenger> passengers)
        {
            var subsets = new List<List<Passenger>> { new List<Passenger>() };

            foreach (var passenger in passengers)
            {
                var newSubsets = new List<List<Passenger>>();
                foreach (var subset in subsets)
                {
                    var newSubset = new List<Passenger>(subset) { passenger };
                    newSubsets.Add(newSubset);
                }
                subsets.AddRange(newSubsets);
            }

            return subsets;
        }

        private decimal GetFuelPricePerKm(int totalSeats)
        {
            return totalSeats <= 4 ? 3000m : (totalSeats <= 7 ? 4000m : 5000m);
        }

        private decimal GetDriverCost(int totalSeats)
        {
            return totalSeats <= 4 ? 500000m : (totalSeats <= 7 ? 700000m : 900000m);
        }

        private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
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
    }
}


















