using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using WebDuLichDaLat.Models;
using WebDuLichDaLat.Services;

namespace WebDuLichDaLat.Controllers
{
    public class CarpoolController : Controller
    {
        private static readonly int[] SupportedCapacities = new[] { 4, 7, 9 }; // 4 chỗ = 3 ghế trống, 7 chỗ = 6 ghế trống, 9 chỗ = 8 ghế trống
        private const int MaxPassengersPerRequest = 16; // Giới hạn tối đa 16 người
        private static readonly TimeSpan TimeWindow = TimeSpan.FromHours(1);
        
        // ============================================
        // CÔNG THỨC TÍNH GIÁ XE MỚI - ĐƯỢC QUẢN LÝ TRONG DATABASE
        // ============================================
        // Giá nhiên liệu, lương tài xế, phí cầu đường được lưu trong bảng VehiclePricingConfigs
        // Mỗi loại xe (4, 7, 9 chỗ) có giá khác nhau:
        // - Xe lớn hơn = giá nhiên liệu cao hơn = lương tài xế cao hơn
        
        // Giữ lại PricePerKm để tương thích với code cũ (sẽ không dùng trong tính giá mới)
        private const decimal PricePerKm = 15000m;

        private static readonly (string Name, string Phone, string Plate)[] DriverPool = new[]
        {
            ("Nguyễn Văn An", "0909 123 456", "49A-123.45"),
            ("Trần Quốc Bảo", "0912 456 789", "49B-678.90"),
            ("Phạm Minh Châu", "0933 888 222", "49D-456.12"),
            ("Lê Hải Đăng", "0977 111 333", "49F-234.56")
        };
        private static int _driverAssignmentIndex = 0;

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly OsrmRouteService _osrmService;
        private readonly RouteMatchingService _routeMatchingService;

        public CarpoolController(ApplicationDbContext context, IConfiguration config, OsrmRouteService osrmService, RouteMatchingService routeMatchingService)
        {
            _context = context;
            _config = config;
            _osrmService = osrmService;
            _routeMatchingService = routeMatchingService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new CarpoolViewModel
            {
                PreferredDepartureTime = DateTime.Now.AddHours(1),
                RequestedVehicleSeats = SupportedCapacities[0],
                OpenTrips = await GetOpenTripsAsync(),
                StatusMessage = TempData["CarpoolStatus"] as string
            };

            // Nếu có assigned trips từ lần submit trước, load lại
            if (TempData["ShowAssignedTrips"] != null && TempData["AssignedTripIds"] != null)
            {
                var tripIdsString = TempData["AssignedTripIds"].ToString();
                if (!string.IsNullOrEmpty(tripIdsString))
                {
                    var tripIds = tripIdsString.Split(',').Select(int.Parse).ToList();
                    var assignedTrips = new List<CarpoolTripInfo>();
                    
                    foreach (var tripId in tripIds)
                    {
                        var tripInfo = await BuildTripInfo(tripId, null);
                        if (tripInfo != null)
                        {
                            assignedTrips.Add(tripInfo);
                        }
                    }
                    
                    if (assignedTrips.Any())
                    {
                        model.AssignedTrips = assignedTrips;
                        if (assignedTrips.Count == 1)
                        {
                            model.AssignedTrip = assignedTrips[0];
                        }
                    }
                }
            }

            ViewBag.GoogleMapsApiKey = _config["Google:MapsApiKey"] ?? "";
            return View(model);
        }

        /// <summary>
        /// API endpoint để tìm chuyến phù hợp (dùng cho đếm ngược 15 giây)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> FindMatchingTrip([FromBody] FindTripRequest request)
        {
            if (request == null || !HasValidCoordinates(request.PickupLat, request.PickupLon) || 
                !HasValidCoordinates(request.DropoffLat, request.DropoffLon))
            {
                return Json(new { success = false, message = "Tọa độ không hợp lệ" });
            }

            int seatsNeeded = Math.Max(1, Math.Min(MaxPassengersPerRequest, request.SeatsNeeded));
            int normalizedCapacity = DetermineVehicleCapacity(seatsNeeded, request.VehicleSeats);

            var windowStart = request.DepartureTime.AddHours(-1);
            var windowEnd = request.DepartureTime.AddHours(1);

            // Tìm xe phù hợp
            var candidateTrips = await _context.Vehicles
                .Where(v => v.IsActive &&
                            v.TotalSeats == normalizedCapacity &&
                            v.AvailableSeats >= seatsNeeded &&
                            v.DepartureTime >= windowStart &&
                            v.DepartureTime <= windowEnd &&
                            v.VehicleType == "Ghép xe tự động")
                .ToListAsync();

            var matchResults = new List<object>();
            
            foreach (var candidate in candidateTrips)
            {
                var matchResult = await _routeMatchingService.TryMatchRouteAsync(
                    candidate,
                    request.PickupLat, request.PickupLon,
                    request.DropoffLat, request.DropoffLon,
                    seatsNeeded);

                if (matchResult.CanMatch)
                {
                    // Tính khoảng cách đến đích
                    double endDistance = 0;
                    if (!string.IsNullOrEmpty(candidate.RoutePolyline))
                    {
                        try
                        {
                            var geometry = JsonConvert.DeserializeObject<List<List<double>>>(candidate.RoutePolyline);
                            if (geometry != null && geometry.Any())
                            {
                                var lastPoint = geometry.Last();
                                if (lastPoint.Count >= 2)
                                {
                                    endDistance = CalculateDistanceKm(
                                        lastPoint[1], lastPoint[0],
                                        request.DropoffLat, request.DropoffLon);
                                }
                            }
                        }
                        catch { }
                    }

                    matchResults.Add(new
                    {
                        tripId = candidate.Id,
                        pickupOrder = matchResult.PickupOrder,
                        dropoffOrder = matchResult.DropoffOrder,
                        endDistance = Math.Round(endDistance, 2),
                        availableSeats = candidate.AvailableSeats
                    });
                }
            }

            // Sắp xếp theo ưu tiên
            var bestMatch = matchResults
                .OrderBy(m => ((dynamic)m).endDistance)
                .ThenBy(m => ((dynamic)m).availableSeats)
                .FirstOrDefault();

            return Json(new { success = bestMatch != null, match = bestMatch, allMatches = matchResults });
        }

