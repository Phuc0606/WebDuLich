using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WebDuLichDaLat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebDuLichDaLat.Services
{
    public class RouteMatchResult
    {
        public bool CanMatch { get; set; }
        public Vehicle? MatchedVehicle { get; set; }
        public int? PickupOrder { get; set; }
        public int? DropoffOrder { get; set; }
        public double? DetourRatio { get; set; }
        public string? Reason { get; set; }
    }

    public class RouteMatchingService
    {
        private readonly OsrmRouteService _osrmService;
        private readonly ApplicationDbContext _context;

        public RouteMatchingService(OsrmRouteService osrmService, ApplicationDbContext context)
        {
            _osrmService = osrmService;
            _context = context;
        }

        /// <summary>
        /// Kiểm tra xem có thể ghép hành khách vào xe theo tuyến cùng chiều không
        /// </summary>
        public async Task<RouteMatchResult> TryMatchRouteAsync(
            Vehicle vehicle,
            double pickupLat, double pickupLon,
            double dropoffLat, double dropoffLon,
            int seatsNeeded,
            double maxDetourRatio = 0.20)
        {
            var result = new RouteMatchResult { MatchedVehicle = vehicle };

            // Nếu xe không có route polyline, không thể ghép theo tuyến
            if (string.IsNullOrEmpty(vehicle.RoutePolyline))
            {
                result.Reason = "Xe chưa có tuyến đường được xác định";
                return result;
            }

            // Parse route polyline
            List<List<double>>? routeGeometry;
            try
            {
                routeGeometry = JsonConvert.DeserializeObject<List<List<double>>>(vehicle.RoutePolyline);
                if (routeGeometry == null || routeGeometry.Count < 2)
                {
                    result.Reason = "Tuyến đường không hợp lệ";
                    return result;
                }
            }
            catch
            {
                result.Reason = "Lỗi parse tuyến đường";
                return result;
            }

            // Xác định thứ tự đón/trả bằng cách snap tọa độ lên polyline
            var (pickupOrder, pickupDistance) = FindClosestPointOnRouteWithDistance(routeGeometry, pickupLat, pickupLon);
            var (dropoffOrder, dropoffDistance) = FindClosestPointOnRouteWithDistance(routeGeometry, dropoffLat, dropoffLon);

            // Cho phép điểm đón/trả nằm ngoài polyline nhưng gần (trong bán kính 5km)
            const double maxSnapDistanceKm = 5.0;
            
            if (pickupDistance > maxSnapDistanceKm)
            {
                result.Reason = $"Điểm đón quá xa tuyến đường (cách {pickupDistance:F1} km)";
                return result;
            }
            
            if (dropoffDistance > maxSnapDistanceKm)
            {
                result.Reason = $"Điểm đến quá xa tuyến đường (cách {dropoffDistance:F1} km)";
                return result;
            }

            // Kiểm tra cùng chiều: PickupOrder phải < DropoffOrder
            // Cho phép điểm đến nằm sau điểm cuối của tuyến (dropoffOrder có thể = polyline.Count - 1)
            if (pickupOrder >= dropoffOrder)
            {
                // Nếu dropoffOrder là điểm cuối và điểm đến gần điểm cuối, cho phép
                if (dropoffOrder == routeGeometry.Count - 1 && dropoffDistance <= 2.0)
                {
                    // Cho phép: điểm đến nằm sau điểm cuối nhưng gần
                    // Không cần thay đổi dropoffOrder
                }
                else
                {
                    result.Reason = "Hành trình không cùng chiều với tuyến xe";
                    return result;
                }
            }

            // Kiểm tra detour ratio động thay vì ngưỡng khoảng cách cứng (5km)
            var detourRatio = await CalculateDetourRatioAsync(
                routeGeometry,
                pickupLat, pickupLon,
                dropoffLat, dropoffLon);

            if (detourRatio.HasValue)
            {
                result.DetourRatio = detourRatio.Value;

                if (detourRatio.Value > maxDetourRatio)
                {
                    result.Reason = $"Detour vượt ngưỡng {maxDetourRatio:P0} (thực tế {detourRatio.Value:P1})";
                    return result;
                }
            }

            // Kiểm tra segment occupancy: mọi đoạn từ PickupOrder đến DropoffOrder phải còn đủ ghế
            var canFit = await CheckSegmentOccupancyAsync(vehicle, pickupOrder, dropoffOrder, seatsNeeded);
            if (!canFit)
            {
                result.Reason = "Không còn đủ ghế trên các đoạn đường của hành trình";
                return result;
            }

            result.CanMatch = true;
            result.PickupOrder = pickupOrder;
            result.DropoffOrder = dropoffOrder;
            return result;
        }

        /// <summary>
        /// Tìm điểm gần nhất trên route polyline (trả về index và khoảng cách)
        /// </summary>
        private (int index, double distanceKm) FindClosestPointOnRouteWithDistance(List<List<double>> routeGeometry, double lat, double lon)
        {
            double minDistance = double.MaxValue;
            int closestIndex = 0;

            for (int i = 0; i < routeGeometry.Count; i++)
            {
                var point = routeGeometry[i];
                if (point.Count < 2) continue;

                var pointLon = point[0];
                var pointLat = point[1];

                // Tính khoảng cách Haversine
                var distance = CalculateHaversineDistance(lat, lon, pointLat, pointLon);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestIndex = i;
                }
            }

            return (closestIndex, minDistance);
        }

        /// <summary>
        /// Tìm điểm gần nhất trên route polyline (backward compatibility)
        /// </summary>
        private int FindClosestPointOnRoute(List<List<double>> routeGeometry, double lat, double lon)
        {
            var (index, _) = FindClosestPointOnRouteWithDistance(routeGeometry, lat, lon);
            return index;
        }

        /// <summary>
        /// Tính khoảng cách Haversine (km)
        /// </summary>
        private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadius = 6371;
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);
            lat1 = ToRadians(lat1);
            lat2 = ToRadians(lat2);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadius * c;
        }


        public async Task<int> CalculateMinAvailableSeatsAsync(int vehicleId, int totalSeats)
        {
            // 1. Lấy tất cả hành khách trên xe
            var passengers = await _context.Passengers
                .Include(p => p.Group)
                .Where(p => p.MatchedVehicleId == vehicleId && p.IsMatched)
                .ToListAsync();

           

            // 3. Tìm điểm đầu và điểm cuối của toàn bộ lộ trình xe (theo Order)
            // Nếu không có hành khách, mặc định min=0, max=0 để vòng lặp không chạy
            int minOrder = passengers.Any() ? passengers.Min(p => p.PickupOrder ?? 0) : 0;
            int maxOrder = passengers.Any() ? passengers.Max(p => p.DropoffOrder ?? 100) : 0;

            int maxOccupancy = 0;

            // 4. Quét qua từng đoạn đường (Segment) để xem đoạn nào đông nhất
            for (int i = minOrder; i < maxOrder; i++)
            {
                int currentSegmentOccupancy = 0;
                foreach (var p in passengers)
                {
                    // Nếu đoạn i nằm trong quãng đường đi của khách p
                    if (p.PickupOrder <= i && p.DropoffOrder > i)
                    {
                        currentSegmentOccupancy += (p.Group?.RequiredSeats ?? 1);
                    }
                }

                if (currentSegmentOccupancy > maxOccupancy)
                {
                    maxOccupancy = currentSegmentOccupancy;
                }
            }

            // 5. Số ghế trống thực tế = Tổng ghế - 1 (Tài xế) - Số ghế tại đoạn đông khách nhất
            int passengerSeats = totalSeats - 1;

            // Đảm bảo không trả về số âm
            return Math.Max(0, passengerSeats - maxOccupancy);
        }

        /// <summary>
        /// Tính Detour Time Ratio: (thời gian mới - thời gian gốc) / thời gian gốc
        /// </summary>
        private async Task<double?> CalculateDetourRatioAsync(
            List<List<double>> routeGeometry,
            double pickupLat, double pickupLon,
            double dropoffLat, double dropoffLon)
        {
            if (routeGeometry == null || routeGeometry.Count < 2)
                return null;

            // OSRM geometry trả [lon, lat]
            var start = routeGeometry.First();
            var end = routeGeometry.Last();

            // Tính thời gian gốc của tuyến hiện tại
            var baseRoute = await _osrmService.GetRouteAsync(
                start[1], start[0],
                end[1], end[0]);

            if (baseRoute == null || baseRoute.DurationSeconds <= 0)
                return null;

            var leg1 = await _osrmService.GetRouteAsync(
                start[1], start[0],
                pickupLat, pickupLon);

            var leg2 = await _osrmService.GetRouteAsync(
                pickupLat, pickupLon,
                dropoffLat, dropoffLon);

            var leg3 = await _osrmService.GetRouteAsync(
                dropoffLat, dropoffLon,
                end[1], end[0]);

            if (leg1 == null || leg2 == null || leg3 == null)
                return null;

            double newDuration = leg1.DurationSeconds + leg2.DurationSeconds + leg3.DurationSeconds;

            if (baseRoute.DurationSeconds <= 0)
                return null;

            double detourRatio = (newDuration - baseRoute.DurationSeconds) / baseRoute.DurationSeconds;
            return detourRatio;
        }





        private double ToRadians(double angle) => angle * Math.PI / 180;

        /// <summary>
        /// Kiểm tra xem các đoạn từ pickupOrder đến dropoffOrder còn đủ ghế không
        /// </summary>
        private async Task<bool> CheckSegmentOccupancyAsync(
            Vehicle vehicle,
            int pickupOrder,
            int dropoffOrder,
            int seatsNeeded)
        {
            // Lấy tất cả hành khách đã ghép vào xe
            var passengers = await _context.Passengers
                .Include(p => p.Group)
                .Where(p => p.MatchedVehicleId == vehicle.Id && p.IsMatched)
                .ToListAsync();

            // Tính số ghế đã sử dụng trên từng đoạn
            var segmentOccupancy = new Dictionary<string, int>(); // Key: "start-end", Value: số ghế

            foreach (var passenger in passengers)
            {
                if (!passenger.PickupOrder.HasValue || !passenger.DropoffOrder.HasValue)
                    continue;

                var pStart = passenger.PickupOrder.Value;
                var pEnd = passenger.DropoffOrder.Value;
                var seats = passenger.Group?.RequiredSeats ?? 1;

                // Tăng occupancy cho mọi đoạn từ pStart đến pEnd
                for (int i = pStart; i < pEnd; i++)
                {
                    var segmentKey = $"{i}-{i + 1}";
                    if (!segmentOccupancy.ContainsKey(segmentKey))
                        segmentOccupancy[segmentKey] = 0;
                    segmentOccupancy[segmentKey] += seats;
                }
            }

            // Kiểm tra các đoạn từ pickupOrder đến dropoffOrder
            for (int i = pickupOrder; i < dropoffOrder; i++)
            {
                var segmentKey = $"{i}-{i + 1}";
                var currentOccupancy = segmentOccupancy.ContainsKey(segmentKey) ? segmentOccupancy[segmentKey] : 0;

             
                int maxPassengerSeats = vehicle.TotalSeats > 1 ? vehicle.TotalSeats - 1 : 0;

                // Nếu (Khách hiện tại + Khách mới) > Ghế khách tối đa -> CHẶN
                if (currentOccupancy + seatsNeeded > maxPassengerSeats)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Xác định tuyến chính từ danh sách hành trình (hành trình dài nhất)
        /// </summary>
        public async Task<List<List<double>>?> DetermineMainRouteAsync(
            List<(double lat, double lon, double dropLat, double dropLon)> routes)
        {
            if (!routes.Any())
                return null;

            // Tìm hành trình dài nhất
            double maxDistance = 0;
            (double lat, double lon, double dropLat, double dropLon) longestRoute = routes[0];

            foreach (var route in routes)
            {
                var distance = await _osrmService.GetDistanceKmAsync(route.lat, route.lon, route.dropLat, route.dropLon);
                if (distance.HasValue && distance.Value > maxDistance)
                {
                    maxDistance = distance.Value;
                    longestRoute = route;
                }
            }

            // Lấy route polyline cho hành trình dài nhất
            var routeResult = await _osrmService.GetRouteAsync(
                longestRoute.lat, longestRoute.lon,
                longestRoute.dropLat, longestRoute.dropLon);

            return routeResult?.Geometry;
        }

        /// <summary>
        /// Cập nhật RoutePolyline của xe để bao phủ tất cả điểm đón/trả của hành khách
        /// Tìm điểm đón sớm nhất và điểm trả muộn nhất, sau đó tạo lại polyline từ điểm đầu đến điểm cuối
        /// </summary>
        public async Task<bool> UpdateVehicleRoutePolylineAsync(int vehicleId)
        {
            // Lấy tất cả hành khách trên xe
            var passengers = await _context.Passengers
                .Include(p => p.Group)
                .Where(p => p.MatchedVehicleId == vehicleId && p.IsMatched)
                .ToListAsync();

            if (!passengers.Any())
                return false;

            // Tìm điểm đón sớm nhất (pickup đầu tiên) và điểm trả muộn nhất (dropoff cuối cùng)
            // Sử dụng tọa độ để xác định điểm đầu và cuối của tuyến
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId);
            if (vehicle == null)
                return false;

            // Thu thập tất cả điểm đón và trả
            var allPickupPoints = passengers.ToList();
            var allDropoffPoints = passengers.ToList();

            // Khai báo biến để lưu passenger đầu/cuối (để cập nhật địa chỉ sau)
            Passenger? earliestPickupPassenger = null;
            Passenger? latestDropoffPassenger = null;

            // Nếu xe đã có RoutePolyline, sử dụng nó để xác định điểm đón sớm nhất và điểm trả muộn nhất
            (double lat, double lon) earliestPickup;
            (double lat, double lon) latestDropoff;

            if (!string.IsNullOrEmpty(vehicle.RoutePolyline))
            {
                try
                {
                    var existingGeometry = JsonConvert.DeserializeObject<List<List<double>>>(vehicle.RoutePolyline);
                    if (existingGeometry != null && existingGeometry.Any())
                    {
                        // Tìm điểm đón có PickupOrder nhỏ nhất (sớm nhất trên tuyến)
                        earliestPickupPassenger = passengers
                            .Where(p => p.PickupOrder.HasValue)
                            .OrderBy(p => p.PickupOrder.Value)
                            .FirstOrDefault();

                        // Tìm điểm trả có DropoffOrder lớn nhất (muộn nhất trên tuyến)
                        latestDropoffPassenger = passengers
                            .Where(p => p.DropoffOrder.HasValue)
                            .OrderByDescending(p => p.DropoffOrder.Value)
                            .FirstOrDefault();

                        if (earliestPickupPassenger != null && latestDropoffPassenger != null)
                        {
                            earliestPickup = (earliestPickupPassenger.PickupLatitude, earliestPickupPassenger.PickupLongitude);
                            latestDropoff = (latestDropoffPassenger.DropoffLatitude, latestDropoffPassenger.DropoffLongitude);
                        }
                        else
                        {
                            // Fallback: sử dụng logic đơn giản
                            var earliestP = allPickupPoints.OrderBy(pa => pa.PickupLatitude + pa.PickupLongitude).First();
                            earliestPickupPassenger = earliestP;
                            earliestPickup = (earliestP.PickupLatitude, earliestP.PickupLongitude);
                            var latestD = allDropoffPoints.OrderByDescending(pa => pa.DropoffLatitude + pa.DropoffLongitude).First();
                            latestDropoffPassenger = latestD;
                            latestDropoff = (latestD.DropoffLatitude, latestD.DropoffLongitude);
                        }
                    }
                    else
                    {
                        // Fallback: sử dụng logic đơn giản
                        var earliestP = allPickupPoints.OrderBy(pa => pa.PickupLatitude + pa.PickupLongitude).First();
                        earliestPickupPassenger = earliestP;
                        earliestPickup = (earliestP.PickupLatitude, earliestP.PickupLongitude);
                        var latestD = allDropoffPoints.OrderByDescending(pa => pa.DropoffLatitude + pa.DropoffLongitude).First();
                        latestDropoffPassenger = latestD;
                        latestDropoff = (latestD.DropoffLatitude, latestD.DropoffLongitude);
                    }
                }
                catch
                {
                    // Fallback: sử dụng logic đơn giản
                    var earliestP = allPickupPoints.OrderBy(pa => pa.PickupLatitude + pa.PickupLongitude).First();
                    earliestPickupPassenger = earliestP;
                    earliestPickup = (earliestP.PickupLatitude, earliestP.PickupLongitude);
                    var latestD = allDropoffPoints.OrderByDescending(pa => pa.DropoffLatitude + pa.DropoffLongitude).First();
                    latestDropoffPassenger = latestD;
                    latestDropoff = (latestD.DropoffLatitude, latestD.DropoffLongitude);
                }
            }
            else
            {
                // Không có RoutePolyline, sử dụng logic đơn giản
                var earliestP = allPickupPoints.OrderBy(pa => pa.PickupLatitude + pa.PickupLongitude).First();
                earliestPickupPassenger = earliestP;
                earliestPickup = (earliestP.PickupLatitude, earliestP.PickupLongitude);
                var latestD = allDropoffPoints.OrderByDescending(pa => pa.DropoffLatitude + pa.DropoffLongitude).First();
                latestDropoffPassenger = latestD;
                latestDropoff = (latestD.DropoffLatitude, latestD.DropoffLongitude);
            }

            // Tạo lại route polyline từ điểm đón sớm nhất đến điểm trả muộn nhất
            var routeResult = await _osrmService.GetRouteAsync(
                earliestPickup.lat, earliestPickup.lon,
                latestDropoff.lat, latestDropoff.lon);

            if (routeResult == null || !routeResult.Geometry.Any())
                return false;

            // Cập nhật RoutePolyline của xe
            vehicle.RoutePolyline = JsonConvert.SerializeObject(routeResult.Geometry);
            
            // Cập nhật điểm đón/trả của xe để tính giá chính xác
            // Sử dụng điểm đầu và cuối của route geometry (lon, lat)
            if (routeResult.Geometry.Any())
            {
                var firstPoint = routeResult.Geometry[0];
                var lastPoint = routeResult.Geometry[routeResult.Geometry.Count - 1];
                
                // OSRM trả về [lon, lat], nên [0] = lon, [1] = lat
                vehicle.PickupLongitude = firstPoint[0];
                vehicle.PickupLatitude = firstPoint[1];
                vehicle.DropoffLongitude = lastPoint[0];
                vehicle.DropoffLatitude = lastPoint[1];
                
                // Cập nhật địa chỉ nếu có
                if (earliestPickupPassenger != null)
                {
                    vehicle.PickupAddress = earliestPickupPassenger.PickupAddress;
                }
                if (latestDropoffPassenger != null)
                {
                    vehicle.DropoffAddress = latestDropoffPassenger.DropoffAddress;
                }
            }
            
            // Cập nhật lại PickupOrder và DropoffOrder cho tất cả hành khách dựa trên polyline mới
            foreach (var passenger in passengers)
            {
                var (pickupOrder, _) = FindClosestPointOnRouteWithDistance(routeResult.Geometry, passenger.PickupLatitude, passenger.PickupLongitude);
                var (dropoffOrder, _) = FindClosestPointOnRouteWithDistance(routeResult.Geometry, passenger.DropoffLatitude, passenger.DropoffLongitude);
                
                passenger.PickupOrder = pickupOrder;
                passenger.DropoffOrder = dropoffOrder;
            }

            _context.Vehicles.Update(vehicle);
            _context.Passengers.UpdateRange(passengers);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}

