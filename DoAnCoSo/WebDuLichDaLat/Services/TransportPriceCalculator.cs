using WebDuLichDaLat.Models;
using Microsoft.EntityFrameworkCore;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Service tính toán giá vận chuyển từ GPS đến Đà Lạt
    /// Hỗ trợ xử lý tỉnh thành sáp nhập
    /// </summary>
    public class TransportPriceCalculator
    {
        private readonly ApplicationDbContext _context;

        // Tọa độ Đà Lạt
        private const double DALAT_LAT = 11.9404;
        private const double DALAT_LNG = 108.4583;

        public TransportPriceCalculator(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tính giá vận chuyển từ GPS đến Đà Lạt
        /// </summary>
        public async Task<TransportPriceResult> GetFinalPriceAsync(
            double startLat,
            double startLng,
            int transportId)
        {
            var result = new TransportPriceResult
            {
                DistanceToDalat = CalculateHaversineDistance(startLat, startLng, DALAT_LAT, DALAT_LNG)
            };

            // BƯỚC 1: Lấy thông tin phương tiện
            var transport = await _context.TransportOptions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == transportId);

            if (transport == null)
            {
                result.Price = 0;
                result.PriceType = "Error";
                result.Note = "Không tìm thấy thông tin phương tiện.";
                return result;
            }

            // ✅ FIX: Xe máy và ô tô cá nhân (IsSelfDrive = True) không lấy giá từ database
            // mà tính theo FuelConsumption và FuelPrice
            if (transport.IsSelfDrive)
            {
                // Ưu tiên 1: Tính theo định mức nhiên liệu (Chính xác nhất)
                if (transport.FuelConsumption > 0 && transport.FuelPrice > 0)
                {
                    // Tính chi phí nhiên liệu theo khoảng cách thực tế
                    decimal fuelUsed = (decimal)result.DistanceToDalat * transport.FuelConsumption / 100m;
                    decimal fuelCost = fuelUsed * transport.FuelPrice;

                    // Chi phí bảo dưỡng và hao mòn (20% chi phí nhiên liệu)
                    decimal maintenanceCost = fuelCost * 0.2m;

                    result.Price = fuelCost + maintenanceCost;
                    result.PriceType = "Calculated";
                    result.Note = $"Chi phí tính theo nhiên liệu: {result.DistanceToDalat:F1}km × " +
                                $"{transport.FuelConsumption}l/100km × {transport.FuelPrice:N0}đ/lít + " +
                                $"bảo dưỡng (20%) = {result.Price:N0}đ";

                    // Vẫn lưu thông tin location nếu có (để hiển thị)
                    var nearestLocation = await FindNearestLocationAsync(startLat, startLng, maxRadiusKm: 50);
                    if (nearestLocation != null)
                    {
                        result.LocationName = nearestLocation.CurrentName;
                        result.LocationId = nearestLocation.Id;
                        result.OldLocationName = nearestLocation.OldName;
                        result.IsMergedLocation = nearestLocation.IsMergedLocation;
                        result.DistanceFromLocation = CalculateHaversineDistance(
                            startLat, startLng,
                            nearestLocation.Latitude, nearestLocation.Longitude
                        );
                    }

                    return result;
                }
                else
                {
                    // Ưu tiên 2 (Fallback): Nếu thiếu dữ liệu xăng, dùng đơn giá thấp
                    // Xe máy: ~600đ/km | Ô tô: ~1,800đ/km
                    decimal fallbackRate = transport.Name?.ToLower().Contains("máy") == true ? 600m : 1800m;
                    result.Price = (decimal)result.DistanceToDalat * fallbackRate;
                    result.PriceType = "Estimated";
                    result.Note = $"Chi phí ước tính (thiếu dữ liệu nhiên liệu): {result.DistanceToDalat:F1}km × " +
                                $"{fallbackRate:N0}đ/km = {result.Price:N0}đ. " +
                                $"Vui lòng cập nhật thông tin nhiên liệu để tính chính xác hơn.";

                    // Vẫn lưu thông tin location nếu có
                    var nearestLocation = await FindNearestLocationAsync(startLat, startLng, maxRadiusKm: 50);
                    if (nearestLocation != null)
                    {
                        result.LocationName = nearestLocation.CurrentName;
                        result.LocationId = nearestLocation.Id;
                        result.OldLocationName = nearestLocation.OldName;
                        result.IsMergedLocation = nearestLocation.IsMergedLocation;
                        result.DistanceFromLocation = CalculateHaversineDistance(
                            startLat, startLng,
                            nearestLocation.Latitude, nearestLocation.Longitude
                        );
                    }

                    return result;
                }
            }

            // BƯỚC 2: Tìm location gần nhất (trong bán kính 50km) - chỉ cho phương tiện công cộng
            var nearestLocationForPublic = await FindNearestLocationAsync(startLat, startLng, maxRadiusKm: 50);

            if (nearestLocationForPublic != null)
            {
                result.LocationName = nearestLocationForPublic.CurrentName;
                result.LocationId = nearestLocationForPublic.Id;
                result.OldLocationName = nearestLocationForPublic.OldName;
                result.IsMergedLocation = nearestLocationForPublic.IsMergedLocation;
                result.DistanceFromLocation = CalculateHaversineDistance(
                    startLat, startLng,
                    nearestLocationForPublic.Latitude, nearestLocationForPublic.Longitude
                );

                // BƯỚC 3: Thử lấy giá cố định từ DB (chỉ cho phương tiện công cộng)
                var fixedPrice = await GetFixedPriceAsync(nearestLocationForPublic.Id, transportId);

                if (fixedPrice.HasValue && fixedPrice > 0)
                {
                    // ✅ QUAN TRỌNG: Giá vé xe công cộng phải tính khứ hồi (x2)
                    // Vì người dùng cần đi và về
                    result.Price = fixedPrice.Value * 2;
                    result.PriceType = "Fixed";

                    // Tạo note rõ ràng với thông tin sáp nhập
                    if (nearestLocationForPublic.IsMergedLocation)
                    {
                        result.Note = $"Giá khứ hồi từ {nearestLocationForPublic.CurrentName} " +
                                    $"(khu vực {nearestLocationForPublic.OldName} cũ) đến Đà Lạt. " +
                                    $"Giá cố định từ nhà xe: {fixedPrice.Value:N0}đ/chiều × 2 = {result.Price:N0}đ.";

                        if (!string.IsNullOrEmpty(nearestLocationForPublic.MergeNote))
                        {
                            result.Note += $" {nearestLocationForPublic.MergeNote}";
                        }
                    }
                    else
                    {
                        result.Note = $"Giá khứ hồi từ {nearestLocationForPublic.CurrentName} đến Đà Lạt. " +
                                    $"Giá cố định từ nhà xe: {fixedPrice.Value:N0}đ/chiều × 2 = {result.Price:N0}đ.";
                    }

                    return result;
                }
            }

            // BƯỚC 4: Không có giá cố định → Tính theo khoảng cách (cho phương tiện công cộng)
            decimal oneWayPrice = CalculateByDistance(result.DistanceToDalat, transport);
            // ✅ QUAN TRỌNG: Giá vé xe công cộng phải tính khứ hồi (x2)
            result.Price = oneWayPrice * 2;
            result.PriceType = "Calculated";

            if (nearestLocationForPublic != null && nearestLocationForPublic.IsMergedLocation)
            {
                result.Note = $"Giá khứ hồi ước tính từ khu vực {nearestLocationForPublic.OldName} " +
                            $"(hiện tại: {nearestLocationForPublic.CurrentName}) đến Đà Lạt, " +
                            $"khoảng cách {result.DistanceToDalat:F1}km. " +
                            $"Giá một chiều: {oneWayPrice:N0}đ × 2 = {result.Price:N0}đ. " +
                            $"Vui lòng liên hệ nhà xe để biết giá chính xác.";
            }
            else
            {
                result.Note = $"Giá khứ hồi ước tính dựa trên khoảng cách {result.DistanceToDalat:F1}km. " +
                             $"Giá một chiều: {oneWayPrice:N0}đ × 2 = {result.Price:N0}đ. " +
                             $"Vui lòng liên hệ nhà xe để biết giá chính xác.";
            }

            return result;
        }

        /// <summary>
        /// Tìm location gần nhất trong bán kính maxRadiusKm
        /// </summary>
        private async Task<LegacyLocation?> FindNearestLocationAsync(
            double lat,
            double lng,
            double maxRadiusKm = 50)
        {
            var allLocations = await _context.LegacyLocations
                .AsNoTracking()
                .Where(l => l.IsActive)
                .ToListAsync();

            if (!allLocations.Any())
                return null;

            var nearestLocation = allLocations
                .Select(loc => new
                {
                    Location = loc,
                    Distance = CalculateHaversineDistance(lat, lng, loc.Latitude, loc.Longitude)
                })
                .Where(x => x.Distance <= maxRadiusKm)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            return nearestLocation?.Location;
        }

        /// <summary>
        /// Lấy giá cố định từ TransportPriceHistory
        /// </summary>
        private async Task<decimal?> GetFixedPriceAsync(int locationId, int transportId)
        {
            return await _context.TransportPriceHistories
                .AsNoTracking()
                .Where(p => p.LegacyLocationId == locationId
                         && p.TransportOptionId == transportId)
                .Select(p => (decimal?)p.Price)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Tính giá theo khoảng cách (fallback)
        /// CẬP NHẬT: Đơn giá thực tế hơn dựa trên thị trường
        /// </summary>
        private decimal CalculateByDistance(double distanceKm, TransportOption? transport)
        {
            // CẬP NHẬT ĐƠN GIÁ THỰC TẾ HƠN
            decimal pricePerKm = transport?.Type switch
            {
                // Xe khách: Ước tính khoảng 1,000đ - 1,200đ/km (200km ~ 240k)
                "Public" => 1200m,

                // Xe limousine/Taxi bao chuyến: Giảm nhẹ từ 4000 xuống 3500
                "Private" => 3500m,

                // Xe máy/Ô tô cá nhân (trong trường hợp rơi vào fallback): Chỉ tính tiền xăng ~1,500đ
                "SelfDrive" => 1500m,

                // Mặc định: Giảm từ 3000 xuống 2000
                _ => 2000m
            };

            // Giảm giá cho quãng đường xa
            if (distanceKm > 300)
                pricePerKm *= 0.85m;    // Giảm 15%
            else if (distanceKm > 200)
                pricePerKm *= 0.9m;     // Giảm 10%

            // Thêm phí mở cửa/phí cơ bản cho Public để tránh giá quá rẻ khi đi ngắn
            decimal baseFare = transport?.Type == "Public" ? 50000m : 0;

            return baseFare + ((decimal)distanceKm * pricePerKm);
        }

        /// <summary>
        /// Tính khoảng cách Haversine giữa 2 điểm GPS
        /// </summary>
        private double CalculateHaversineDistance(
            double lat1, double lng1,
            double lat2, double lng2)
        {
            const double R = 6371; // Bán kính Trái Đất (km)

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLng = DegreesToRadians(lng2 - lng1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) *
                    Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }
    }
}