        public class FindTripRequest
        {
            public double PickupLat { get; set; }
            public double PickupLon { get; set; }
            public double DropoffLat { get; set; }
            public double DropoffLon { get; set; }
            public int SeatsNeeded { get; set; }
            public int VehicleSeats { get; set; }
            public DateTime DepartureTime { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Index(CarpoolViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.GoogleMapsApiKey = _config["Google:MapsApiKey"] ?? "";
                model.OpenTrips = await GetOpenTripsAsync();
                return View(model);
            }

            // --- Xử lý tọa độ & Validate (Giữ nguyên logic cũ, chỉ thu gọn code để bạn dễ nhìn) ---
            try
            {
                if (!HasValidCoordinates(model.PickupLatitude, model.PickupLongitude))
                {
                    var (pickupLat, pickupLon) = await GeocodeAddress(model.PickupAddress);
                    model.PickupLatitude = pickupLat; model.PickupLongitude = pickupLon;
                }
                if (!HasValidCoordinates(model.DropoffLatitude, model.DropoffLongitude))
                {
                    var (dropoffLat, dropoffLon) = await GeocodeAddress(model.DropoffAddress);
                    model.DropoffLatitude = dropoffLat; model.DropoffLongitude = dropoffLon;
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Lỗi lấy tọa độ: {ex.Message}");
                ViewBag.GoogleMapsApiKey = _config["Google:MapsApiKey"] ?? "";
                model.OpenTrips = await GetOpenTripsAsync();
                return View(model);
            }

            // --- Bắt đầu Logic chính ---
            // Kiểm tra giới hạn 16 người
            if (model.NumberOfPassengers > MaxPassengersPerRequest)
            {
                ModelState.AddModelError("NumberOfPassengers", $"Số lượng người không được vượt quá {MaxPassengersPerRequest} người. Vui lòng liên hệ trực tiếp để đặt xe cho nhóm lớn hơn.");
                ViewBag.GoogleMapsApiKey = _config["Google:MapsApiKey"] ?? "";
                model.OpenTrips = await GetOpenTripsAsync();
                return View(model);
            }

            int seatsNeeded = Math.Max(1, Math.Min(MaxPassengersPerRequest, model.NumberOfPassengers));
            int normalizedCapacity = DetermineVehicleCapacity(seatsNeeded, model.RequestedVehicleSeats);
            model.RequestedVehicleSeats = normalizedCapacity;

            // Validate số điện thoại
            var rawPhone = model.PhoneNumber ?? string.Empty;
            var phoneNumber = new string(rawPhone.Where(c => char.IsDigit(c) || c == '+' || c == '-').ToArray());
            if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length < 7)
            {
                ModelState.AddModelError("PhoneNumber", "Vui lòng nhập số điện thoại hợp lệ.");
                ViewBag.GoogleMapsApiKey = _config["Google:MapsApiKey"] ?? "";
                model.OpenTrips = await GetOpenTripsAsync();
                return View(model);
            }
            if (phoneNumber.Length > 20) phoneNumber = phoneNumber.Substring(0, 20);

            // 🔒 KIỂM TRA DUPLICATE: Chỉ kiểm tra trùng hoàn toàn (tên + số điện thoại + điểm đón + điểm đến + thời gian) trong 5 phút
            // Cho phép dùng lại số điện thoại để đặt thêm chuyến khác
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
            var departureTimeWindowStart = model.PreferredDepartureTime.AddMinutes(-5);
            var departureTimeWindowEnd = model.PreferredDepartureTime.AddMinutes(5);
            
            var duplicateCheck = await _context.Passengers
                .Where(p => p.Name == model.PassengerName &&
                           p.PhoneNumber == phoneNumber &&
                           p.PickupAddress == model.PickupAddress &&
                           p.DropoffAddress == model.DropoffAddress &&
                           p.PreferredDepartureTime >= departureTimeWindowStart &&
                           p.PreferredDepartureTime <= departureTimeWindowEnd &&
                           p.CreatedAt >= fiveMinutesAgo &&
                           p.IsMatched == true)
                .FirstOrDefaultAsync();

            if (duplicateCheck != null)
            {
                ModelState.AddModelError(string.Empty, "Bạn đã đăng ký chuyến xe giống hệt này trong vòng 5 phút gần đây. Vui lòng đợi một chút hoặc kiểm tra lại chuyến xe của bạn.");
                ViewBag.GoogleMapsApiKey = _config["Google:MapsApiKey"] ?? "";
                model.OpenTrips = await GetOpenTripsAsync();
                return View(model);
            }

            var windowStart = model.PreferredDepartureTime - TimeWindow;
            var windowEnd = model.PreferredDepartureTime + TimeWindow;

            // 🔥 LOGIC TỰ ĐỘNG CHIA NHÓM: Ưu tiên xe lớn nhất trước
            // Ví dụ: 10 người -> 8 người vào xe 9 chỗ (8 ghế trống), 2 người vào xe 4 chỗ (3 ghế trống)
            // Ví dụ: 24 người -> 3 xe 9 chỗ, mỗi xe 8 người
            var assignedTrips = new List<CarpoolTripInfo>();
            int remainingSeats = seatsNeeded;
            int groupIndex = 1;

            // 🔒 LOGIC NHÓM RIÊNG: Khi PrivateGroup = true, tạo xe riêng ngay, không tìm xe khác, không ghép thêm
            if (model.PrivateGroup)
            {
                // Tạo xe riêng cho toàn bộ số người (có thể chia thành nhiều xe nếu số người > 8)
                while (remainingSeats > 0)
                {
                    // Xác định loại xe phù hợp cho số ghế còn lại
                    int capacityForNewVehicle = DetermineVehicleCapacity(remainingSeats, model.RequestedVehicleSeats);
                    var driver = GetNextDriver();
                    var routeResult = await _osrmService.GetRouteAsync(model.PickupLatitude, model.PickupLongitude, model.DropoffLatitude, model.DropoffLongitude);

                    string? routePolyline = null;
                    int? pickupOrder = null;
                    int? dropoffOrder = null;
                    if (routeResult != null && routeResult.Geometry.Any())
                    {
                        routePolyline = JsonConvert.SerializeObject(routeResult.Geometry);
                        pickupOrder = 0;
                        dropoffOrder = routeResult.Geometry.Count - 1;
                    }

                    int maxPassengerSeats = capacityForNewVehicle - 1; // Trừ 1 ghế tài xế
                    int seatsForThisTrip = Math.Min(remainingSeats, maxPassengerSeats);

                    var privateVehicle = new Vehicle
                    {
                        DriverName = $"{driver.Name}|{driver.Phone}",
                        LicensePlate = driver.Plate,
                        VehicleType = "Ghép xe tự động",
                        PickupAddress = model.PickupAddress,
                        PickupLatitude = model.PickupLatitude,
                        PickupLongitude = model.PickupLongitude,
                        DropoffAddress = model.DropoffAddress,
                        DropoffLatitude = model.DropoffLatitude,
                        DropoffLongitude = model.DropoffLongitude,
                        DepartureTime = model.PreferredDepartureTime,
                        TotalSeats = capacityForNewVehicle,
                        AvailableSeats = 0, // Xe riêng, không có ghế trống
                        CostPerKm = PricePerKm,
                        FixedPrice = await CalculateTripCostAsync(model.PickupLatitude, model.PickupLongitude, model.DropoffLatitude, model.DropoffLongitude, capacityForNewVehicle),
                        RoutePolyline = routePolyline,
                        IsActive = false, // Xe riêng, không cho ghép thêm
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Vehicles.Add(privateVehicle);
                    await _context.SaveChangesAsync();

                    // Tạo PassengerGroup (tạo khi có 2+ ghế hoặc khi IsGroup = true và seatsForThisTrip > 0)
                    PassengerGroup? currentGroup = null;
                    if (seatsForThisTrip > 0 && (seatsForThisTrip > 1 || model.IsGroup))
                    {
                        currentGroup = new PassengerGroup 
                        { 
                            GroupName = $"Nhóm riêng {model.PassengerName}" + (groupIndex > 1 ? $" - Phần {groupIndex}" : ""), 
                            RequiredSeats = seatsForThisTrip, 
                            PrivateGroup = true 
                        };
                        _context.PassengerGroups.Add(currentGroup);
                        await _context.SaveChangesAsync();
                    }

                    // Số điện thoại đã được validate ở trên

                    // Tính giá cho nhóm này
                    var estimatedDistance = await _osrmService.GetDistanceKmAsync(model.PickupLatitude, model.PickupLongitude, model.DropoffLatitude, model.DropoffLongitude);
                    decimal? estimatedCost = null;
                    decimal? costPerSeat = null;

                    if (privateVehicle.FixedPrice.HasValue && privateVehicle.FixedPrice > 0)
                    {
                        double passengerDist = (estimatedDistance.HasValue && estimatedDistance > 0) ? estimatedDistance.Value : 0;
                        double tripTotalDistanceKm = passengerDist > 0 ? passengerDist : 300;
                        if (tripTotalDistanceKm <= 0) tripTotalDistanceKm = 300;

                        int sellableSeats = privateVehicle.TotalSeats > 1 ? privateVehicle.TotalSeats - 1 : 1;
                        decimal pricePerFullSeat = privateVehicle.FixedPrice.Value / sellableSeats;
                        double ratio = Math.Min(passengerDist / tripTotalDistanceKm, 1.0);
                        decimal finalTotalCost = (pricePerFullSeat * (decimal)ratio) * seatsForThisTrip;

                        estimatedCost = Math.Round(finalTotalCost / 1000) * 1000;
                        costPerSeat = seatsForThisTrip > 0 ? estimatedCost / seatsForThisTrip : 0;
                    }

                    // Tạo Passenger
                    var passenger = new Passenger
                    {
                        Name = model.PassengerName,
                        PhoneNumber = phoneNumber,
                        PickupLatitude = model.PickupLatitude,
                        PickupLongitude = model.PickupLongitude,
                        PickupAddress = model.PickupAddress,
                        DropoffLatitude = model.DropoffLatitude,
                        DropoffLongitude = model.DropoffLongitude,
                        DropoffAddress = model.DropoffAddress,
                        PreferredDepartureTime = model.PreferredDepartureTime,
                        GroupId = currentGroup?.Id,
                        IsMatched = true,
                        MatchedVehicleId = privateVehicle.Id,
                        PickupOrder = pickupOrder,
                        DropoffOrder = dropoffOrder
                    };
                    _context.Passengers.Add(passenger);
                    await _context.SaveChangesAsync();

                    // Lưu CompletedTrip vì xe riêng coi như đã đầy
                    await SaveCompletedTripAsync(privateVehicle.Id);
                    
                    // Tạo TripInfo cho nhóm này
                    var tripInfo = await BuildTripInfo(privateVehicle.Id, passenger.Id);
                    assignedTrips.Add(tripInfo);

                    // Giảm số ghế còn lại
                    remainingSeats -= seatsForThisTrip;
                    groupIndex++;
                }

                // Trả về kết quả cho nhóm riêng - Lưu vào TempData và redirect để reset form
                var privateGroupMessage = assignedTrips.Count == 1
                    ? "✅ Đã tạo chuyến xe riêng thành công! Xe của bạn sẽ không ghép thêm người khác."
                    : $"✅ Đã tạo {assignedTrips.Count} chuyến xe riêng thành công! Các xe của bạn sẽ không ghép thêm người khác.";
                
                // Lưu trip IDs để load lại trong GET
                var privateTripIds = assignedTrips.Select(t => t.TripId).ToList();
                TempData["CarpoolStatus"] = privateGroupMessage;
                TempData["AssignedTripIds"] = string.Join(",", privateTripIds);
                TempData["ShowAssignedTrips"] = true;
                
                return RedirectToAction(nameof(Index));
            }

            // Lặp để ghép từng nhóm cho đến khi hết người (Logic ghép xe thông thường)
            while (remainingSeats > 0)
            {
                // 1. Lấy danh sách xe có sẵn
                var candidateTrips = await _context.Vehicles
                    .Where(v => v.IsActive &&
                                v.DepartureTime >= windowStart && v.DepartureTime <= windowEnd &&
                                v.VehicleType == "Ghép xe tự động")
                    .ToListAsync();

                Vehicle? currentTargetTrip = null;
                int? currentPickupOrder = null;
                int? currentDropoffOrder = null;
                int seatsForThisTrip = 0;

                if (!model.IsGroup)
                {
                    // 2. Tìm xe phù hợp: Logic thông minh - ưu tiên xe nhỏ nhất có thể chứa đủ số ghế còn lại
                    // Ví dụ: 11 người -> ưu tiên 9 chỗ (8 ghế) cho 8 người đầu, sau đó 4 chỗ (3 ghế) cho 3 người còn lại
                    // Ví dụ: 12 người -> ưu tiên 9 chỗ (8 ghế) cho 8 người đầu, sau đó 7 chỗ (6 ghế) cho 4 người còn lại
                    
                    // Tìm xe nhỏ nhất có thể chứa đủ số ghế còn lại
                    // Nếu không có xe nhỏ nào chứa đủ, mới dùng xe lớn hơn
                    var capacitiesOrdered = SupportedCapacities.OrderBy(c => c); // Sắp xếp tăng dần: 4, 6, 9

                    Vehicle? foundVehicle = null;
                    RouteMatchResult? foundMatchResult = null;

                    // Bước 1: Tìm xe nhỏ nhất có thể chứa đủ số ghế còn lại
                    foreach (var capacity in capacitiesOrdered)
                    {
                        int availableSeatsForCapacity = capacity - 1; // Trừ 1 ghế tài xế
                        
                        // Nếu số ghế còn lại <= số ghế trống của xe này, xe này có thể chứa đủ
                        if (remainingSeats <= availableSeatsForCapacity)
                        {
                            int seatsToMatch = remainingSeats;

                            // Tìm xe có capacity này và có đủ chỗ
                            var candidates = candidateTrips
                                .Where(v => v.TotalSeats == capacity && v.AvailableSeats >= seatsToMatch)
                                .ToList();

                            var matchResults = new List<(Vehicle vehicle, RouteMatchResult result)>();
                            foreach (var candidate in candidates)
                            {
                                var matchResult = await _routeMatchingService.TryMatchRouteAsync(
                                    candidate, model.PickupLatitude, model.PickupLongitude,
                                    model.DropoffLatitude, model.DropoffLongitude, seatsToMatch);

                                if (matchResult.CanMatch)
                                {
                                    matchResults.Add((candidate, matchResult));
                                }
                            }

                            // Chọn xe có ít ghế trống nhất (để tối ưu việc sử dụng xe)
                            var bestMatch = matchResults.OrderBy(m => m.vehicle.AvailableSeats).FirstOrDefault();
                            if (bestMatch.vehicle != null)
                            {
                                foundVehicle = bestMatch.vehicle;
                                foundMatchResult = bestMatch.result;
                                break; // Tìm thấy xe nhỏ nhất phù hợp, dừng lại
                            }
                        }
                    }

                    // Bước 2: Nếu không tìm thấy xe nhỏ phù hợp, dùng xe lớn nhất có thể chứa một phần số ghế còn lại
                    // (Ưu tiên xe lớn để tận dụng tối đa)
                    if (foundVehicle == null)
                    {
                        var capacitiesOrderedDesc = SupportedCapacities.OrderByDescending(c => c - 1); // Sắp xếp giảm dần: 9, 6, 4

                        foreach (var capacity in capacitiesOrderedDesc)
                        {
                            int availableSeatsForCapacity = capacity - 1; // Trừ 1 ghế tài xế
                            int seatsToMatch = Math.Min(remainingSeats, availableSeatsForCapacity);

                            // Tìm xe có capacity này và có đủ chỗ
                            var candidates = candidateTrips
                                .Where(v => v.TotalSeats == capacity && v.AvailableSeats >= seatsToMatch)
                                .ToList();

                            var matchResults = new List<(Vehicle vehicle, RouteMatchResult result)>();
                            foreach (var candidate in candidates)
                            {
                                var matchResult = await _routeMatchingService.TryMatchRouteAsync(
                                    candidate, model.PickupLatitude, model.PickupLongitude,
                                    model.DropoffLatitude, model.DropoffLongitude, seatsToMatch);

                                if (matchResult.CanMatch)
                                {
                                    matchResults.Add((candidate, matchResult));
                                }
                            }

                            // Chọn xe có ít ghế trống nhất (để tối ưu việc sử dụng xe)
                            var bestMatch = matchResults.OrderBy(m => m.vehicle.AvailableSeats).FirstOrDefault();
                            if (bestMatch.vehicle != null)
                            {
                                foundVehicle = bestMatch.vehicle;
                                foundMatchResult = bestMatch.result;
                                break; // Tìm thấy xe phù hợp, dừng lại
                            }
                        }
                    }

                    if (foundVehicle != null && foundMatchResult != null)
                    {
                        currentTargetTrip = foundVehicle;
                        currentPickupOrder = foundMatchResult.PickupOrder;
                        currentDropoffOrder = foundMatchResult.DropoffOrder;
                        // Số ghế có thể ghép vào xe này
                        int maxPassengerSeats = currentTargetTrip.TotalSeats - 1;
                        seatsForThisTrip = Math.Min(remainingSeats, maxPassengerSeats);
                    }
                }

                // 3. Nếu vẫn không tìm thấy xe -> Tạo xe mới (Xe ảo)
                if (currentTargetTrip == null)
                {
                    // Xác định loại xe phù hợp cho số ghế còn lại (ưu tiên xe lớn nhất có đủ chỗ)
                    // Ví dụ: 8 người -> xe 9 chỗ (8 ghế trống), 6 người -> xe 7 chỗ (6 ghế trống), 3 người -> xe 4 chỗ (3 ghế trống)
                    int capacityForNewVehicle = DetermineVehicleCapacity(remainingSeats, 0);
                    var driver = GetNextDriver();
                    var routeResult = await _osrmService.GetRouteAsync(model.PickupLatitude, model.PickupLongitude, model.DropoffLatitude, model.DropoffLongitude);

                    string? routePolyline = null;
                    if (routeResult != null && routeResult.Geometry.Any())
                    {
                        routePolyline = JsonConvert.SerializeObject(routeResult.Geometry);
                        currentPickupOrder = 0;
                        currentDropoffOrder = routeResult.Geometry.Count - 1;
                    }

                    currentTargetTrip = new Vehicle
                    {
                        DriverName = $"{driver.Name}|{driver.Phone}",
                        LicensePlate = driver.Plate,
                        VehicleType = "Ghép xe tự động",
                        PickupAddress = model.PickupAddress,
                        PickupLatitude = model.PickupLatitude,
                        PickupLongitude = model.PickupLongitude,
                        DropoffAddress = model.DropoffAddress,
                        DropoffLatitude = model.DropoffLatitude,
                        DropoffLongitude = model.DropoffLongitude,
                        DepartureTime = model.PreferredDepartureTime,
                        TotalSeats = capacityForNewVehicle,
                        AvailableSeats = capacityForNewVehicle - 1, // Sẽ được tính lại sau
                        CostPerKm = PricePerKm,
                        FixedPrice = await CalculateTripCostAsync(model.PickupLatitude, model.PickupLongitude, model.DropoffLatitude, model.DropoffLongitude, capacityForNewVehicle),
                        RoutePolyline = routePolyline,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Vehicles.Add(currentTargetTrip);
                    await _context.SaveChangesAsync(); // Lưu để có ID
                    
                    // Xác định số ghế có thể ghép vào xe mới này
                    int maxPassengerSeats = currentTargetTrip.TotalSeats - 1;
                    seatsForThisTrip = Math.Min(remainingSeats, maxPassengerSeats);
                }

                // 4. Tạo PassengerGroup và Passenger cho nhóm này
                // Tạo PassengerGroup khi có 2+ ghế hoặc khi IsGroup = true và seatsForThisTrip > 0
                PassengerGroup? currentGroup = null;
                if (seatsForThisTrip > 0 && (seatsForThisTrip > 1 || model.IsGroup))
                {
                    currentGroup = new PassengerGroup 
                    { 
                        GroupName = $"Nhóm {model.PassengerName} - Phần {groupIndex}", 
                        RequiredSeats = seatsForThisTrip, 
                        PrivateGroup = model.PrivateGroup 
                    };
                    _context.PassengerGroups.Add(currentGroup);
                    await _context.SaveChangesAsync();
                }

                    // Số điện thoại đã được validate ở trên

                // Tính giá cho nhóm này
                var estimatedDistance = await _osrmService.GetDistanceKmAsync(model.PickupLatitude, model.PickupLongitude, model.DropoffLatitude, model.DropoffLongitude);
                decimal? estimatedCost = null;
                decimal? costPerSeat = null;

                if (currentTargetTrip.FixedPrice.HasValue && currentTargetTrip.FixedPrice > 0)
                {
                    double passengerDist = (estimatedDistance.HasValue && estimatedDistance > 0) ? estimatedDistance.Value : 0;
                    bool isNewVirtualTrip = (DateTime.UtcNow - currentTargetTrip.CreatedAt).TotalSeconds < 10;
                    double tripTotalDistanceKm = 300;

                    if (isNewVirtualTrip)
                    {
                        tripTotalDistanceKm = passengerDist;
                    }
                    else
                    {
                        var tripDistResult = await _osrmService.GetDistanceKmAsync(
                            currentTargetTrip.PickupLatitude, currentTargetTrip.PickupLongitude,
                            currentTargetTrip.DropoffLatitude, currentTargetTrip.DropoffLongitude);
                        tripTotalDistanceKm = (tripDistResult.HasValue && tripDistResult.Value > 0) ? tripDistResult.Value : 300;
                    }
                    if (tripTotalDistanceKm <= 0) tripTotalDistanceKm = 300;

                    int sellableSeats = currentTargetTrip.TotalSeats > 1 ? currentTargetTrip.TotalSeats - 1 : 1;
                    decimal pricePerFullSeat = currentTargetTrip.FixedPrice.Value / sellableSeats;
                    double ratio = Math.Min(passengerDist / tripTotalDistanceKm, 1.0);
                    decimal finalTotalCost = (pricePerFullSeat * (decimal)ratio) * seatsForThisTrip;

                    estimatedCost = Math.Round(finalTotalCost / 1000) * 1000;
                    costPerSeat = seatsForThisTrip > 0 ? estimatedCost / seatsForThisTrip : 0;
                }

                // Tạo Passenger
                var passenger = new Passenger
                {
                    Name = model.PassengerName,
                    PhoneNumber = phoneNumber,
                    PickupLatitude = model.PickupLatitude,
                    PickupLongitude = model.PickupLongitude,
                    PickupAddress = model.PickupAddress,
                    DropoffLatitude = model.DropoffLatitude,
                    DropoffLongitude = model.DropoffLongitude,
                    DropoffAddress = model.DropoffAddress,
                    PreferredDepartureTime = model.PreferredDepartureTime,
                    GroupId = currentGroup?.Id,
                    IsMatched = true,
                    MatchedVehicleId = currentTargetTrip.Id,
                    PickupOrder = currentPickupOrder,
                    DropoffOrder = currentDropoffOrder
                };
                _context.Passengers.Add(passenger);
                await _context.SaveChangesAsync();

                // 🔄 CẬP NHẬT ROUTE POLYLINE: Nếu hành khách tham gia vào xe đã có sẵn (không phải xe mới tạo)
                // Cập nhật RoutePolyline để bao phủ tất cả điểm đón/trả, giúp hành khách tiếp theo có thể ghép dễ dàng hơn
                bool isExistingVehicle = (DateTime.UtcNow - currentTargetTrip.CreatedAt).TotalSeconds > 10;
                if (isExistingVehicle)
                {
                    await _routeMatchingService.UpdateVehicleRoutePolylineAsync(currentTargetTrip.Id);
                    // Reload vehicle để lấy RoutePolyline mới
                    currentTargetTrip = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == currentTargetTrip.Id);
                }

                // Tính lại ghế trống với kiểm tra tranh chấp (race condition)
                int realAvailableSeats = await _routeMatchingService.CalculateMinAvailableSeatsAsync(currentTargetTrip.Id, currentTargetTrip.TotalSeats);

                // Đảm bảo vẫn còn đủ ghế cho seatsForThisTrip tại thời điểm commit
                int maxPassengerSeatsForVehicle = currentTargetTrip.TotalSeats > 1 ? currentTargetTrip.TotalSeats - 1 : 0;
                if (realAvailableSeats < 0 || seatsForThisTrip > maxPassengerSeatsForVehicle)
                {
                    TempData["CarpoolStatus"] = "Xin lỗi, xe vừa hết ghế trống. Vui lòng chọn chuyến khác.";
                    return RedirectToAction(nameof(Index));
                }

                currentTargetTrip.AvailableSeats = realAvailableSeats;
                currentTargetTrip.IsActive = (currentTargetTrip.AvailableSeats > 0); // PrivateGroup đã được xử lý ở trên
                _context.Update(currentTargetTrip);
                
                // Lưu trạng thái xe trước khi kiểm tra CompletedTrip
                await _context.SaveChangesAsync();
                
                // Lưu CompletedTrip nếu xe đầy (sau khi đã lưu trạng thái)
                if (!currentTargetTrip.IsActive)
                {
                    await SaveCompletedTripAsync(currentTargetTrip.Id);
                }

                // Tạo TripInfo cho nhóm này
                var tripInfo = await BuildTripInfo(currentTargetTrip.Id, passenger.Id);
                assignedTrips.Add(tripInfo);

                // Giảm số ghế còn lại
                remainingSeats -= seatsForThisTrip;
                groupIndex++;
            }

            // 6. Lưu kết quả vào TempData và redirect để reset form
            var resultMessage = assignedTrips.Count == 1
                ? (assignedTrips[0].IsFull ? "Xe đầy." : $"Đã ghép. Chờ thêm {assignedTrips[0].AvailableSeats} ghế.")
                : $"Đã chia nhóm thành {assignedTrips.Count} chuyến xe. Vui lòng xem chi tiết bên dưới.";
            
            // Lưu trip IDs để load lại trong GET
            var tripIds = assignedTrips.Select(t => t.TripId).ToList();
            TempData["CarpoolStatus"] = resultMessage;
            TempData["AssignedTripIds"] = string.Join(",", tripIds);
            TempData["ShowAssignedTrips"] = true;
            
            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int passengerId)
        {
            var passenger = await _context.Passengers
                .Include(p => p.Group)
                .FirstOrDefaultAsync(p => p.Id == passengerId);

            if (passenger == null)
            {
                TempData["CarpoolStatus"] = "Không tìm thấy thông tin hành khách cần hủy.";
                return RedirectToAction(nameof(Index));
            }

            // ✅ CẬP NHẬT PENDING REQUEST: Đánh dấu là Cancelled
            var pendingRequest = await _context.PendingCarpoolRequests
                .FirstOrDefaultAsync(pcr => pcr.PassengerId == passengerId);
            
            if (pendingRequest != null)
            {
                pendingRequest.Status = RequestStatus.Cancelled;
                pendingRequest.CancelledAt = DateTime.Now;
                pendingRequest.CancellationReason = "Người dùng hủy chuyến";
                _context.PendingCarpoolRequests.Update(pendingRequest);
            }

            var trip = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == passenger.MatchedVehicleId);
            if (trip != null)
            {
                int seats = passenger.Group?.RequiredSeats ?? 1;
                int maxPassengerSeats = trip.TotalSeats - 1; // Trừ 1 ghế tài xế
                trip.AvailableSeats = Math.Min(maxPassengerSeats, trip.AvailableSeats + seats);
                trip.IsActive = true;
            }

            if (passenger.Group != null)
            {
                _context.PassengerGroups.Remove(passenger.Group);
            }

            _context.Passengers.Remove(passenger);
            await _context.SaveChangesAsync();

            // ✅ Sau khi xóa hành khách và cập nhật ghế, cập nhật lại RoutePolyline của xe
            if (trip != null)
            {
                await _routeMatchingService.UpdateVehicleRoutePolylineAsync(trip.Id);
            }

            TempData["CarpoolStatus"] = "Bạn đã hủy tham gia chuyến xe thành công.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<(double Latitude, double Longitude)> GeocodeAddress(string address)
        {
            // Sử dụng Nominatim (OpenStreetMap) để geocode
            var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(address)}&limit=1&countrycodes=vn&addressdetails=1&accept-language=vi";
            
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "nhom8dalat-app (contact: phuocnguyen10112004@gmail.com)");
                var response = await client.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<List<dynamic>>(response);

                if (data != null && data.Any())
                {
                    var first = data[0];
                    double lat = double.Parse(first.lat.ToString(), CultureInfo.InvariantCulture);
                    double lon = double.Parse(first.lon.ToString(), CultureInfo.InvariantCulture);
                    return (lat, lon);
                }
            }

            // KHÔNG fallback về tọa độ mặc định - yêu cầu người dùng nhập lại
            throw new InvalidOperationException("Không tìm thấy tọa độ phù hợp cho địa chỉ vừa nhập. Vui lòng nhập rõ hơn (quận/phường/tỉnh) hoặc chọn từ gợi ý trên bản đồ.");
        }

        /// <summary>
        /// Lấy ra "từ khóa chính" trong địa chỉ để dùng cho tìm kiếm LIKE,
        /// ưu tiên cụm cuối cùng (thường là tỉnh/thành: \"Bình Định\", \"Đà Lạt\", ...).
        /// </summary>
        private string GetAddressKeyword(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return string.Empty;

            // Loại bỏ khoảng trắng thừa
            address = address.Trim();

            // Tách theo dấu phẩy trước (thường địa chỉ dạng: đường, phường, quận, tỉnh)
            var partsByComma = address.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var lastPart = partsByComma.LastOrDefault()?.Trim();

            if (string.IsNullOrWhiteSpace(lastPart))
                lastPart = address;

            // Lấy 2 từ cuối cùng làm keyword (ví dụ: \"Bình Định\", \"Thành phố Đà Lạt\")
            var words = Regex.Split(lastPart, @"\s+")
                             .Where(w => !string.IsNullOrWhiteSpace(w))
                             .ToArray();

            if (words.Length == 0)
                return lastPart;

            if (words.Length == 1)
                return words[0];

            return $"{words[words.Length - 2]} {words[words.Length - 1]}";
        }

        private (string Name, string Phone, string Plate) GetNextDriver()
        {
            var driver = DriverPool[_driverAssignmentIndex % DriverPool.Length];
            _driverAssignmentIndex++;
            return driver;
        }

        private (string Name, string Phone) ExtractDriverInfo(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return ("Đang cập nhật", "Đang cập nhật");

            var parts = raw.Split('|', 2, StringSplitOptions.TrimEntries);
            return parts.Length == 2 ? (parts[0], parts[1]) : (raw.Trim(), "Đang cập nhật");
        }

        /// <summary>
        /// Lấy cấu hình giá theo loại xe từ database
        /// </summary>
        private async Task<VehiclePricingConfig?> GetPricingConfigAsync(int seatCapacity)
        {
            var config = await _context.VehiclePricingConfigs
                .Where(c => c.SeatCapacity == seatCapacity && c.IsActive)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Không tìm thấy cấu hình giá cho xe {seatCapacity} chỗ. Dùng giá mặc định xe 4 chỗ.");
            }

            return config;
        }

        /// <summary>
        /// Tính chi phí chuyến đi dựa trên khoảng cách và loại xe
        /// </summary>
        /// <param name="distanceKm">Khoảng cách (km)</param>
        /// <param name="seatCapacity">Số chỗ xe (4, 7, 9)</param>
        /// <returns>Chi phí chuyến đi (đồng)</returns>
        private async Task<decimal> CalculateTripCostAsync(double distanceKm, int seatCapacity)
        {
            // --- PHẦN 1: LẤY CẤU HÌNH HOẶC TẠO FALLBACK THÔNG MINH ---
            var config = await GetPricingConfigAsync(seatCapacity);

            if (config == null)
            {
                // Logic này bạn đã có: Phân biệt giá xe nhỏ/to
                decimal baseFuel = seatCapacity <= 4 ? 3000m : (seatCapacity <= 7 ? 4000m : 5000m);
                decimal baseSalary = seatCapacity <= 4 ? 500000m : 700000m;

                config = new VehiclePricingConfig
                {
                    SeatCapacity = seatCapacity,
                    VehicleTypeName = $"Xe {seatCapacity} chỗ",
                    FuelPricePerKm = baseFuel,
                    DriverSalaryPerTrip = baseSalary,
                    TollFee = 150000m,
                    ProfitMargin = 1.2m,
                    MinimumTripCost = 300000m
                };
                System.Diagnostics.Debug.WriteLine($"⚠️ Dùng giá mặc định (Fallback): {config.VehicleTypeName}");
            }

            // --- PHẦN 2: TÍNH TOÁN GIÁ TIỀN THÔNG MINH (LOGIC MỚI) ---

            distanceKm = Math.Max(5, distanceKm);

            // a. Tiền xăng (Luôn tính theo km)
            decimal fuelCost = (decimal)distanceKm * config.FuelPricePerKm;

            // b. Lương tài xế (Linh hoạt: Ngắn tính theo km, Dài tính trọn gói)
            decimal driverSalary;
            if (distanceKm < 200)
            {
                // Đi ngắn (<200km): Tính 3.000đ/km tiền công (hoặc lấy 80% giá xăng)
                // Logic: Đi 10km trả 30k tiền công là hợp lý, thay vì trả 500k
                driverSalary = (decimal)distanceKm * 3000m;
            }
            else
            {
                // Đi dài (HCM-Đà Lạt): Tính lương khoán theo chuyến (500k/700k)
                driverSalary = config.DriverSalaryPerTrip;
            }

            // c. Phí cầu đường (Chỉ tính khi đi xa)
            // Đi < 50km thường là loanh quanh thành phố hoặc đường ngắn, ít qua trạm thu phí lớn
            decimal tollFee = (distanceKm > 50) ? config.TollFee : 0;

            // d. Chi phí vận hành (Gốc)
            decimal operatingCost = fuelCost + driverSalary;

            // e. Lợi nhuận (Chỉ nhân trên chi phí vận hành)
            decimal profit = operatingCost * config.ProfitMargin;

            // f. Tổng cộng = Lợi nhuận + Phí cầu đường
            decimal totalCost = profit + tollFee;

            System.Diagnostics.Debug.WriteLine($"💰 CALCULATION: Dist={distanceKm}km | Fuel={fuelCost:N0} | Salary={driverSalary:N0} | Toll={tollFee:N0} | Total={totalCost:N0}");

            // Đảm bảo không thấp hơn mức tối thiểu (để tài xế chịu nhận cuốc)
            return Math.Round(Math.Max(totalCost, config.MinimumTripCost), 0);
        }

        /// <summary>
        /// Tính chi phí chuyến đi (overload với tọa độ - tự tính khoảng cách)
        /// </summary>
        private async Task<decimal> CalculateTripCostAsync(double pickupLat, double pickupLng, double dropLat, double dropLng, int seatCapacity)
        {
            // Dùng OSRM để tính khoảng cách thực tế
            var distanceKm = await _osrmService.GetDistanceKmAsync(pickupLat, pickupLng, dropLat, dropLng);
            
            // Fallback về Haversine nếu OSRM lỗi
            var fallbackDistance = CalculateDistanceKm(pickupLat, pickupLng, dropLat, dropLng);
            
            // ✅ KIỂM TRA KHOẢNG CÁCH HỢP LÍ (VN max ~1500 km)
            double distance;
            if (distanceKm.HasValue && distanceKm.Value > 0 && distanceKm.Value < 1500)
            {
                distance = distanceKm.Value;
                System.Diagnostics.Debug.WriteLine($"✅ CalculateTripCostAsync - OSRM distance: {distance} km");
            }
            else if (fallbackDistance > 0 && fallbackDistance < 1500)
            {
                distance = fallbackDistance;
                System.Diagnostics.Debug.WriteLine($"✅ CalculateTripCostAsync - Haversine fallback: {distance} km");
            }
            else
            {
                // Khoảng cách sai → dùng mặc định 300 km (HCM-Đà Lạt)
                distance = 300;
                System.Diagnostics.Debug.WriteLine($"⚠️ CalculateTripCostAsync - Distance invalid! Using default 300 km");
                System.Diagnostics.Debug.WriteLine($"   OSRM: {distanceKm} km, Haversine: {fallbackDistance} km");
                System.Diagnostics.Debug.WriteLine($"   Pickup: ({pickupLat}, {pickupLng}), Dropoff: ({dropLat}, {dropLng})");
            }

            // Gọi method chính để tính chi phí
            return await CalculateTripCostAsync(distance, seatCapacity);
        }

        private double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
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

        private double ToRadians(double angle) => angle * Math.PI / 180;

        /// <summary>
        /// Xác định loại xe phù hợp: Ưu tiên xe nhỏ nhất có đủ chỗ (để tối ưu khi tạo xe mới cho số ghế còn lại)
        /// Ví dụ: 3 người -> xe 4 chỗ (3 ghế trống), 6 người -> xe 7 chỗ (6 ghế trống), 8 người -> xe 9 chỗ (8 ghế trống)
        /// Lưu ý: Khi số ghế còn lại = 4, xe 4 chỗ chỉ có 3 ghế trống (thiếu 1), nên dùng xe 7 chỗ (6 ghế trống, dư 2 nhưng OK)
        /// </summary>
        private int DetermineVehicleCapacity(int seatsNeeded, int requestedSeats)
        {
            var target = Math.Max(seatsNeeded, requestedSeats);
            
            // Ưu tiên xe nhỏ nhất có đủ chỗ (4 chỗ < 7 chỗ < 9 chỗ)
            // Nhưng phải đảm bảo số ghế trống (capacity - 1) >= seatsNeeded
            foreach (var capacity in SupportedCapacities.OrderBy(c => c))
            {
                int availableSeats = capacity - 1; // Trừ 1 ghế tài xế
                if (availableSeats >= target)
                {
                    return capacity;
                }
            }

            // Nếu không tìm thấy (số ghế cần > capacity của xe lớn nhất), trả về xe lớn nhất
            return SupportedCapacities.Max();
        }

        /// <summary>
        /// Tính khoảng cách phân đoạn từ route polyline
        /// </summary>
        private async Task<double?> CalculateSegmentDistanceAsync(string routePolyline, int startOrder, int endOrder)
        {
            try
            {
                var geometry = JsonConvert.DeserializeObject<List<List<double>>>(routePolyline);
                if (geometry == null || geometry.Count < 2)
                    return null;

                // Kiểm tra và đảm bảo startOrder < endOrder
                if (startOrder >= endOrder)
                    return null;

                // Đảm bảo endOrder không vượt quá số điểm trong geometry
                if (endOrder >= geometry.Count)
                    endOrder = geometry.Count - 1;

                // Đảm bảo startOrder không âm
                if (startOrder < 0)
                    startOrder = 0;

                // Lấy điểm bắt đầu và kết thúc
                var startPoint = geometry[startOrder];
                var endPoint = geometry[endOrder];

                if (startPoint.Count < 2 || endPoint.Count < 2)
                    return null;

                // RoutePolyline format: [[lon, lat], ...]
                // Tính tổng khoảng cách qua tất cả các điểm trung gian trên polyline
                // Cách 1: Tính tổng các segment nhỏ (nhanh, nhưng Haversine không chính xác 100%)
                // Cách 2: Gọi OSRM (chính xác hơn, nhưng tốn API call)
                // Chọn cách 2 vì chính xác hơn cho tính giá
                
                var startLon = startPoint[0];
                var startLat = startPoint[1];
                var endLon = endPoint[0];
                var endLat = endPoint[1];

                // Gọi OSRM để tính khoảng cách thực tế của phân đoạn trên tuyến đường
                // OSRM sẽ tính theo tuyến đường thực tế, chính xác hơn Haversine
                var distance = await _osrmService.GetDistanceKmAsync(
                    startLat, startLon,
                    endLat, endLon);

                // Nếu OSRM trả về kết quả hợp lý, dùng nó
                if (distance.HasValue && distance.Value > 0 && distance.Value < 2000)
                {
                    return distance;
                }

                // Fallback: Tính tổng khoảng cách bằng Haversine qua các điểm trung gian
                double totalDistance = 0;
                for (int i = startOrder; i < endOrder; i++)
                {
                    if (i + 1 >= geometry.Count) break;
                    var point1 = geometry[i];
                    var point2 = geometry[i + 1];
                    if (point1.Count >= 2 && point2.Count >= 2)
                    {
                        var lat1 = point1[1];
                        var lon1 = point1[0];
                        var lat2 = point2[1];
                        var lon2 = point2[0];
                        totalDistance += CalculateDistanceKm(lat1, lon1, lat2, lon2);
                    }
                }

                return totalDistance > 0 ? totalDistance : null;
            }
            catch (Exception ex)
            {
                // Log lỗi để debug (có thể thêm logging sau)
                System.Diagnostics.Debug.WriteLine($"Lỗi tính khoảng cách phân đoạn: {ex.Message}");
                return null;
            }
        }

        private string DescribeVehicleType(int seats)
        {
            return seats <= 0 ? "Đang cập nhật" : $"Xe {seats} chỗ";
        }

        private bool HasValidCoordinates(double lat, double lng)
        {
            return Math.Abs(lat) > double.Epsilon && Math.Abs(lng) > double.Epsilon;
        }

        private async Task<CarpoolTripInfo?> BuildTripInfo(int vehicleId, int? highlightPassengerId = null)
        {
            // 1. Lấy thông tin xe từ DB
            var trip = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId);
            if (trip == null) return null;

            // 2. Lấy danh sách khách đã đặt
            var passengers = await _context.Passengers
                .Include(p => p.Group)
                .Where(p => p.MatchedVehicleId == trip.Id)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            var (driverName, driverPhone) = ExtractDriverInfo(trip.DriverName);

            // =========================================================================
            // 🟢 LOGIC TÍNH TIỀN HIỂN THỊ (Đã Fix lỗi giá quá cao)
            // =========================================================================

            // a. Tính khoảng cách toàn tuyến xe (A->D) để làm mẫu số
            var tripDistResult = await _osrmService.GetDistanceKmAsync(
                trip.PickupLatitude, trip.PickupLongitude,
                trip.DropoffLatitude, trip.DropoffLongitude);

            double tripTotalDistance = 0;

            // Nếu OSRM tính được thì lấy, nếu lỗi mạng thì mới dùng đường chim bay
            if (tripDistResult.HasValue && tripDistResult.Value > 0)
            {
                tripTotalDistance = tripDistResult.Value; // Lấy số ~295km
            }
            else
            {
                tripTotalDistance = CalculateDistanceKm(
                    trip.PickupLatitude, trip.PickupLongitude,
                    trip.DropoffLatitude, trip.DropoffLongitude);
            }

            if (tripTotalDistance <= 0) tripTotalDistance = 1; // Tránh lỗi chia cho 0

            int sellableSeats = trip.TotalSeats > 1 ? trip.TotalSeats - 1 : 1;

            // Tính "Đơn giá trần" dựa trên số ghế khách
            // Ví dụ: 1.8tr / 3 ghế khách = 600k/ghế
            decimal basePricePerFullSeat = (trip.FixedPrice ?? 0) / sellableSeats;

            var passengerInfos = new List<CarpoolPassengerInfo>();

            // --- [THAY THẾ TOÀN BỘ VÒNG LẶP FOREACH CŨ BẰNG ĐOẠN NÀY] ---

            foreach (var p in passengers)
            {
                double pDist;

                // Ưu tiên: Tính khoảng cách thực tế trên RoutePolyline nếu có PickupOrder/DropoffOrder
                // Điều này chính xác hơn vì họ đi trên cùng tuyến đường thực tế
                if (p.PickupOrder.HasValue && p.DropoffOrder.HasValue && 
                    !string.IsNullOrEmpty(trip.RoutePolyline) &&
                    p.PickupOrder.Value < p.DropoffOrder.Value)
                {
                    // Tính khoảng cách thực tế trên RoutePolyline từ PickupOrder đến DropoffOrder
                    var segmentDist = await CalculateSegmentDistanceAsync(trip.RoutePolyline, p.PickupOrder.Value, p.DropoffOrder.Value);
                    if (segmentDist.HasValue && segmentDist.Value > 0)
                    {
                        pDist = segmentDist.Value;
                    }
                    else
                    {
                        // Fallback: dùng OSRM
                        var distResult = await _osrmService.GetDistanceKmAsync(
                            p.PickupLatitude, p.PickupLongitude,
                            p.DropoffLatitude, p.DropoffLongitude);
                        pDist = distResult.HasValue && distResult.Value > 0 
                            ? distResult.Value 
                            : CalculateDistanceKm(p.PickupLatitude, p.PickupLongitude, p.DropoffLatitude, p.DropoffLongitude);
                    }
                }
                else
                {
                    // Nếu không có PickupOrder/DropoffOrder, dùng OSRM như cũ
                    var distResult = await _osrmService.GetDistanceKmAsync(
                        p.PickupLatitude, p.PickupLongitude,
                        p.DropoffLatitude, p.DropoffLongitude);

                    if (distResult.HasValue && distResult.Value > 0)
                    {
                        pDist = distResult.Value;
                    }
                    else
                    {
                        pDist = CalculateDistanceKm(
                            p.PickupLatitude, p.PickupLongitude,
                            p.DropoffLatitude, p.DropoffLongitude);
                    }
                }

                // d. Tính tỷ lệ: Khách đi / Tổng tuyến (Max 100%)
                double ratio = Math.Min(pDist / tripTotalDistance, 1.0);

                // e. Tính tiền hiển thị: Đơn giá gốc * Tỷ lệ * Số ghế khách đặt
                decimal displayCost = basePricePerFullSeat * (decimal)ratio * (p.Group?.RequiredSeats ?? 1);

                passengerInfos.Add(new CarpoolPassengerInfo
                {
                    PassengerId = p.Id,
                    Name = p.Name,
                    Seats = p.Group?.RequiredSeats ?? 1,
                    Cost = Math.Round(displayCost / 1000) * 1000, // Làm tròn đẹp
                    DistanceKm = Math.Round(pDist, 1) // Giờ nó sẽ hiện đúng ~291.4 km
                });
            }

            // =========================================================================

            return new CarpoolTripInfo
            {
                TripId = trip.Id,
                PickupAddress = trip.PickupAddress,
                DropoffAddress = trip.DropoffAddress,
                DepartureTime = trip.DepartureTime,
                TotalSeats = trip.TotalSeats,
                AvailableSeats = trip.AvailableSeats, // Lấy trực tiếp từ DB (đã được tính đúng ở Service)
                TotalCost = trip.FixedPrice ?? 0,
                CostPerSeat = basePricePerFullSeat, // Hiển thị giá 1 ghế full tuyến để tham khảo
                DriverName = driverName,
                DriverPhone = driverPhone,
                LicensePlate = trip.LicensePlate,
                VehicleType = DescribeVehicleType(trip.TotalSeats),
                TripDistance = Math.Round(tripTotalDistance, 1), // Khoảng cách của chuyến
                Passengers = passengerInfos,
                CurrentPassengerId = highlightPassengerId,
                CurrentPassengerCost = passengerInfos.FirstOrDefault(p => p.PassengerId == highlightPassengerId)?.Cost
            };
        }


        private async Task<List<CarpoolTripInfo>> GetOpenTripsAsync()
        {
            var now = DateTime.UtcNow.AddHours(-2);

            var tripIds = await _context.Vehicles
                .Where(v => v.IsActive &&
                            v.VehicleType == "Ghép xe tự động" &&
                            v.AvailableSeats > 0 &&
                            v.DepartureTime >= now)
                .OrderBy(v => v.DepartureTime)
                .Take(8)
                .Select(v => v.Id)
                .ToListAsync();

            var results = new List<CarpoolTripInfo>();
            foreach (var id in tripIds)
            {
                var info = await BuildTripInfo(id);
                if (info != null)
                {
                    results.Add(info);
                }
            }

            return results;
        }

        /// <summary>
        /// Copy chuyến đã đầy vào bảng CompletedTrips để lưu lịch sử
        /// </summary>
        private async Task SaveCompletedTripAsync(int vehicleId)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == vehicleId);

            if (vehicle == null || vehicle.IsActive) // Chỉ lưu khi chuyến đã đầy (IsActive = false)
                return;

            // Kiểm tra xem đã lưu chưa (tránh lưu trùng)
            var existing = await _context.CompletedTrips
                .FirstOrDefaultAsync(ct => ct.VehicleId == vehicleId && ct.Status == TripStatus.Completed);
            
            if (existing != null)
                return;

            // Lấy tất cả hành khách đã ghép vào xe
            var passengers = await _context.Passengers
                .Include(p => p.Group)
                .Where(p => p.MatchedVehicleId == vehicleId && p.IsMatched)
                .ToListAsync();

            if (!passengers.Any())
                return;

            // Tính toán thông tin chuyến
            int occupiedSeats = passengers.Sum(p => p.Group?.RequiredSeats ?? 1);
            int maxPassengerSeats = vehicle.TotalSeats - 1; // Trừ 1 ghế tài xế

            // Extract driver info từ DriverName (format: "Name|Phone")
            var (driverName, driverPhone) = ExtractDriverInfo(vehicle.DriverName);

            // Lấy tripInfo để có chi phí chính xác
            var tripInfo = await BuildTripInfo(vehicleId, null);
            decimal totalCost = tripInfo?.TotalCost ?? vehicle.FixedPrice ?? 0;
            decimal costPerSeat = tripInfo?.CostPerSeat ?? (occupiedSeats > 0 ? totalCost / occupiedSeats : 0);

            // Tạo CompletedTrip
            var completedTrip = new CompletedTrip
            {
                VehicleId = vehicle.Id,
                DriverName = driverName,
                DriverPhone = driverPhone,
                LicensePlate = vehicle.LicensePlate,
                PickupLatitude = vehicle.PickupLatitude,
                PickupLongitude = vehicle.PickupLongitude,
                PickupAddress = vehicle.PickupAddress,
                DropoffLatitude = vehicle.DropoffLatitude,
                DropoffLongitude = vehicle.DropoffLongitude,
                DropoffAddress = vehicle.DropoffAddress,
                DepartureTime = vehicle.DepartureTime,
                TotalSeats = vehicle.TotalSeats,
                OccupiedSeats = occupiedSeats,
                VehicleType = vehicle.VehicleType ?? DescribeVehicleType(vehicle.TotalSeats),
                TotalCost = totalCost,
                CostPerSeat = costPerSeat > 0 ? costPerSeat : null,
                RoutePolyline = vehicle.RoutePolyline,
                Status = TripStatus.Completed,
                CompletedAt = DateTime.Now
            };

            _context.CompletedTrips.Add(completedTrip);
            await _context.SaveChangesAsync(); // Lưu để có Id

            // Copy hành khách vào CompletedTripPassengers
            foreach (var passenger in passengers)
            {
                // Tìm chi phí của hành khách từ tripInfo
                decimal passengerCost = 0;
                if (tripInfo != null)
                {
                    var passengerInfo = tripInfo.Passengers.FirstOrDefault(p => p.PassengerId == passenger.Id);
                    passengerCost = passengerInfo?.Cost ?? 0;
                }
                
                // Nếu không có trong tripInfo, tính chia đều
                if (passengerCost <= 0 && costPerSeat > 0)
                {
                    int seats = passenger.Group?.RequiredSeats ?? 1;
                    passengerCost = costPerSeat * seats;
                }

                var completedPassenger = new CompletedTripPassenger
                {
                    CompletedTripId = completedTrip.Id,
                    OriginalPassengerId = passenger.Id,
                    Name = passenger.Name,
                    PhoneNumber = passenger.PhoneNumber,
                    PickupLatitude = passenger.PickupLatitude,
                    PickupLongitude = passenger.PickupLongitude,
                    PickupAddress = passenger.PickupAddress,
                    DropoffLatitude = passenger.DropoffLatitude,
                    DropoffLongitude = passenger.DropoffLongitude,
                    DropoffAddress = passenger.DropoffAddress,
                    PickupOrder = passenger.PickupOrder,
                    DropoffOrder = passenger.DropoffOrder,
                    RequiredSeats = passenger.Group?.RequiredSeats ?? 1,
                    GroupId = passenger.GroupId,
                    GroupName = passenger.Group?.GroupName ?? $"Khách lẻ {passenger.Name}",
                    Cost = Math.Round(passengerCost, 0)
                };

                _context.CompletedTripPassengers.Add(completedPassenger);
            }

            await _context.SaveChangesAsync();
            
            System.Diagnostics.Debug.WriteLine($"✅ Đã lưu lịch sử chuyến #{completedTrip.Id} (Vehicle #{vehicleId}, {occupiedSeats} hành khách)");
        }
    }
}

