using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebDuLichDaLat.Models;
using WebDuLichDaLat.Services;

namespace WebDuLichDaLat.Controllers
{
    // Enum định nghĩa 3 mức ngân sách
    public enum BudgetPlanType
    {
        Budget,      // 🔥 TIẾT KIỆM - Giá rẻ nhất
        Balanced,    // 🎯 CÂN BẰNG - Phổ biến nhất
        Premium      // 🌟 CAO CẤP - Sang trọng nhất
    }

    public class TripPlannerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly TransportPriceCalculator _priceCalculator;
        private readonly OsrmRouteService _osrmService;
        private readonly SuggestionFormatterService _suggestionFormatter;
        private readonly CostCalculationService _costCalculationService;
        private readonly RecommendationService _recommendationService;
        private readonly ItineraryService _itineraryService; // ✅ Thêm mới - Service để tạo lịch trình

        public TripPlannerController(
            ApplicationDbContext context,
            TransportPriceCalculator priceCalculator,
            OsrmRouteService osrmService,
            SuggestionFormatterService suggestionFormatter,
            CostCalculationService costCalculationService,
            RecommendationService recommendationService,
            ItineraryService itineraryService) // ✅ Inject ItineraryService
        {
            _context = context;
            _priceCalculator = priceCalculator;
            _osrmService = osrmService;
            _suggestionFormatter = suggestionFormatter;
            _costCalculationService = costCalculationService;
            _recommendationService = recommendationService;
            _itineraryService = itineraryService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Tối ưu: Chỉ load dữ liệu cần thiết cho GET request
            var categories = _context.Categories
                .AsNoTracking()
                .ToList();

            // Chỉ load các trường cần thiết cho dropdown và map
            var touristPlaces = _context.TouristPlaces
                .AsNoTracking()
                .Select(tp => new TouristPlace
                {
                    Id = tp.Id,
                    Name = tp.Name,
                    Latitude = tp.Latitude,
                    Longitude = tp.Longitude,
                    CategoryId = tp.CategoryId
                })
                .ToList();

            var transportOptions = _context.TransportOptions
                .AsNoTracking()
                .ToList();

            var model = new TripPlannerViewModel
            {
                Categories = categories,
                TouristPlaces = touristPlaces,
                TransportOptions = transportOptions,
                // Không load Hotels, Restaurants, Attractions ở GET - chỉ load khi cần trong POST
                Hotels = new List<Hotel>(),
                Restaurants = new List<Restaurant>(),
                Attractions = new List<Attraction>(),
                Suggestions = new List<string>()
            };

            model.TransportSelectList = transportOptions
                .Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = $"{t.Name} ({t.Type})"
                })
                .ToList();

            ViewBag.TouristPlacesJson = JsonConvert.SerializeObject(
                touristPlaces.Select(tp => new { tp.Id, tp.Name, tp.Latitude, tp.Longitude, tp.CategoryId })
            );

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(TripPlannerViewModel model)
        {
            // ✅ FIX #1: Kiểm tra hệ thống có dữ liệu không
            if (!await ValidateSystemData())
            {
                ViewBag.SystemError = "⚠️ Hệ thống chưa có dữ liệu. Vui lòng liên hệ admin.";
                return await ReturnViewWithDefaults(model);
            }

            // ✅ FIX #1: Validate ngân sách
            if (model.Budget <= 0)
            {
                ModelState.AddModelError("Budget", "Ngân sách phải lớn hơn 0đ");
                return await ReturnViewWithDefaults(model);
            }
            if (model.Budget < 100000) // Tối thiểu 100k
            {
                ModelState.AddModelError("Budget",
                    "Ngân sách quá thấp. Tối thiểu 100,000đ cho chuyến đi Đà Lạt");
                return await ReturnViewWithDefaults(model);
            }

            // ✅ FIX #1: Validate số ngày
            if (model.NumberOfDays < 0)
            {
                model.NumberOfDays = 0; // Auto-calculate
            }
            if (model.NumberOfDays > 30)
            {
                ModelState.AddModelError("NumberOfDays",
                    "Số ngày không được vượt quá 30. Vui lòng chia thành nhiều chuyến đi.");
                return await ReturnViewWithDefaults(model);
            }

            // Tối ưu: Chỉ load dữ liệu cần thiết, sử dụng AsNoTracking() cho read-only
            var transportOptions = _context.TransportOptions
                .AsNoTracking()
                .ToList();

            model.TransportOptions = transportOptions;
            model.TransportSelectList = transportOptions
                .Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = $"{t.Name} ({t.Type})"
                })
                .ToList();

            // Chỉ load Hotels, Restaurants, Attractions khi thực sự cần (cho suggestions)
            // Hotels và Restaurants giờ có tọa độ trực tiếp, không cần Include Location
            model.Hotels = _context.Hotels
                .AsNoTracking()
                .ToList();
            model.Restaurants = _context.Restaurants
                .AsNoTracking()
                .ToList();
            model.Attractions = _context.Attractions
                .AsNoTracking()
                .ToList();

            // ✅ FIX: Load lại TouristPlaces và Categories để hiển thị trong dropdown
            model.Categories = _context.Categories.AsNoTracking().ToList();
            model.TouristPlaces = _context.TouristPlaces
                .AsNoTracking()
                .Select(tp => new TouristPlace
                {
                    Id = tp.Id,
                    Name = tp.Name,
                    Latitude = tp.Latitude,
                    Longitude = tp.Longitude,
                    CategoryId = tp.CategoryId
                })
                .ToList();

            // ✅ FIX: Load lại TouristPlacesJson cho map
            ViewBag.TouristPlacesJson = JsonConvert.SerializeObject(
                model.TouristPlaces.Select(tp => new { tp.Id, tp.Name, tp.Latitude, tp.Longitude, tp.CategoryId })
            );

            // ⚙️ Chuẩn hóa lại tọa độ/distance theo InvariantCulture để tránh lỗi format dấu phẩy
            var rawLatitude = Request.Form["StartLatitude"].FirstOrDefault();
            var rawLongitude = Request.Form["StartLongitude"].FirstOrDefault();
            var rawDistance = Request.Form["DistanceKm"].FirstOrDefault();

            var normalizedLat = ParseCoordinate(rawLatitude);
            var normalizedLng = ParseCoordinate(rawLongitude);
            var normalizedDistance = ParseCoordinate(rawDistance);

            if (normalizedLat.HasValue) model.StartLatitude = normalizedLat;
            if (normalizedLng.HasValue) model.StartLongitude = normalizedLng;
            if (normalizedDistance.HasValue) model.DistanceKm = normalizedDistance.Value;

            // ✅ FIX #1: Validate GPS coordinates
            if (model.StartLatitude.HasValue)
            {
                if (model.StartLatitude < -90 || model.StartLatitude > 90)
                {
                    model.StartLatitude = null;
                    ViewBag.CoordinateWarning = "Tọa độ không hợp lệ. Hệ thống sẽ ước tính vị trí.";
                }
            }
            if (model.StartLongitude.HasValue)
            {
                if (model.StartLongitude < -180 || model.StartLongitude > 180)
                {
                    model.StartLongitude = null;
                }
            }

            // ✅ FIX #1: Cảnh báo nếu GPS ngoài Việt Nam
            if (model.StartLatitude.HasValue && model.StartLongitude.HasValue)
            {
                if (model.StartLatitude < 8 || model.StartLatitude > 24 ||
                    model.StartLongitude < 102 || model.StartLongitude > 110)
                {
                    ViewBag.LocationWarning =
                        "⚠️ Vị trí xuất phát nằm ngoài Việt Nam. Chi phí có thể không chính xác.";
                }
            }

            var selectedPlaceIds = model.SelectedTouristPlaceIds?
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList() ?? new List<string>();

            // ✅ FIX #1: Validate số lượng địa điểm
            if (selectedPlaceIds.Count > 20)
            {
                ModelState.AddModelError("SelectedTouristPlaceIds",
                    "Vui lòng chọn tối đa 20 địa điểm để có kết quả tối ưu.");
                return await ReturnViewWithDefaults(model);
            }

            // ✅ FIX #1: Validate vị trí xuất phát
            if (string.IsNullOrWhiteSpace(model.StartLocation))
            {
                ModelState.AddModelError("StartLocation", "Vui lòng nhập vị trí bắt đầu");
                return await ReturnViewWithDefaults(model);
            }

            // ✅ MỚI: Geocode địa chỉ nếu chưa có tọa độ GPS (áp dụng logic từ CarpoolController)
            try
            {
                if (!HasValidCoordinates(model.StartLatitude ?? 0, model.StartLongitude ?? 0))
                {
                    var (pickupLat, pickupLon) = await GeocodeAddress(model.StartLocation);
                    model.StartLatitude = pickupLat;
                    model.StartLongitude = pickupLon;
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("StartLocation", $"Lỗi lấy tọa độ: {ex.Message}");
                return await ReturnViewWithDefaults(model);
            }

            // ✅ BƯỚC 1: TÍNH GIÁ VẬN CHUYỂN TỪ GPS TRƯỚC (CHO TỪNG PHƯƠNG TIỆN)
            var transportCostMap = new Dictionary<int, decimal>();
            TransportPriceResult? primaryTransportPriceResult = null;
            bool isFromOtherCity = !(model.StartLocation?.Contains("Đà Lạt", StringComparison.OrdinalIgnoreCase) ?? false);
            int primaryTransportId = model.SelectedTransportId ?? transportOptions.First().Id;

            // ✅ MỚI: Lấy điểm đầu tiên được gợi ý để tính giá cho xe tự lái
            TouristPlace? firstSuggestedPlace = null;
            if (selectedPlaceIds != null && selectedPlaceIds.Any())
            {
                var firstPlaceId = selectedPlaceIds.First();
                firstSuggestedPlace = model.TouristPlaces.FirstOrDefault(p => p.Id == firstPlaceId);
            }

            if (model.StartLatitude.HasValue && model.StartLongitude.HasValue && isFromOtherCity)
            {
                var transportsNeedingPrice = transportOptions
                    .Where(t => !t.Name.Contains("Taxi nội thành", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var transport in transportsNeedingPrice)
                {
                    try
                    {
                        TransportPriceResult priceResult;

                        // ✅ FIX: Với xe tự lái (xe máy, xe hơi cá nhân), tính từ vị trí xuất phát đến điểm đầu tiên được gợi ý
                        // Thay vì đến trung tâm Đà Lạt
                        if (transport.IsSelfDrive && firstSuggestedPlace != null)
                        {
                            // Tính khoảng cách đến điểm đầu tiên bằng OSRM
                            var distanceToFirstPlace = await _osrmService.GetDistanceKmAsync(
                                model.StartLatitude.Value,
                                model.StartLongitude.Value,
                                firstSuggestedPlace.Latitude,
                                firstSuggestedPlace.Longitude
                            );

                            if (distanceToFirstPlace.HasValue && distanceToFirstPlace.Value > 0)
                            {
                                // Tính giá dựa trên khoảng cách đến điểm đầu tiên
                                if (transport.FuelConsumption > 0 && transport.FuelPrice > 0)
                                {
                                    decimal fuelUsed = (decimal)distanceToFirstPlace.Value * transport.FuelConsumption / 100m;
                                    decimal fuelCost = fuelUsed * transport.FuelPrice;
                                    decimal maintenanceCost = fuelCost * 0.2m;

                                    priceResult = new TransportPriceResult
                                    {
                                        Price = fuelCost + maintenanceCost,
                                        PriceType = "Calculated",
                                        DistanceToDalat = distanceToFirstPlace.Value, // Lưu khoảng cách thực tế
                                        Note = $"Chi phí tính theo nhiên liệu từ vị trí xuất phát đến {firstSuggestedPlace.Name}: " +
                                               $"{distanceToFirstPlace.Value:F1}km × {transport.FuelConsumption}l/100km × " +
                                               $"{transport.FuelPrice:N0}đ/lít + bảo dưỡng (20%) = {fuelCost + maintenanceCost:N0}đ"
                                    };
                                }
                                else
                                {
                                    // Fallback nếu không có thông tin nhiên liệu
                                    priceResult = await _priceCalculator.GetFinalPriceAsync(
                                        model.StartLatitude.Value,
                                        model.StartLongitude.Value,
                                        transport.Id
                                    );
                                }
                            }
                            else
                            {
                                // Nếu OSRM lỗi, dùng Haversine
                                var haversineDistance = _recommendationService.GetDistance(
                                    model.StartLatitude.Value,
                                    model.StartLongitude.Value,
                                    firstSuggestedPlace.Latitude,
                                    firstSuggestedPlace.Longitude
                                );

                                if (transport.FuelConsumption > 0 && transport.FuelPrice > 0)
                                {
                                    decimal fuelUsed = (decimal)haversineDistance * transport.FuelConsumption / 100m;
                                    decimal fuelCost = fuelUsed * transport.FuelPrice;
                                    decimal maintenanceCost = fuelCost * 0.2m;

                                    priceResult = new TransportPriceResult
                                    {
                                        Price = fuelCost + maintenanceCost,
                                        PriceType = "Calculated",
                                        DistanceToDalat = haversineDistance,
                                        Note = $"Chi phí tính theo nhiên liệu từ vị trí xuất phát đến {firstSuggestedPlace.Name}: " +
                                               $"{haversineDistance:F1}km × {transport.FuelConsumption}l/100km × " +
                                               $"{transport.FuelPrice:N0}đ/lít + bảo dưỡng (20%) = {fuelCost + maintenanceCost:N0}đ"
                                    };
                                }
                                else
                                {
                                    priceResult = await _priceCalculator.GetFinalPriceAsync(
                                        model.StartLatitude.Value,
                                        model.StartLongitude.Value,
                                        transport.Id
                                    );
                                }
                            }
                        }
                        else
                        {
                            // Phương tiện công cộng: tính đến Đà Lạt như cũ
                            priceResult = await _priceCalculator.GetFinalPriceAsync(
                                model.StartLatitude.Value,
                                model.StartLongitude.Value,
                                transport.Id
                            );
                        }

                        transportCostMap[transport.Id] = priceResult.Price;

                        if (primaryTransportPriceResult == null && transport.Id == primaryTransportId)
                        {
                            primaryTransportPriceResult = priceResult;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (transport.Id == primaryTransportId)
                        {
                            ViewBag.TransportWarning = $"⚠️ Lỗi tính toán giá vận chuyển: {ex.Message}. " +
                                                       "Sử dụng giá mặc định.";
                        }
                    }
                }
            }

            // Nếu không có kết quả cho phương tiện ưu tiên nhưng vẫn có dữ liệu, lấy entry đầu tiên
            if (primaryTransportPriceResult == null && transportCostMap.Any())
            {
                var fallback = transportCostMap.First();
                primaryTransportId = fallback.Key;
                primaryTransportPriceResult = new TransportPriceResult
                {
                    Price = fallback.Value,
                    PriceType = "Calculated",
                    Note = $"Giá ước tính dựa trên khoảng cách {model.DistanceKm:F1}km. Vui lòng liên hệ nhà xe để biết giá chính xác."
                };
            }

            if (primaryTransportPriceResult != null)
            {
                var transportPriceResult = primaryTransportPriceResult;
                ViewBag.TransportPriceResult = transportPriceResult;
                ViewBag.TransportPrice = transportPriceResult.Price;
                ViewBag.TransportNote = transportPriceResult.Note;
                ViewBag.TransportPriceType = transportPriceResult.PriceType;
                ViewBag.IsMergedLocation = transportPriceResult.IsMergedLocation;
                ViewBag.OldLocationName = transportPriceResult.OldLocationName;
                ViewBag.LocationName = transportPriceResult.LocationName;

                if (transportPriceResult.PriceType == "Calculated")
                {
                    ViewBag.TransportWarning = "⚠️ Giá vận chuyển được tính ước lượng theo khoảng cách. " +
                                              "Vui lòng liên hệ nhà xe để biết giá chính xác.";
                }

                if (transportPriceResult.LocationId.HasValue &&
                    transportPriceResult.DistanceFromLocation.HasValue &&
                    transportPriceResult.DistanceFromLocation > 10)
                {
                    ViewBag.LocationSuggestion =
                        $"📍 Bạn có muốn xuất phát từ {transportPriceResult.LocationName}? " +
                        $"(Cách vị trí của bạn {transportPriceResult.DistanceFromLocation:F1}km)";
                }

                if (transportPriceResult.IsMergedLocation &&
                    !string.IsNullOrEmpty(transportPriceResult.OldLocationName))
                {
                    ViewBag.MergeInfo =
                        $"ℹ️ {transportPriceResult.OldLocationName} đã sáp nhập vào " +
                        $"{transportPriceResult.LocationName} từ 01/07/2025. " +
                        $"Giá vận chuyển từ khu vực {transportPriceResult.OldLocationName} cũ.";
                }
            }

            // ✅ BƯỚC 2: TRUYỀN GIÁ ĐÃ TÍNH VÀO THUẬT TOÁN THÔNG MINH
            // ✅ FIX #4: Gọi hàm tính toán với validation
            var (suggestions, autoFillMessage) = await GenerateSmartSuggestionsWithValidation(
                model,
                selectedPlaceIds,
                transportCostMap // ← TRUYỀN GIÁ ĐÃ TÍNH CHO TỪNG PHƯƠNG TIỆN
            );
            model.Suggestions = suggestions;

            // ✅ MỚI: Tính khoảng cách từ vị trí bắt đầu đến điểm đầu tiên được gợi ý
            if (model.StartLatitude.HasValue && model.StartLongitude.HasValue)
            {
                TouristPlace firstPlace = null;

                if (selectedPlaceIds != null && selectedPlaceIds.Any())
                {
                    // Nếu người dùng đã chọn địa điểm, lấy địa điểm đầu tiên từ danh sách đã chọn
                    var firstPlaceId = selectedPlaceIds.First();
                    firstPlace = model.TouristPlaces.FirstOrDefault(p => p.Id == firstPlaceId);
                }
                else
                {
                    // Nếu hệ thống tự động gợi ý, lấy địa điểm đầu tiên từ ranked places
                    // Tính lại ranked places để lấy điểm đầu tiên theo logic gợi ý
                    var candidatePlaces = _context.TouristPlaces
                        .AsNoTracking()
                        .Where(p => model.SelectedCategoryId == null || p.CategoryId == model.SelectedCategoryId)
                        .ToList();

                    if (candidatePlaces.Any())
                    {
                        var rankedPlaces = _recommendationService.RankPlaces(
                            candidatePlaces,
                            model.SelectedCategoryId,
                            model.StartLatitude,
                            model.StartLongitude
                        );

                        if (rankedPlaces.Any())
                        {
                            firstPlace = rankedPlaces.First();
                        }
                    }
                }

                if (firstPlace != null)
                {
                    // ✅ FIX: Dùng OSRM để tính khoảng cách thực tế (giống như JavaScript trên bản đồ)
                    // Thay vì dùng Haversine (đường chim bay) để đồng bộ với km hiển thị trên bản đồ
                    var distanceKmResult = await _osrmService.GetDistanceKmAsync(
                        model.StartLatitude.Value,
                        model.StartLongitude.Value,
                        firstPlace.Latitude,
                        firstPlace.Longitude
                    );

                    double distanceKm;
                    if (distanceKmResult.HasValue && distanceKmResult.Value > 0)
                    {
                        // Dùng OSRM (đường đi thực tế) - giống như trên bản đồ
                        distanceKm = distanceKmResult.Value;
                    }
                    else
                    {
                        // Fallback về Haversine nếu OSRM lỗi
                        distanceKm = _recommendationService.GetDistance(
                            model.StartLatitude.Value,
                            model.StartLongitude.Value,
                            firstPlace.Latitude,
                            firstPlace.Longitude
                        );
                    }

                    ViewBag.FirstPlaceDistanceKm = Math.Round(distanceKm, 1);
                    ViewBag.FirstPlaceName = firstPlace.Name;
                }
            }

            // Hiển thị thông báo auto-fill nếu có
            if (!string.IsNullOrEmpty(autoFillMessage))
            {
                ViewBag.AutoFillMessage = autoFillMessage;
            }

            // ✅ BƯỚC 3: NẾU KHÔNG CÓ KẾT QUẢ, HIỂN THỊ CẢNH BÁO (KHÔNG FALLBACK VỀ LOGIC CŨ)
            if (!model.Suggestions.Any())
            {
                model.Suggestions = GenerateBudgetWarning(
                    model.Budget,
                    model.NumberOfDays > 0 ? model.NumberOfDays : CalculateRecommendedDays(selectedPlaceIds.Count),
                    selectedPlaceIds.Count
                );
            }

            return View(model);
        }

        private List<string> CalculateSuggestions(TripPlannerViewModel model, List<string> selectedPlaceIds, bool onlyTopCheapest = false)
        {
            var suggestions = new List<string>();
            var warnings = new List<string>();
            decimal originalBudget = model.Budget;

            if (originalBudget <= 0)
                return new List<string> { "⚠️ Vui lòng nhập ngân sách hợp lệ." };

            if (string.IsNullOrEmpty(model.StartLocation))
                return new List<string> { "⚠️ Vui lòng nhập vị trí bắt đầu." };

            int recommendedDays = CalculateRecommendedDays(selectedPlaceIds.Count);
            int actualDays = model.NumberOfDays > 0 ? model.NumberOfDays : recommendedDays;

            // Cảnh báo khi số ngày quá nhiều so với địa điểm
            if (actualDays > selectedPlaceIds.Count * 2)
            {
                warnings.Add($"⚠️ Gợi ý: {selectedPlaceIds.Count} địa điểm với {actualDays} ngày có thể dư thừa. " +
                            $"Bạn sẽ có nhiều thời gian nghỉ ngơi và khám phá sâu hơn mỗi địa điểm.");
            }
            else if (actualDays > recommendedDays + 2)
            {
                warnings.Add($"⚠️ Gợi ý: {selectedPlaceIds.Count} địa điểm nên đi {recommendedDays}-{recommendedDays + 1} ngày thay vì {actualDays} ngày để tối ưu chi phí.");
            }

            double distanceKm = model.DistanceKm > 0 ? model.DistanceKm : 1;
            bool isFromOtherCity = !(model.StartLocation?.Contains("Đà Lạt") ?? false);

            if (!ValidateSelectedPlaces(selectedPlaceIds))
            {
                warnings.Add("• Bạn chưa chọn địa điểm cụ thể — hệ thống sẽ lập kế hoạch tổng quát cho Đà Lạt dựa trên ngân sách và số ngày.");
            }

            // Tìm location legacy theo toạ độ trước (nếu có), fallback sang theo tên (ưu tiên tên hiện tại)
            var legacyLocation = _context.LegacyLocations.AsEnumerable()
                .OrderBy(l =>
                {
                    // Nếu có toạ độ xuất phát, dùng khoảng cách để ưu tiên tỉnh gần nhất
                    if (model.StartLatitude.HasValue && model.StartLongitude.HasValue)
                    {
                        return _recommendationService.GetDistance(model.StartLatitude.Value, model.StartLongitude.Value, l.Latitude, l.Longitude);
                    }
                    // Nếu không có toạ độ, khoảng cách = +inf (sẽ dùng bước lọc theo tên bên dưới)
                    return double.MaxValue;
                })
                .ThenBy(l => l.CurrentName) // ổn định thứ tự
                .FirstOrDefault();

            // Nếu không có toạ độ hoặc toạ độ không đáng tin (cách rất xa), dùng khớp theo tên
            bool needNameMatch = legacyLocation == null
                                  || !(model.StartLatitude.HasValue && model.StartLongitude.HasValue)
                                  || _recommendationService.GetDistance(model.StartLatitude ?? 0, model.StartLongitude ?? 0, legacyLocation.Latitude, legacyLocation.Longitude) > 200; // >200km coi như không dùng được

            if (needNameMatch)
            {
                legacyLocation = _context.LegacyLocations
                    .AsEnumerable()
                    .Select(l => new
                    {
                        Item = l,
                        MatchCurrent = !string.IsNullOrEmpty(model.StartLocation) && !string.IsNullOrEmpty(l.CurrentName) &&
                                       model.StartLocation.ToLower().Contains(l.CurrentName.ToLower()),
                        MatchOld = !string.IsNullOrEmpty(model.StartLocation) && !string.IsNullOrEmpty(l.OldName) &&
                                   model.StartLocation.ToLower().Contains(l.OldName.ToLower())
                    })
                    .Where(x => x.MatchCurrent || x.MatchOld)
                    .OrderByDescending(x => x.MatchCurrent)
                    .Select(x => x.Item)
                    .FirstOrDefault();
            }

            // Lấy danh sách phương tiện phù hợp
            var mainTransports = model.TransportOptions
                .Where(t => isFromOtherCity ? !t.Name.Contains("Taxi nội thành")
                                            : t.Name.Contains("Taxi nội thành") || t.IsSelfDrive)
                .ToList();

            // Áp dụng bộ lọc phương tiện nếu người dùng đã chọn
            if (model.SelectedTransportId.HasValue)
            {
                mainTransports = mainTransports
                    .Where(t => t.Id == model.SelectedTransportId.Value)
                    .ToList();
            }

            if (!mainTransports.Any())
                return new List<string> { "Không có phương tiện phù hợp với vị trí xuất phát." };

            // Lặp qua từng phương tiện để tính toán
            foreach (var transport in mainTransports)
            {
                try
                {
                    // 1. Tính chi phí vận chuyển chính
                    decimal transportCost = CalculateTransportCost(transport, legacyLocation, distanceKm,
                        selectedPlaceIds, actualDays, isFromOtherCity);

                    // 2. Lấy danh sách địa điểm đã chọn
                    // Tối ưu: Sử dụng AsNoTracking() vì chỉ đọc
                    var selectedPlaces = _context.TouristPlaces
                        .AsNoTracking()
                        .Where(p => selectedPlaceIds.Contains(p.Id))
                        .ToList();

                    // 3. ✅ Sử dụng ItineraryService để tạo lịch trình tối ưu
                    double startLat = 11.940419;  // Tạm tọa độ trung tâm Đà Lạt
                    double startLng = 108.458313;
                    var route = _itineraryService.OptimizeRoute(startLat, startLng, selectedPlaces);

                    // 4. Tính toán ngân sách theo thứ tự mới:
                    // 1. Vận chuyển xa (đã tính) - Trừ thẳng (Real Cost)
                    // 2. Vé tham quan - Trừ thẳng (Real Cost)
                    // 3. Di chuyển nội thành - Trừ thẳng (Estimated)
                    // 4. Phát sinh - Giữ lại 10% Tổng ngân sách
                    // 5. Khách sạn - Trần 40% - 45% (của số dư còn lại)
                    // 6. Ăn uống - 100% (Phần còn lại)

                    // Cảnh báo nếu vận chuyển > 40% tổng ngân sách
                    if (transportCost > originalBudget * 0.4m)
                    {
                        warnings.Add($"⚠️ Cảnh báo: Chi phí vận chuyển ({FormatCurrency(transportCost)}) chiếm {Math.Round(transportCost / originalBudget * 100, 1)}% tổng ngân sách. " +
                                    $"Nên tăng ngân sách để có đủ tiền cho các khoản khác.");
                    }

                    decimal remainingBudget = originalBudget - transportCost;
                    if (remainingBudget <= 0) continue;

                    // 2. Chi phí vé tham quan - Trừ thẳng (Real Cost)
                    var ticketCalculation = _costCalculationService.CalculateTicketCosts(selectedPlaceIds);
                    decimal ticketCost = ticketCalculation.TotalCost;
                    var ticketDetails = ticketCalculation.TicketDetails;
                    var ticketWarnings = ticketCalculation.Warnings;

                    remainingBudget -= ticketCost;
                    if (remainingBudget <= 0) continue;

                    // 3. Chi phí di chuyển nội thành - Trừ thẳng (Estimated)
                    // Ước tính ban đầu: 150k-300k/ngày, lấy trung bình 225k/ngày
                    decimal localTransportCost = actualDays * 225000m;
                    List<string> localTransportDetails = new List<string>
                    {
                        $"Ước tính: {actualDays} ngày × {FormatCurrency(225000)}/ngày = {FormatCurrency(localTransportCost)}"
                    };

                    // Ước tính chính xác hơn nếu có thể (sẽ tính lại sau khi có hotel)
                    var tempHotel = _context.Hotels
                        .AsNoTracking()
                        .FirstOrDefault();
                    if (tempHotel != null)
                    {
                        var estimatedLocalTransportCalc = CalculateLocalTransportCosts(
                            selectedPlaceIds, actualDays, tempHotel, transport);
                        if (estimatedLocalTransportCalc.TotalCost > 0)
                        {
                            localTransportCost = estimatedLocalTransportCalc.TotalCost;
                            localTransportDetails = estimatedLocalTransportCalc.Details;
                        }
                    }

                    remainingBudget -= localTransportCost;
                    if (remainingBudget <= 0) continue;

                    // 4. Chi phí phát sinh - Giữ lại 10% Tổng ngân sách
                    decimal miscCost = originalBudget * 0.1m;

                    remainingBudget -= miscCost;
                    if (remainingBudget <= 0) continue;

                    // 5. Chi phí khách sạn - Trần 40% - 45% (của số dư còn lại)
                    // Sử dụng 42.5% làm trung bình (giữa 40% và 45%)
                    var clusters = _itineraryService.ClusterPlacesByDistance(selectedPlaces, actualDays);
                    decimal hotelBudget = remainingBudget * 0.425m;
                    var hotelCalculation = _costCalculationService.CalculateOptimizedHotelCosts(
                        selectedPlaceIds, hotelBudget, actualDays, clusters);
                    decimal hotelCost = hotelCalculation.TotalCost;
                    string hotelDetails = hotelCalculation.Details;
                    var selectedHotels = hotelCalculation.SelectedHotels;

                    // Đảm bảo không vượt quá 45% số dư còn lại
                    decimal maxHotelCost = remainingBudget * 0.45m;
                    if (hotelCost > maxHotelCost)
                    {
                        hotelCost = maxHotelCost;
                    }

                    remainingBudget -= hotelCost;
                    if (remainingBudget <= 0) continue;

                    // ✅ TẠO DailyItinerary để đồng bộ tất cả các thành phần (sau khi có hotel)
                    List<DailyItinerary> dailyItinerary = null;
                    List<string> distanceWarnings = new List<string>();
                    if (selectedPlaceIds != null && selectedPlaceIds.Any())
                    {
                        var places = _context.TouristPlaces
                            .AsNoTracking()
                            .Where(p => selectedPlaceIds.Contains(p.Id))
                            .ToList();
                        if (places.Any())
                        {
                            var itineraryClusters = _itineraryService.ClusterPlacesByDistance(places, actualDays);
                            var (itinerary, itineraryWarnings) = _itineraryService.BuildDailyItinerary(places, actualDays, selectedHotels, itineraryClusters);
                            dailyItinerary = itinerary;
                            distanceWarnings = itineraryWarnings;

                            // Tính lại chi phí di chuyển nội thành chính xác hơn với dailyItinerary
                            if (dailyItinerary != null && dailyItinerary.Any())
                            {
                                var accurateLocalTransportCalc = CalculateLocalTransportCosts(dailyItinerary, transport);
                                if (accurateLocalTransportCalc.TotalCost > 0)
                                {
                                    // Điều chỉnh lại remainingBudget nếu có thay đổi
                                    decimal oldLocalTransportCost = localTransportCost;
                                    localTransportCost = accurateLocalTransportCalc.TotalCost;
                                    localTransportDetails = accurateLocalTransportCalc.Details;

                                    // Điều chỉnh lại remainingBudget
                                    remainingBudget += (oldLocalTransportCost - localTransportCost);
                                }
                            }
                        }
                    }

                    // 6. Chi phí ăn uống - 100% (Phần còn lại)
                    // "Thừa thiếu bù trừ" dồn hết vào đây để ăn ngon
                    decimal foodCost;
                    string foodDetails;
                    if (dailyItinerary != null && dailyItinerary.Any())
                    {
                        var foodCalculation = CalculateOptimizedFoodCosts(dailyItinerary, remainingBudget);
                        foodCost = foodCalculation.TotalCost;
                        foodDetails = foodCalculation.Details;
                    }
                    else
                    {
                        var foodCalculation = CalculateOptimizedFoodCosts(selectedPlaceIds, remainingBudget, actualDays);
                        foodCost = foodCalculation.TotalCost;
                        foodDetails = foodCalculation.Details;
                    }

                    // Tính tổng chi phí cuối cùng
                    decimal totalCost = transportCost + ticketCost + localTransportCost +
                                       miscCost + hotelCost + foodCost;

                    // 11. Kiểm tra ngân sách
                    if (totalCost > originalBudget) continue;

                    decimal remaining = originalBudget - totalCost;

                    // 12. Tạo chi tiết lịch trình - ✅ ĐỒNG BỘ với dailyItinerary
                    string routeDetails;
                    if (dailyItinerary != null && dailyItinerary.Any())
                    {
                        // Sử dụng dailyItinerary để đảm bảo đồng bộ với lịch trình thực tế
                        // ✅ Sử dụng ItineraryService
                        routeDetails = _itineraryService.GenerateRouteDetailsFromItinerary(dailyItinerary);
                    }
                    else
                    {
                        // Fallback: sử dụng route đơn giản nếu không có dailyItinerary
                        routeDetails = string.Join("<br/>", route.Select((p, idx) => $"{idx + 1}. {p.Name}"));
                    }

                    // 13. Tạo cluster details - hiển thị phân bổ thời gian dựa trên địa điểm
                    // ✅ FIX: Tái tạo clusters từ dailyItinerary để đảm bảo đồng bộ
                    string clusterDetails = "";
                    if (dailyItinerary != null && dailyItinerary.Any())
                    {
                        // Tái tạo clusters từ dailyItinerary (đảm bảo đồng bộ) - đổi tên biến để tránh conflict
                        var reconstructedClusters = ReconstructClustersFromItinerary(dailyItinerary);

                        if (reconstructedClusters.Any())
                        {
                            clusterDetails = "<br/><b>📍 Phân bổ thời gian:</b><br/>";

                            // ✅ FIX: Sử dụng số ngày thực tế từ dailyItinerary (đã đồng bộ)
                            var groupedByCluster = dailyItinerary
                                .Where(d => d.ClusterIndex >= 0)
                                .GroupBy(d => d.ClusterIndex)
                                .OrderBy(g => g.Key)
                                .ToList();

                            // ✅ FIX: Tính số đêm chính xác để tổng = tổng số ngày - 1
                            int totalDays = dailyItinerary.Count;
                            int expectedTotalNights = Math.Max(0, totalDays - 1);

                            int phaseNumber = 1;
                            int totalNightsCalculated = 0;

                            foreach (var clusterGroup in groupedByCluster)
                            {
                                var cluster = reconstructedClusters[clusterGroup.Key];
                                int daysInCluster = clusterGroup.Count();

                                // Tính số đêm cho cluster này
                                int nightsInCluster;
                                bool isLastCluster = clusterGroup.Key == groupedByCluster.Last().Key;

                                if (isLastCluster)
                                {
                                    // Cluster cuối: nhận phần còn lại để tổng đúng
                                    nightsInCluster = Math.Max(0, expectedTotalNights - totalNightsCalculated);
                                }
                                else
                                {
                                    // Các cluster khác: số đêm = số ngày (bao gồm đêm sau ngày cuối trước khi chuyển)
                                    nightsInCluster = daysInCluster;
                                    totalNightsCalculated += nightsInCluster;
                                }

                                // ✅ FIX: Hiển thị số ngày thực tế từ dailyItinerary
                                clusterDetails += $"Giai đoạn {phaseNumber}: {daysInCluster} ngày " +
                                                $"({nightsInCluster} đêm) tại " +
                                                $"{string.Join(", ", cluster.Places.Take(2).Select(p => p.Name))}" +
                                                $"{(cluster.Places.Count > 2 ? "..." : "")}<br/>";
                                phaseNumber++;
                            }
                        }
                    }
                    else if (selectedPlaceIds != null && selectedPlaceIds.Any())
                    {
                        // Fallback: Nếu không có dailyItinerary, tạo clusters như cũ
                        var places = _context.TouristPlaces.Where(p => selectedPlaceIds.Contains(p.Id)).ToList();
                        if (places.Any())
                        {
                            // ✅ Sử dụng ItineraryService - đổi tên biến để tránh conflict
                            var fallbackClusters = _itineraryService.ClusterPlacesByDistance(places, actualDays);
                            if (fallbackClusters.Any())
                            {
                                clusterDetails = "<br/><b>📍 Phân bổ thời gian:</b><br/>";

                                var validClusters = fallbackClusters.Where(c => c.RecommendedNights >= 0).ToList();
                                int phaseNumber = 1;
                                int totalNights = validClusters.Sum(c => c.RecommendedNights);

                                // Tính số ngày cho mỗi cluster dựa trên tỷ lệ RecommendedNights
                                var daysAllocation = new int[validClusters.Count];
                                int allocatedDays = 0;

                                for (int i = 0; i < validClusters.Count; i++)
                                {
                                    var cluster = validClusters[i];
                                    int daysToShow;

                                    if (i == validClusters.Count - 1)
                                    {
                                        daysToShow = actualDays - allocatedDays;
                                        if (daysToShow < 1) daysToShow = 1;
                                    }
                                    else
                                    {
                                        if (totalNights > 0)
                                        {
                                            double ratio = (double)cluster.RecommendedNights / totalNights;
                                            daysToShow = (int)Math.Round(ratio * actualDays);
                                            int daysRemaining = actualDays - allocatedDays - (validClusters.Count - i - 1);
                                            daysToShow = Math.Min(daysToShow, daysRemaining);
                                        }
                                        else
                                        {
                                            daysToShow = actualDays / validClusters.Count;
                                        }
                                        if (daysToShow < 1) daysToShow = 1;
                                    }

                                    daysAllocation[i] = daysToShow;
                                    allocatedDays += daysToShow;
                                }

                                if (allocatedDays != actualDays)
                                {
                                    int diff = actualDays - allocatedDays;
                                    daysAllocation[validClusters.Count - 1] += diff;
                                    if (daysAllocation[validClusters.Count - 1] < 1)
                                    {
                                        daysAllocation[validClusters.Count - 1] = 1;
                                    }
                                }

                                for (int i = 0; i < validClusters.Count; i++)
                                {
                                    var cluster = validClusters[i];
                                    int daysToShow = daysAllocation[i];

                                    clusterDetails += $"Giai đoạn {phaseNumber}: {daysToShow} ngày " +
                                                    $"({cluster.RecommendedNights} đêm) tại " +
                                                    $"{string.Join(", ", cluster.Places.Take(2).Select(p => p.Name))}" +
                                                    $"{(cluster.Places.Count > 2 ? "..." : "")}<br/>";
                                    phaseNumber++;
                                }
                            }
                        }
                    }

                    // 13.1 Nếu không chọn địa điểm: thêm đích đến Đà Lạt và gợi ý địa điểm để người dùng chọn
                    if (selectedPlaceIds == null || !selectedPlaceIds.Any())
                    {
                        var suggested = SuggestDefaultPlaces(6);
                        if (suggested.Any())
                        {
                            var suggestionsList = string.Join("<br/>", suggested.Select(p => $"• {p.Name}"));
                            clusterDetails = $"<br/><b>📍 Đích đến: Đà Lạt</b><br/><b>📌 Gợi ý địa điểm để chọn:</b><br/>{suggestionsList}";
                        }
                        else
                        {
                            clusterDetails = $"<br/><b>📍 Đích đến: Đà Lạt</b>";
                        }
                    }

                    // 13.2 Nếu không chọn địa điểm: chỉ hiển thị chi phí đến Đà Lạt và gợi ý địa điểm
                    bool basicOnly = selectedPlaceIds == null || !selectedPlaceIds.Any();
                    if (basicOnly)
                    {
                        // Ẩn toàn bộ các chi phí khác, chỉ giữ chi phí đến Đà Lạt
                        hotelCost = 0; foodCost = 0; ticketCost = 0; localTransportCost = 0; miscCost = 0;
                        totalCost = transportCost;
                        remaining = originalBudget - totalCost;
                        routeDetails = string.Empty;
                        ticketDetails = new List<string>();
                        localTransportDetails = new List<string>();
                        ticketWarnings = new List<string>();
                        warnings.Clear();
                    }

                    // 14. Format suggestion (truyền DailyItinerary để hiển thị chi tiết)
                    // ✅ Gộp cảnh báo khoảng cách vào warnings
                    var allWarnings = new List<string>(ticketWarnings ?? new List<string>());
                    allWarnings.AddRange(distanceWarnings);

                    string suggestion = FormatOptimizedSuggestionInternal(
                        model,
                        transport, transportCost,
                        hotelDetails, hotelCost,
                        foodDetails, foodCost,
                        ticketCost, localTransportCost, miscCost,
                        totalCost, remaining, actualDays,
                        ticketDetails, localTransportDetails, allWarnings,
                        routeDetails, clusterDetails, basicOnly,
                        dailyItinerary); // ← Thêm DailyItinerary

                    suggestions.Add(suggestion);
                }
                catch (Exception ex)
                {
                    suggestions.Add($"⚠️ Lỗi tính toán cho phương tiện {transport.Name}: {ex.Message}");
                }
            }

            // 15. Xử lý kết quả
            if (!suggestions.Any())
                return GenerateBudgetWarning(originalBudget, actualDays, selectedPlaceIds.Count);

            // 16. Loại bỏ duplicate và sắp xếp
            suggestions = RemoveDuplicateSuggestions(suggestions)
                         .OrderBy(s => ExtractTotalCost(s))
                         .Take(5).ToList();

            // 17. Thêm warnings nếu có
            if (warnings.Any())
            {
                var finalResults = new List<string>();
                finalResults.AddRange(warnings);
                finalResults.AddRange(suggestions);
                return finalResults;
            }

            return suggestions;
        }


        // NEW: Calculate local transport costs within Da Lat with advanced logic

        private (decimal TotalCost, List<string> Details) CalculateLocalTransportCosts(
    List<string> selectedPlaceIds,
    int days,
    Hotel selectedHotel,
    TransportOption selectedTransport = null)
        {
            var details = new List<string>();
            decimal totalCost = 0;

            // Validate inputs
            if (selectedPlaceIds == null || !selectedPlaceIds.Any())
            {
                details.Add("• Không có địa điểm để di chuyển");
                return (50000 * days, details);
            }

            if (days <= 0)
            {
                details.Add("• Số ngày không hợp lệ");
                return (0, details);
            }

            // Get tourist places
            var places = _context.TouristPlaces
                .Where(p => selectedPlaceIds.Contains(p.Id))
                .ToList();

            if (!places.Any())
            {
                decimal defaultCost = 50000 * days;
                details.Add($"• Di chuyển nội thành (ước tính): {FormatCurrency(defaultCost)}");
                return (defaultCost, details);
            }

            // Hotel coordinates - lấy trực tiếp từ Hotel
            double startLat = selectedHotel?.Latitude ?? 11.940419;
            double startLng = selectedHotel?.Longitude ?? 108.458313;

            try
            {
                // SỬA: Gọi hàm với tham số days chính xác
                if (selectedTransport != null && selectedTransport.IsSelfDrive)
                {
                    return CalculatePersonalVehicleTransportWithFullSchedule(places, days, startLat, startLng, selectedTransport, details);
                }

                return CalculateTaxiTransportWithFullSchedule(places, days, startLat, startLng, details);
            }
            catch (Exception ex)
            {
                details.Add($"• Lỗi tính toán di chuyển: {ex.Message}");
                decimal fallbackCost = days * 100000;
                return (fallbackCost, details);
            }
        }

        // ✅ BƯỚC 5: SỬA CalculateLocalTransportCosts - Dùng DailyItinerary (Overload mới)
        /// <summary>
        /// Tính chi phí di chuyển nội thành dựa trên DailyItinerary (đồng bộ với lịch trình)
        /// </summary>
        private (decimal TotalCost, List<string> Details) CalculateLocalTransportCosts(
            List<DailyItinerary> dailyItinerary,
            TransportOption selectedTransport = null)
        {
            var details = new List<string>();
            decimal totalCost = 0;
            double totalDistance = 0;
            var allDayRoutes = new List<string>();

            foreach (var dayPlan in dailyItinerary)
            {
                int displayDay = dayPlan.DayNumber;
                var dayPlaces = dayPlan.Places;
                var hotel = dayPlan.Hotel;

                if (!dayPlaces.Any())
                {
                    allDayRoutes.Add($"Ngày {displayDay}: Nghỉ ngơi (~0 km)");
                    continue;
                }

                double startLat = hotel?.Latitude ?? 11.940419;
                double startLng = hotel?.Longitude ?? 108.458313;

                var (dayDistance, routeDescription) = CalculateDayRoute(
                    dayPlaces, startLat, startLng, displayDay);
                totalDistance += dayDistance;
                allDayRoutes.Add(routeDescription);
            }

            // Tính chi phí dựa trên totalDistance
            if (selectedTransport != null && selectedTransport.IsSelfDrive)
            {
                if (selectedTransport.FuelConsumption > 0 && selectedTransport.FuelPrice > 0)
                {
                    decimal fuelUsed = ((decimal)totalDistance * selectedTransport.FuelConsumption) / 100m;
                    totalCost = fuelUsed * selectedTransport.FuelPrice;
                    details.Add($"• {selectedTransport.Name} (phương tiện cá nhân)");
                    details.AddRange(allDayRoutes);
                    details.Add($"↳ Tổng quãng đường: ~{totalDistance:F1} km");
                    details.Add($"↳ Nhiên liệu: {fuelUsed:F2} lít × {selectedTransport.FuelPrice:N0}đ = {FormatCurrency(totalCost)}");
                }
                else
                {
                    totalCost = (decimal)totalDistance * 3000; // 3,000đ/km ước tính
                    details.Add($"• {selectedTransport.Name} (ước tính)");
                    details.AddRange(allDayRoutes);
                    details.Add($"↳ Chi phí ước tính: {FormatCurrency(totalCost)} (~{totalDistance:F1} km)");
                }
            }
            else
            {
                // Taxi nội thành
                decimal taxiRatePerKm = 15000; // 15,000đ/km
                totalCost = (decimal)totalDistance * taxiRatePerKm;
                details.Add("• Taxi nội thành");
                details.AddRange(allDayRoutes);
                details.Add($"↳ Tổng quãng đường: {totalDistance:F1} km × {taxiRatePerKm:N0}đ/km = {FormatCurrency(totalCost)}");

                if (totalDistance > 50)
                {
                    details.Add($"↳ Lưu ý: Quãng đường dài, có thể thương lượng giá theo ngày");
                }
            }

            return (totalCost, details);
        }

        // Helper methods cho CalculateLocalTransportCosts (giữ nguyên như đã viết trước đó)
        private (decimal TotalCost, List<string> Details) CalculatePersonalVehicleTransport(
            List<TouristPlace> places,
            int days,
            double startLat,
            double startLng,
            TransportOption transport,
            List<string> details)
        {
            decimal totalCost = 0;
            double totalDistance = 0;
            var allDayRoutes = new List<string>();

            // Distribute places across days
            // ✅ Sử dụng ItineraryService
            var dailyPlaces = _itineraryService.DistributePlacesAcrossDays(places, days);

            for (int day = 1; day <= days; day++)
            {
                var dayPlaces = dailyPlaces.ElementAtOrDefault(day - 1) ?? new List<TouristPlace>();

                if (!dayPlaces.Any())
                {
                    allDayRoutes.Add($"Ngày {day}: Nghỉ ngơi/khám phá khu vực (~5 km)");
                    totalDistance += 5;
                    continue;
                }

                var (dayDistance, routeDescription) = CalculateDayRoute(dayPlaces, startLat, startLng, day);
                totalDistance += dayDistance;
                allDayRoutes.Add(routeDescription);
            }

            // Calculate fuel cost
            if (transport.FuelConsumption > 0 && transport.FuelPrice > 0)
            {
                decimal fuelUsed = ((decimal)totalDistance * transport.FuelConsumption) / 100m;
                totalCost = fuelUsed * transport.FuelPrice;

                details.Add($"• {transport.Name} (phương tiện cá nhân)");
                details.AddRange(allDayRoutes);
                details.Add($"↳ Tổng quãng đường: ~{totalDistance:F1} km");
                details.Add($"↳ Nhiên liệu: {fuelUsed:F2} lít × {transport.FuelPrice:N0}đ = {FormatCurrency(totalCost)}");
            }
            else
            {
                totalCost = (decimal)totalDistance * 3000;
                details.Add($"• {transport.Name} (ước tính)");
                details.AddRange(allDayRoutes);
                details.Add($"↳ Chi phí ước tính: {FormatCurrency(totalCost)}");
            }

            return (totalCost, details);
        }

        private (decimal TotalCost, List<string> Details) CalculateTaxiTransport(
            List<TouristPlace> places,
            int days,
            double startLat,
            double startLng,
            List<string> details)
        {
            decimal totalCost = 0;
            double totalDistance = 0;
            var allDayRoutes = new List<string>();
            decimal taxiRatePerKm = 15000;

            // ✅ Sử dụng ItineraryService
            var dailyPlaces = _itineraryService.DistributePlacesAcrossDays(places, days);

            for (int day = 1; day <= days; day++)
            {
                var dayPlaces = dailyPlaces.ElementAtOrDefault(day - 1) ?? new List<TouristPlace>();

                if (!dayPlaces.Any())
                {
                    allDayRoutes.Add($"Ngày {day}: Nghỉ ngơi (~0 km)");
                    continue;
                }

                var (dayDistance, routeDescription) = CalculateDayRoute(dayPlaces, startLat, startLng, day);
                totalDistance += dayDistance;
                allDayRoutes.Add(routeDescription);
            }

            totalCost = (decimal)totalDistance * taxiRatePerKm;

            details.Add("• Taxi nội thành");
            details.AddRange(allDayRoutes);
            details.Add($"↳ Tổng quãng đường: {totalDistance:F1} km × {taxiRatePerKm:N0}đ/km = {FormatCurrency(totalCost)}");

            return (totalCost, details);
        }

        private (double Distance, string RouteDescription) CalculateDayRoute(
            List<TouristPlace> dayPlaces,
            double startLat,
            double startLng,
            int dayNumber)
        {
            if (!dayPlaces.Any())
                return (0, $"Ngày {dayNumber}: Nghỉ ngơi");

            double totalDayDistance = 0;
            var routeParts = new List<string> { "Khách sạn" };

            double currentLat = startLat;
            double currentLng = startLng;

            foreach (var place in dayPlaces)
            {
                try
                {
                    double distanceToPlace = _recommendationService.GetDistance(currentLat, currentLng, place.Latitude, place.Longitude);
                    totalDayDistance += distanceToPlace;
                    routeParts.Add($"{place.Name} (~{distanceToPlace:F1} km)");

                    currentLat = place.Latitude;
                    currentLng = place.Longitude;
                }
                catch (Exception)
                {
                    routeParts.Add($"{place.Name} (lỗi tọa độ)");
                    totalDayDistance += 5;
                }
            }

            // Return to hotel
            try
            {
                double returnDistance = _recommendationService.GetDistance(currentLat, currentLng, startLat, startLng);
                totalDayDistance += returnDistance;
                routeParts.Add($"Khách sạn (~{returnDistance:F1} km)");
            }
            catch (Exception)
            {
                totalDayDistance += 5;
                routeParts.Add("Khách sạn (~5 km)");
            }

            string routeDescription = $"Ngày {dayNumber}: {string.Join(" → ", routeParts)} | Tổng: ~{totalDayDistance:F1} km";
            return (totalDayDistance, routeDescription);
        }






        // ============= TÍNH QUÃNG ĐƯỜNG CHIA NGÀY + KHỨ HỒI ============= 




        private (decimal Cost, List<string> Details) CalculateElectricShuttleStrategy(List<string> selectedPlaceIds, List<LocalTransport> localTransports, bool hasMultiplePlacesPerDay)
        {
            var details = new List<string>();
            decimal totalCost = 0;
            var electricPlaces = new List<string>();
            var nonElectricPlaces = new List<string>();

            // Separate places with electric shuttle vs without
            foreach (var placeId in selectedPlaceIds)
            {
                var electricShuttle = localTransports.FirstOrDefault(lt =>
                    lt.TransportType == TransportType.ElectricShuttle &&
                    (lt.TouristPlaceId == placeId || lt.TouristPlaceId == null));

                if (electricShuttle != null)
                {
                    electricPlaces.Add(placeId);
                    decimal cost = (electricShuttle.PricePerTrip ?? 50000) * 2; // Round trip
                    totalCost += cost;

                    // Tối ưu: Chỉ cần Name, sử dụng AsNoTracking()
                    var place = _context.TouristPlaces
                        .AsNoTracking()
                        .Select(p => new { p.Id, p.Name })
                        .FirstOrDefault(p => p.Id == placeId);
                    details.Add($"• {place?.Name}: Xe điện {FormatCurrency(cost)} (khứ hồi)");
                }
                else
                {
                    nonElectricPlaces.Add(placeId);
                }
            }

            // Add taxi cost for non-electric places
            if (nonElectricPlaces.Any())
            {
                var taxiTransport = localTransports.FirstOrDefault(lt => lt.TransportType == TransportType.LocalTaxi);
                decimal taxiCostPerKm = taxiTransport?.PricePerKm ?? 15000;

                foreach (var placeId in nonElectricPlaces)
                {
                    decimal estimatedKm = GetEstimatedDistance(placeId);
                    decimal tripCost = taxiCostPerKm * estimatedKm * 2; // Round trip

                    // If multiple places in a day, add inter-destination cost
                    if (hasMultiplePlacesPerDay)
                        tripCost += taxiCostPerKm * 3; // Average 3km between destinations

                    totalCost += tripCost;

                    // Tối ưu: Chỉ cần Name, sử dụng AsNoTracking()
                    var place = _context.TouristPlaces
                        .AsNoTracking()
                        .Select(p => new { p.Id, p.Name })
                        .FirstOrDefault(p => p.Id == placeId);
                    details.Add($"• {place?.Name}: Taxi {FormatCurrency(tripCost)} ({estimatedKm * 2}km)");
                }
            }

            return (totalCost, details);
        }
        private (decimal Cost, List<string> Details) CalculateTaxiStrategy(List<string> selectedPlaceIds, List<LocalTransport> localTransports, Hotel selectedHotel, bool hasMultiplePlacesPerDay)
        {
            var details = new List<string>();
            var taxiTransport = localTransports.FirstOrDefault(lt => lt.TransportType == TransportType.LocalTaxi);
            decimal taxiCostPerKm = taxiTransport?.PricePerKm ?? 15000;
            decimal totalCost = 0;

            if (hasMultiplePlacesPerDay)
            {
                // Calculate optimized route cost
                decimal totalDistance = 0;

                foreach (var placeId in selectedPlaceIds)
                {
                    totalDistance += GetEstimatedDistance(placeId);
                }

                // Add inter-destination distances
                if (selectedPlaceIds.Count > 1)
                {
                    totalDistance += (selectedPlaceIds.Count - 1) * 4; // Average 4km between places
                }

                // Add return to hotel
                totalDistance += GetEstimatedDistance(selectedPlaceIds.Last());

                totalCost = totalDistance * taxiCostPerKm;
                details.Add($"• Taxi cho tất cả điểm: {FormatCurrency(totalCost)} (~{totalDistance:F0}km)");
                details.Add($"  ↳ Bao gồm: di chuyển giữa các điểm + về khách sạn");
            }
            else
            {
                // Simple round trip for each place
                foreach (var placeId in selectedPlaceIds)
                {
                    decimal estimatedKm = GetEstimatedDistance(placeId);
                    decimal tripCost = taxiCostPerKm * estimatedKm * 2; // Round trip
                    totalCost += tripCost;

                    // Tối ưu: Chỉ cần Name, sử dụng AsNoTracking()
                    var place = _context.TouristPlaces
                        .AsNoTracking()
                        .Select(p => new { p.Id, p.Name })
                        .FirstOrDefault(p => p.Id == placeId);
                    details.Add($"• {place?.Name}: {FormatCurrency(tripCost)} ({estimatedKm * 2}km khứ hồi)");
                }
            }

            return (totalCost, details);
        }

        private (decimal Cost, List<string> Details) CalculateHotelShuttleStrategy(List<string> selectedPlaceIds, List<LocalTransport> localTransports, Hotel selectedHotel, bool hasMultiplePlacesPerDay)
        {
            var details = new List<string>();
            decimal totalCost = 0;
            var shuttlePlaces = new List<string>();
            var nonShuttlePlaces = new List<string>();

            // Check which places have hotel shuttle service
            foreach (var placeId in selectedPlaceIds)
            {
                var hotelShuttle = localTransports.FirstOrDefault(lt =>
                    lt.TransportType == TransportType.HotelShuttle &&
                    (lt.HotelId == selectedHotel.Id || lt.HotelId == null) &&
                    (lt.TouristPlaceId == placeId || lt.TouristPlaceId == null));

                if (hotelShuttle != null)
                {
                    shuttlePlaces.Add(placeId);
                    decimal cost = (hotelShuttle.PricePerTrip ?? 0) * 2; // Round trip
                    totalCost += cost;

                    // Tối ưu: Chỉ cần Name, sử dụng AsNoTracking()
                    var place = _context.TouristPlaces
                        .AsNoTracking()
                        .Select(p => new { p.Id, p.Name })
                        .FirstOrDefault(p => p.Id == placeId);
                    details.Add($"• {place?.Name}: Xe buýt KS {FormatCurrency(cost)} (khứ hồi)");
                }
                else
                {
                    nonShuttlePlaces.Add(placeId);
                }
            }

            // Add taxi for places without shuttle
            if (nonShuttlePlaces.Any())
            {
                var taxiStrategy = CalculateTaxiStrategy(nonShuttlePlaces, localTransports, selectedHotel, hasMultiplePlacesPerDay && nonShuttlePlaces.Count > 1);
                totalCost += taxiStrategy.Cost;
                details.AddRange(taxiStrategy.Details);
            }

            return shuttlePlaces.Any() ? (totalCost, details) : (0, new List<string>());
        }

        private (decimal Cost, List<string> Details) CalculateMotorbikeStrategy(List<LocalTransport> localTransports, int days)
        {
            var details = new List<string>();
            var motorbike = localTransports.Where(lt => lt.TransportType == TransportType.MotorbikeRental)
                                         .OrderBy(lt => lt.PricePerDay ?? decimal.MaxValue)
                                         .FirstOrDefault();

            if (motorbike == null)
                return (0, details);

            decimal dailyCost = motorbike.PricePerDay ?? 150000;
            decimal totalCost = dailyCost * days;

            details.Add($"• {motorbike.Name}: {FormatCurrency(totalCost)} ({days} ngày × {FormatCurrency(dailyCost)})");
            details.Add($"  ↳ Tự do di chuyển, phù hợp nhiều điểm/ngày");

            return (totalCost, details);
        }

        // Helper method to estimate distance from hotel to tourist place
        private decimal GetEstimatedDistance(string placeId)
        {
            // This could be enhanced with actual GPS coordinates calculation
            // For now, use estimated distances based on place type or location
            // Tối ưu: Chỉ cần Name để so sánh, sử dụng AsNoTracking()
            var place = _context.TouristPlaces
                .AsNoTracking()
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefault(p => p.Id == placeId);

            // Default distances (can be customized based on actual locations)
            return place?.Name?.ToLower() switch
            {
                var name when name.Contains("hồ xuân hương") => 2,
                var name when name.Contains("langbiang") => 12,
                var name when name.Contains("valley") => 8,
                var name when name.Contains("dalat flower") => 6,
                var name when name.Contains("crazy house") => 3,
                var name when name.Contains("bao dai") => 4,
                var name when name.Contains("linh phuoc") => 15,
                var name when name.Contains("elephant") => 8,
                _ => 5 // Default 5km
            };
        }

        // Updated method signature to include local transport
        private (decimal TotalCost, string Details, List<Hotel> SelectedHotels) CalculateOptimizedHotelCosts(
    List<string> selectedPlaceIds,
    decimal hotelBudget,
    int days)
        {
            int nights = days > 1 ? days - 1 : 0;
            if (nights == 0)
                return (0, "Chuyến đi trong ngày - không cần nghỉ đêm", new List<Hotel>());

            if (hotelBudget <= 0)
                return (0, "Không có ngân sách cho khách sạn", new List<Hotel>());

            var places = _context.TouristPlaces
                .AsNoTracking()
                .Where(p => selectedPlaceIds.Contains(p.Id))
                .ToList();

            if (!places.Any())
            {
                var details = new List<string>();
                var selectedHotels = new List<Hotel>();
                decimal totalCost = 0;
                decimal budgetPerNight = hotelBudget / nights;

                var candidates = _context.Hotels
                    .AsNoTracking()
                    .OrderBy(h => Math.Abs(h.PricePerNight - budgetPerNight))
                    .ThenBy(h => h.PricePerNight)
                    .Take(Math.Max(2, nights / 2))
                    .ToList();

                if (!candidates.Any())
                    return (0, "Không tìm thấy khách sạn phù hợp", new List<Hotel>());

                foreach (var hotel in candidates)
                {
                    int nightsForHotel = Math.Max(1, nights / candidates.Count);
                    decimal clusterCost = hotel.PricePerNight * nightsForHotel;
                    if (totalCost + clusterCost > hotelBudget)
                    {
                        nightsForHotel = (int)Math.Floor((hotelBudget - totalCost) / Math.Max(1, hotel.PricePerNight));
                        if (nightsForHotel <= 0) break;
                        clusterCost = hotel.PricePerNight * nightsForHotel;
                    }

                    selectedHotels.Add(hotel);
                    totalCost += clusterCost;
                    details.Add($"• {hotel.Name}: {nightsForHotel} đêm × {FormatCurrency(hotel.PricePerNight)}");

                    if (totalCost >= hotelBudget) break;
                }

                return (totalCost, string.Join("<br/>", details), selectedHotels);
            }

            // ✅ Sử dụng ItineraryService
            var clusters = _itineraryService.ClusterPlacesByDistance(places, days);

            if (!clusters.Any())
                return (0, "Không thể tạo cụm địa điểm", new List<Hotel>());

            var selectedHotels2 = new List<Hotel>();
            var details2 = new List<string>();
            decimal totalCost2 = 0;
            decimal budgetPerNight2 = hotelBudget / nights;

            // ✅ FIX CHÍNH: Phân bổ số đêm cho từng cluster
            // Tổng số đêm = days - 1 (VD: 6 ngày = 5 đêm)

            foreach (var cluster in clusters)
            {
                int nightsForCluster = cluster.RecommendedNights;

                // ✅ FIX: Cluster cuối cùng nhận hết số đêm còn lại
                if (cluster == clusters.Last())
                {
                    // Parse số đêm từ details2 đã thêm vào
                    int assignedNights = details2
                        .Select(line =>
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)\s+đêm");
                            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
                        })
                        .Sum();

                    nightsForCluster = nights - assignedNights;

                    // Đảm bảo không âm
                    if (nightsForCluster < 0) nightsForCluster = 0;
                }

                if (nightsForCluster <= 0) continue;

                decimal budgetForCluster = budgetPerNight2 * nightsForCluster;
                decimal maxPricePerNight = budgetForCluster / nightsForCluster;

                var hotel = FindBestHotelForCluster(cluster.Places, maxPricePerNight);

                if (hotel != null)
                {
                    // ✅ FIX: Kiểm tra trùng lặp khách sạn
                    if (!selectedHotels2.Contains(hotel))
                    {
                        selectedHotels2.Add(hotel);
                        decimal clusterCost = hotel.PricePerNight * nightsForCluster;
                        totalCost2 += clusterCost;

                        string locationNames = GetLocationNamesDisplay(cluster.Places);

                        // ✅ HIỂN THỊ CHỈ SỐ ĐÊM (không hiển thị số ngày để tránh nhầm lẫn)
                        details2.Add($"• {hotel.Name} ({locationNames}): " +
                                   $"{nightsForCluster} đêm × {FormatCurrency(hotel.PricePerNight)}");
                    }
                }
            }

            if (!selectedHotels2.Any())
            {
                var defaultHotel = _context.Hotels
                    .AsNoTracking()
                    .OrderBy(h => h.PricePerNight)
                    .FirstOrDefault();
                if (defaultHotel != null)
                {
                    selectedHotels2.Add(defaultHotel);
                    decimal defaultCost = Math.Min(defaultHotel.PricePerNight * nights, hotelBudget);
                    totalCost2 = defaultCost;
                    details2.Add($"• {defaultHotel.Name} (Mặc định): {nights} đêm × {FormatCurrency(defaultCost / nights)}");
                }
            }

            return (totalCost2, string.Join("<br/>", details2), selectedHotels2);
        }

        // ✅ MỚI: Tính chi phí khách sạn theo mức ngân sách
        private (decimal TotalCost, string Details, List<Hotel> SelectedHotels) CalculateOptimizedHotelCostsByBudgetPlan(
            List<string> selectedPlaceIds,
            decimal hotelBudget,
            int days,
            BudgetPlanType planType)
        {
            int nights = days > 1 ? days - 1 : 0;
            if (nights == 0)
                return (0, "Chuyến đi trong ngày - không cần nghỉ đêm", new List<Hotel>());

            if (hotelBudget <= 0)
                return (0, "Không có ngân sách cho khách sạn", new List<Hotel>());

            var places = _context.TouristPlaces
                .AsNoTracking()
                .Where(p => selectedPlaceIds.Contains(p.Id))
                .ToList();

            // ✅ Sử dụng ItineraryService và CostCalculationService
            List<PlaceCluster> clusters = null;
            if (places.Any())
            {
                clusters = _itineraryService.ClusterPlacesByDistance(places, days);
                if (clusters == null || !clusters.Any())
                    return _costCalculationService.CalculateOptimizedHotelCosts(selectedPlaceIds, hotelBudget, days, null);
            }
            else
            {
                return _costCalculationService.CalculateOptimizedHotelCosts(selectedPlaceIds, hotelBudget, days, null);
            }
            if (!clusters.Any())
                return (0, "Không thể tạo cụm địa điểm", new List<Hotel>());

            var selectedHotels = new List<Hotel>();
            var details = new List<string>();
            decimal totalCost = 0;
            decimal budgetPerNight = hotelBudget / nights;

            foreach (var cluster in clusters)
            {
                int nightsForCluster = cluster.RecommendedNights;
                if (cluster == clusters.Last())
                {
                    int assignedNights = details
                        .Select(line =>
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)\s+đêm");
                            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
                        })
                        .Sum();
                    nightsForCluster = nights - assignedNights;
                    if (nightsForCluster < 0) nightsForCluster = 0;
                }

                if (nightsForCluster <= 0) continue;

                decimal budgetForCluster = budgetPerNight * nightsForCluster;
                decimal maxPricePerNight = budgetForCluster / nightsForCluster;

                var hotel = FindBestHotelForClusterByBudgetPlan(cluster.Places, maxPricePerNight, planType);

                if (hotel != null && !selectedHotels.Contains(hotel))
                {
                    selectedHotels.Add(hotel);
                    decimal clusterCost = hotel.PricePerNight * nightsForCluster;
                    totalCost += clusterCost;

                    string locationNames = GetLocationNamesDisplay(cluster.Places);
                    details.Add($"• {hotel.Name} ({locationNames}): " +
                               $"{nightsForCluster} đêm × {FormatCurrency(hotel.PricePerNight)}");
                }
            }

            if (!selectedHotels.Any())
            {
                var defaultHotel = _context.Hotels
                    .AsNoTracking()
                    .OrderBy(h => planType == BudgetPlanType.Premium ? -h.PricePerNight : h.PricePerNight)
                    .FirstOrDefault();
                if (defaultHotel != null)
                {
                    selectedHotels.Add(defaultHotel);
                    decimal defaultCost = Math.Min(defaultHotel.PricePerNight * nights, hotelBudget);
                    totalCost = defaultCost;
                    details.Add($"• {defaultHotel.Name} (Mặc định): {nights} đêm × {FormatCurrency(defaultCost / nights)}");
                }
            }

            return (totalCost, string.Join("<br/>", details), selectedHotels);
        }







        // ============================================================================
        // ✅ MỚI: Geocode functions (áp dụng từ CarpoolController)
        // ============================================================================
        /// <summary>
        /// Kiểm tra tọa độ GPS có hợp lệ không
        /// </summary>
        private bool HasValidCoordinates(double lat, double lng)
        {
            return Math.Abs(lat) > double.Epsilon && Math.Abs(lng) > double.Epsilon;
        }

        /// <summary>
        /// Geocode địa chỉ thành tọa độ GPS sử dụng Nominatim (OpenStreetMap)
        /// </summary>
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

        private double? ParseCoordinate(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var invariant))
                return invariant;

            if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, new CultureInfo("vi-VN"), out var vietnamese))
                return vietnamese;

            return null;
        }

        // Updated FormatSuggestion to include local transport
        private List<string> RemoveDuplicateSuggestions(List<string> suggestions)
        {
            var uniqueSuggestions = new List<string>();
            var seenTransports = new HashSet<string>();

            foreach (var suggestion in suggestions)
            {
                var match = Regex.Match(suggestion, @"🚗 <strong>([^<]+)</strong>");
                if (match.Success)
                {
                    string transportName = match.Groups[1].Value;
                    if (!seenTransports.Contains(transportName))
                    {
                        seenTransports.Add(transportName);
                        uniqueSuggestions.Add(suggestion);
                    }
                }
            }

            return uniqueSuggestions;
        }

        // ✅ MỚI: Loại bỏ duplicate dựa trên mức ngân sách
        private List<string> RemoveDuplicateSuggestionsByBudgetPlan(List<string> suggestions)
        {
            var uniqueSuggestions = new List<string>();
            var seenBudgetPlans = new HashSet<string>();

            foreach (var suggestion in suggestions)
            {
                // Tìm tên gói ngân sách (🔥 TIẾT KIỆM, 🎯 CÂN BẰNG, 🌟 CAO CẤP)
                var match = Regex.Match(suggestion, @"(🔥|🎯|🌟)\s*(TIẾT KIỆM|CÂN BẰNG|CAO CẤP)");
                if (match.Success)
                {
                    string planName = match.Groups[2].Value; // Lấy tên gói (TIẾT KIỆM, CÂN BẰNG, CAO CẤP)
                    if (!seenBudgetPlans.Contains(planName))
                    {
                        seenBudgetPlans.Add(planName);
                        uniqueSuggestions.Add(suggestion);
                    }
                }
                else
                {
                    // Nếu không tìm thấy tên gói (có thể là lỗi), vẫn thêm vào
                    uniqueSuggestions.Add(suggestion);
                }
            }

            return uniqueSuggestions;
        }

        private int CalculateRecommendedDays(int numberOfPlaces) =>
            numberOfPlaces switch
            {
                <= 0 => 1,
                <= 2 => 1,
                <= 4 => 2,
                <= 6 => 3,
                <= 8 => 4,
                _ => 5
            };

        private decimal ExtractTotalCost(string suggestion)
        {
            var match = Regex.Match(suggestion, @"Tổng chi phí:\s([\d,.]+)đ");
            if (match.Success)
            {
                var value = match.Groups[1].Value.Replace(".", "").Replace(",", "");
                return decimal.TryParse(value, out var cost) ? cost : decimal.MaxValue;
            }
            return decimal.MaxValue;
        }

        private (decimal TotalCost, string Details) CalculateOptimizedFoodCosts(
    List<string> selectedPlaceIds,
    decimal foodBudget,
    int days)
        {
            if (days <= 0 || foodBudget <= 0)
                return (0, "Không có ngân sách ăn uống");

            var places = _context.TouristPlaces
                .AsNoTracking()
                .Where(p => selectedPlaceIds.Contains(p.Id))
                .ToList();

            if (!places.Any())
                return (foodBudget, $"Ăn uống tổng quát: {days} ngày × {FormatCurrency(foodBudget / days)}/ngày");

            // ✅ PHƯƠNG ÁN MỚI: PHÂN BỔ THEO BỮA (SÁNG - TRƯA - TỐI)
            // 1. Tạo lịch trình tối ưu
            double centerLat = places.Average(p => p.Latitude);
            double centerLng = places.Average(p => p.Longitude);
            // ✅ Sử dụng ItineraryService
            var optimizedRoute = _itineraryService.OptimizeRoute(centerLat, centerLng, places);

            // 2. Phân bổ địa điểm theo từng ngày
            var dailyPlaces = _itineraryService.DistributePlacesAcrossDays(optimizedRoute, days);

            var details = new List<string>();
            decimal totalCost = 0;
            decimal dailyBudget = foodBudget / days;

            // ✅ PHÂN BỔ NGÂN SÁCH THEO BỮA: Sáng 20%, Trưa 40%, Tối 40%
            decimal breakfastBudget = dailyBudget * 0.20m;  // 20%
            decimal lunchBudget = dailyBudget * 0.40m;      // 40%
            decimal dinnerBudget = dailyBudget * 0.40m;     // 40%

            // 3. Theo dõi nhà hàng đã dùng để đảm bảo đa dạng
            var usedRestaurantIds = new HashSet<int>();
            var lastDayRestaurantIds = new Dictionary<int, int>(); // Lưu quán đã dùng theo từng bữa

            // 4. Mỗi ngày chọn 3 quán: sáng, trưa, tối
            for (int day = 0; day < days; day++)
            {
                var dayPlaces = dailyPlaces.ElementAtOrDefault(day) ?? new List<TouristPlace>();
                int displayDay = day + 1;
                decimal dayCost = 0;

                if (!dayPlaces.Any())
                {
                    // Ngày nghỉ: dùng ngân sách trung bình (chia đều 3 bữa)
                    dayCost = dailyBudget;
                    totalCost += dayCost;
                    details.Add($"• Ngày {displayDay}: {FormatCurrency(dayCost)} (nghỉ ngơi/tự do)");
                    continue;
                }

                // Tính trung tâm địa điểm trong ngày
                double dayCenterLat = dayPlaces.Average(p => p.Latitude);
                double dayCenterLng = dayPlaces.Average(p => p.Longitude);
                var placeIds = dayPlaces.Select(p => p.Id).ToList();

                // Chọn quán cho từng bữa
                var mealDetails = new List<string>();
                decimal dayTotalCost = 0;

                // ✅ BỮA SÁNG (20% ngân sách)
                var breakfastRestaurant = SelectRestaurantForMeal(
                    placeIds, breakfastBudget, dayCenterLat, dayCenterLng,
                    usedRestaurantIds, lastDayRestaurantIds.GetValueOrDefault(0), "sáng");

                decimal breakfastCost = breakfastRestaurant.Item2;
                // ✅ XỬ LÝ: Nếu quán sáng vượt 20%, chọn quán rẻ hơn hoặc điều chỉnh
                if (breakfastCost > breakfastBudget * 1.1m) // Cho phép vượt 10%
                {
                    // Tìm quán rẻ hơn
                    var cheaperBreakfast = SelectCheaperRestaurant(placeIds, breakfastBudget, usedRestaurantIds);
                    if (cheaperBreakfast != null)
                    {
                        breakfastRestaurant = (cheaperBreakfast, Math.Min(cheaperBreakfast.AveragePricePerPerson * 1.2m, breakfastBudget));
                        breakfastCost = breakfastRestaurant.Item2;
                    }
                    else
                    {
                        // Nếu không có quán rẻ hơn, giới hạn chi phí = 20% + 10% buffer
                        breakfastCost = Math.Min(breakfastCost, breakfastBudget * 1.1m);
                    }
                }

                if (breakfastRestaurant.Item1 != null)
                {
                    usedRestaurantIds.Add(breakfastRestaurant.Item1.Id);
                    lastDayRestaurantIds[0] = breakfastRestaurant.Item1.Id;
                    mealDetails.Add($"Sáng: {breakfastRestaurant.Item1.Name} ({FormatCurrency(breakfastCost)})");
                }
                else
                {
                    mealDetails.Add($"Sáng: {FormatCurrency(breakfastCost)}");
                }
                dayTotalCost += breakfastCost;

                // ✅ BỮA TRƯA (40% ngân sách)
                var lunchRestaurant = SelectRestaurantForMeal(
                    placeIds, lunchBudget, dayCenterLat, dayCenterLng,
                    usedRestaurantIds, lastDayRestaurantIds.GetValueOrDefault(1), "trưa");

                decimal lunchCost = lunchRestaurant.Item2;
                if (lunchRestaurant.Item1 != null)
                {
                    usedRestaurantIds.Add(lunchRestaurant.Item1.Id);
                    lastDayRestaurantIds[1] = lunchRestaurant.Item1.Id;
                    mealDetails.Add($"Trưa: {lunchRestaurant.Item1.Name} ({FormatCurrency(lunchCost)})");
                }
                else
                {
                    mealDetails.Add($"Trưa: {FormatCurrency(lunchCost)}");
                }
                dayTotalCost += lunchCost;

                // ✅ BỮA TỐI (40% ngân sách)
                var dinnerRestaurant = SelectRestaurantForMeal(
                    placeIds, dinnerBudget, dayCenterLat, dayCenterLng,
                    usedRestaurantIds, lastDayRestaurantIds.GetValueOrDefault(2), "tối");

                decimal dinnerCost = dinnerRestaurant.Item2;
                if (dinnerRestaurant.Item1 != null)
                {
                    usedRestaurantIds.Add(dinnerRestaurant.Item1.Id);
                    lastDayRestaurantIds[2] = dinnerRestaurant.Item1.Id;
                    mealDetails.Add($"Tối: {dinnerRestaurant.Item1.Name} ({FormatCurrency(dinnerCost)})");
                }
                else
                {
                    mealDetails.Add($"Tối: {FormatCurrency(dinnerCost)}");
                }
                dayTotalCost += dinnerCost;

                totalCost += dayTotalCost;

                // Tạo mô tả cho ngày
                string locationNames = GetLocationNamesDisplay(dayPlaces);
                details.Add($"• Ngày {displayDay} ({locationNames}): {string.Join(" | ", mealDetails)}");
            }

            // ✅ ĐẢM BẢO TỔNG CHI PHÍ KHÔNG VƯỢT QUÁ NGÂN SÁCH
            if (totalCost > foodBudget)
            {
                // Điều chỉnh tỷ lệ để tổng chi phí = ngân sách
                decimal adjustmentRatio = foodBudget / totalCost;
                totalCost = foodBudget;

                // Cập nhật lại details với chi phí đã điều chỉnh (giữ nguyên tỷ lệ 20-40-40)
                details.Clear();
                usedRestaurantIds.Clear();
                lastDayRestaurantIds.Clear();

                decimal adjustedDailyBudget = dailyBudget * adjustmentRatio;
                decimal adjustedBreakfastBudget = adjustedDailyBudget * 0.20m;
                decimal adjustedLunchBudget = adjustedDailyBudget * 0.40m;
                decimal adjustedDinnerBudget = adjustedDailyBudget * 0.40m;

                for (int day = 0; day < days; day++)
                {
                    var dayPlaces = dailyPlaces.ElementAtOrDefault(day) ?? new List<TouristPlace>();
                    int displayDay = day + 1;

                    if (!dayPlaces.Any())
                    {
                        details.Add($"• Ngày {displayDay}: {FormatCurrency(adjustedDailyBudget)} (nghỉ ngơi/tự do)");
                        continue;
                    }

                    var placeIds = dayPlaces.Select(p => p.Id).ToList();
                    double dayCenterLat = dayPlaces.Average(p => p.Latitude);
                    double dayCenterLng = dayPlaces.Average(p => p.Longitude);

                    var mealDetails = new List<string>();

                    // Bữa sáng
                    var breakfastRestaurant = SelectRestaurantForMeal(
                        placeIds, adjustedBreakfastBudget, dayCenterLat, dayCenterLng,
                        usedRestaurantIds, lastDayRestaurantIds.GetValueOrDefault(0), "sáng");
                    decimal breakfastCost = Math.Min(breakfastRestaurant.Item2, adjustedBreakfastBudget * 1.1m);
                    if (breakfastRestaurant.Item1 != null)
                    {
                        usedRestaurantIds.Add(breakfastRestaurant.Item1.Id);
                        lastDayRestaurantIds[0] = breakfastRestaurant.Item1.Id;
                        mealDetails.Add($"Sáng: {breakfastRestaurant.Item1.Name} ({FormatCurrency(breakfastCost)})");
                    }
                    else
                    {
                        mealDetails.Add($"Sáng: {FormatCurrency(breakfastCost)}");
                    }

                    // Bữa trưa
                    var lunchRestaurant = SelectRestaurantForMeal(
                        placeIds, adjustedLunchBudget, dayCenterLat, dayCenterLng,
                        usedRestaurantIds, lastDayRestaurantIds.GetValueOrDefault(1), "trưa");
                    decimal lunchCost = Math.Min(lunchRestaurant.Item2, adjustedLunchBudget);
                    if (lunchRestaurant.Item1 != null)
                    {
                        usedRestaurantIds.Add(lunchRestaurant.Item1.Id);
                        lastDayRestaurantIds[1] = lunchRestaurant.Item1.Id;
                        mealDetails.Add($"Trưa: {lunchRestaurant.Item1.Name} ({FormatCurrency(lunchCost)})");
                    }
                    else
                    {
                        mealDetails.Add($"Trưa: {FormatCurrency(lunchCost)}");
                    }

                    // Bữa tối
                    var dinnerRestaurant = SelectRestaurantForMeal(
                        placeIds, adjustedDinnerBudget, dayCenterLat, dayCenterLng,
                        usedRestaurantIds, lastDayRestaurantIds.GetValueOrDefault(2), "tối");
                    decimal dinnerCost = Math.Min(dinnerRestaurant.Item2, adjustedDinnerBudget);
                    if (dinnerRestaurant.Item1 != null)
                    {
                        usedRestaurantIds.Add(dinnerRestaurant.Item1.Id);
                        lastDayRestaurantIds[2] = dinnerRestaurant.Item1.Id;
                        mealDetails.Add($"Tối: {dinnerRestaurant.Item1.Name} ({FormatCurrency(dinnerCost)})");
                    }
                    else
                    {
                        mealDetails.Add($"Tối: {FormatCurrency(dinnerCost)}");
                    }

                    string locationNames = GetLocationNamesDisplay(dayPlaces);
                    details.Add($"• Ngày {displayDay} ({locationNames}): {string.Join(" | ", mealDetails)}");
                }
            }

            return (totalCost, string.Join("<br/>", details));
        }

        // ✅ MỚI: Tính chi phí ăn uống theo mức ngân sách
        private (decimal TotalCost, string Details) CalculateOptimizedFoodCostsByBudgetPlan(
            List<string> selectedPlaceIds,
            decimal foodBudget,
            int days,
            BudgetPlanType planType)
        {
            // Điều chỉnh ngân sách theo mức ngân sách
            decimal adjustedBudget = planType switch
            {
                BudgetPlanType.Budget => foodBudget * 0.9m,      // Giảm 10% cho tiết kiệm
                BudgetPlanType.Balanced => foodBudget,           // Giữ nguyên
                BudgetPlanType.Premium => foodBudget * 1.15m,    // Tăng 15% cho cao cấp
                _ => foodBudget
            };

            // Sử dụng hàm gốc nhưng với ngân sách đã điều chỉnh
            // Logic chọn nhà hàng sẽ được điều chỉnh thông qua targetMealPrice
            var result = CalculateOptimizedFoodCosts(selectedPlaceIds, adjustedBudget, days);

            // Thêm thông tin về mức ngân sách vào details
            string planInfo = planType switch
            {
                BudgetPlanType.Budget => " (Nhà hàng bình dân)",
                BudgetPlanType.Balanced => " (Nhà hàng tầm trung)",
                BudgetPlanType.Premium => " (Nhà hàng sang trọng)",
                _ => ""
            };

            if (!string.IsNullOrEmpty(planInfo) && !result.Details.Contains(planInfo))
            {
                result = (result.TotalCost, result.Details.Replace("Ăn uống", $"Ăn uống{planInfo}"));
            }

            return result;
        }

        // ✅ BƯỚC 4: SỬA CalculateOptimizedFoodCosts - Dùng DailyItinerary (Overload mới)
        /// <summary>
        /// Tính chi phí ăn uống dựa trên DailyItinerary (đồng bộ với lịch trình)
        /// </summary>
        private (decimal TotalCost, string Details) CalculateOptimizedFoodCosts(
            List<DailyItinerary> dailyItinerary,
            decimal foodBudget)
        {
            int days = dailyItinerary?.Count ?? 0;
            if (days <= 0 || foodBudget <= 0)
                return (0, "Không có ngân sách ăn uống");

            var details = new List<string>();
            decimal totalCost = 0;
            decimal dailyBudget = foodBudget / days;

            decimal breakfastBudget = dailyBudget * 0.20m;
            decimal lunchBudget = dailyBudget * 0.40m;
            decimal dinnerBudget = dailyBudget * 0.40m;

            var usedRestaurantIds = new HashSet<int>();
            var lastDayRestaurantIds = new Dictionary<int, int>();

            foreach (var dayPlan in dailyItinerary)
            {
                var dayPlaces = dayPlan.Places;
                int displayDay = dayPlan.DayNumber;

                if (!dayPlaces.Any())
                {
                    totalCost += dailyBudget;
                    details.Add($"• Ngày {displayDay}: {FormatCurrency(dailyBudget)} (nghỉ ngơi)");
                    continue;
                }

                double dayCenterLat = dayPlaces.Average(p => p.Latitude);
                double dayCenterLng = dayPlaces.Average(p => p.Longitude);
                var placeIds = dayPlaces.Select(p => p.Id).ToList();

                var mealDetails = new List<string>();
                decimal dayTotalCost = 0;

                // Chọn quán cho 3 bữa (giữ nguyên logic cũ)
                var breakfastRestaurant = SelectRestaurantForMeal(
                    placeIds, breakfastBudget, dayCenterLat, dayCenterLng,
                    usedRestaurantIds, lastDayRestaurantIds.GetValueOrDefault(0), "sáng");
                decimal breakfastCost = breakfastRestaurant.Item2;
                if (breakfastCost > breakfastBudget * 1.1m)
                {
                    var cheaperBreakfast = SelectCheaperRestaurant(placeIds, breakfastBudget, usedRestaurantIds);
                    if (cheaperBreakfast != null)
                    {
                        breakfastRestaurant = (cheaperBreakfast, Math.Min(cheaperBreakfast.AveragePricePerPerson * 1.2m, breakfastBudget));
                        breakfastCost = breakfastRestaurant.Item2;
                    }
                    else
                    {
                        breakfastCost = Math.Min(breakfastCost, breakfastBudget * 1.1m);
                    }
                }
                if (breakfastRestaurant.Item1 != null)
                {
                    usedRestaurantIds.Add(breakfastRestaurant.Item1.Id);
                    lastDayRestaurantIds[0] = breakfastRestaurant.Item1.Id;
                    mealDetails.Add($"Sáng: {breakfastRestaurant.Item1.Name} ({FormatCurrency(breakfastCost)})");
                }
                else
                {
                    mealDetails.Add($"Sáng: {FormatCurrency(breakfastCost)}");
                }
                dayTotalCost += breakfastCost;

                var lunchRestaurant = SelectRestaurantForMeal(
                    placeIds, lunchBudget, dayCenterLat, dayCenterLng,
                    usedRestaurantIds, lastDayRestaurantIds.GetValueOrDefault(1), "trưa");
                decimal lunchCost = lunchRestaurant.Item2;
                if (lunchRestaurant.Item1 != null)
                {
                    usedRestaurantIds.Add(lunchRestaurant.Item1.Id);
                    lastDayRestaurantIds[1] = lunchRestaurant.Item1.Id;
                    mealDetails.Add($"Trưa: {lunchRestaurant.Item1.Name} ({FormatCurrency(lunchCost)})");
                }
                else
                {
                    mealDetails.Add($"Trưa: {FormatCurrency(lunchCost)}");
                }
                dayTotalCost += lunchCost;

                var dinnerRestaurant = SelectRestaurantForMeal(
                    placeIds, dinnerBudget, dayCenterLat, dayCenterLng,
                    usedRestaurantIds, lastDayRestaurantIds.GetValueOrDefault(2), "tối");
                decimal dinnerCost = dinnerRestaurant.Item2;
                if (dinnerRestaurant.Item1 != null)
                {
                    usedRestaurantIds.Add(dinnerRestaurant.Item1.Id);
                    lastDayRestaurantIds[2] = dinnerRestaurant.Item1.Id;
                    mealDetails.Add($"Tối: {dinnerRestaurant.Item1.Name} ({FormatCurrency(dinnerCost)})");
                }
                else
                {
                    mealDetails.Add($"Tối: {FormatCurrency(dinnerCost)}");
                }
                dayTotalCost += dinnerCost;

                totalCost += dayTotalCost;

                string locationNames = GetLocationNamesDisplay(dayPlaces);
                details.Add($"• Ngày {displayDay} ({locationNames}): {string.Join(" | ", mealDetails)}");
            }

            return (totalCost, string.Join("<br/>", details));
        }

        // ✅ HÀM HELPER: Chọn quán cho từng bữa
        private (Restaurant, decimal) SelectRestaurantForMeal(
            List<string> placeIds,
            decimal mealBudget,
            double centerLat,
            double centerLng,
            HashSet<int> usedRestaurantIds,
            int lastRestaurantId,
            string mealType)
        {
            // Tính giá mục tiêu cho bữa (1 người, không phải 2.5 bữa)
            decimal targetPrice = mealBudget;

            var candidateRestaurants = GetRestaurantsByLocation(placeIds, mealBudget);

            // Lọc nhà hàng: không dùng lại nhà hàng đã dùng
            var availableRestaurants = candidateRestaurants
                .Where(r => r.Id != lastRestaurantId && !usedRestaurantIds.Contains(r.Id))
                .ToList();

            if (!availableRestaurants.Any())
            {
                availableRestaurants = candidateRestaurants
                    .Where(r => r.Id != lastRestaurantId)
                    .ToList();
            }

            if (!availableRestaurants.Any())
            {
                availableRestaurants = candidateRestaurants;
            }

            if (availableRestaurants.Any())
            {
                // Chọn quán gần nhất và phù hợp ngân sách
                var selected = availableRestaurants
                    .Where(r => r.AveragePricePerPerson <= targetPrice * 1.2m) // Cho phép vượt 20%
                    .OrderBy(r =>
                    {
                        // Tính khoảng cách từ tọa độ của nhà hàng
                        // Nhà hàng LUÔN có tọa độ (bắt buộc)
                        return _recommendationService.GetDistance(centerLat, centerLng, r.Latitude, r.Longitude);
                    })
                    .ThenBy(r => Math.Abs(r.AveragePricePerPerson - targetPrice)) // Gần giá mục tiêu
                    .FirstOrDefault();

                if (selected != null)
                {
                    // Chi phí = giá trung bình/người (1 bữa)
                    decimal mealCost = Math.Min(selected.AveragePricePerPerson, targetPrice * 1.1m);
                    return (selected, mealCost);
                }
            }

            // Không tìm được quán, trả về ngân sách mặc định
            return (null, targetPrice);
        }

        // ✅ HÀM HELPER: Chọn quán rẻ hơn (cho bữa sáng)
        private Restaurant SelectCheaperRestaurant(
            List<string> placeIds,
            decimal maxBudget,
            HashSet<int> usedRestaurantIds)
        {
            var candidateRestaurants = GetRestaurantsByLocation(placeIds, maxBudget);

            var cheaperRestaurants = candidateRestaurants
                .Where(r => !usedRestaurantIds.Contains(r.Id))
                .Where(r => r.AveragePricePerPerson <= maxBudget * 0.8m) // Rẻ hơn 20%
                .OrderBy(r => r.AveragePricePerPerson) // Rẻ nhất trước
                .ToList();

            return cheaperRestaurants.FirstOrDefault();
        }




        private (decimal TotalCost, List<string> TicketDetails, List<string> Warnings) CalculateTicketCosts(
    List<string> selectedPlaceIds,
    TripPlannerViewModel model)
        {
            var details = new List<string>();
            var warnings = new List<string>();
            decimal cost = 0;

            if (!selectedPlaceIds.Any())
                return (cost, details, warnings);

            // Tối ưu: Gộp truy vấn để tránh N+1 problem
            // Load tất cả places và attractions trong một lần
            var places = _context.TouristPlaces
                .AsNoTracking()
                .Where(p => selectedPlaceIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToList();

            var attractionsByPlace = _context.Attractions
                .AsNoTracking()
                .Where(a => selectedPlaceIds.Contains(a.TouristPlaceId))
                .GroupBy(a => a.TouristPlaceId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var place in places)
            {
                if (!attractionsByPlace.TryGetValue(place.Id, out var attractions) || !attractions.Any())
                {
                    warnings.Add($"Không có thông tin vé cho địa điểm {place.Name}");
                    continue;
                }

                decimal sum = attractions.Sum(a => a.TicketPrice);
                cost += sum;
                details.Add($"• {place.Name}: Vé tham quan {FormatCurrency(sum)}");
            }

            if (cost == 0 && places.Any())
            {
                warnings.Add("Không tìm thấy thông tin vé tham quan cho các địa điểm đã chọn.");
            }

            return (cost, details, warnings);
        }

        private List<string> GenerateBudgetWarning(decimal budget, int days, int placeCount)
        {
            return new List<string> {
                $"⚠️ <strong>Ngân sách không đủ</strong><br/>" +
                $"Ngân sách hiện tại: {FormatCurrency(budget)} cho {days} ngày và {placeCount} địa điểm.<br/>" +
                $"💡 <strong>Gợi ý:</strong><br/>• Giảm số ngày<br/>• Hoặc tăng ngân sách<br/>• Hoặc giảm địa điểm"
            };
        }

        private bool ValidateSelectedPlaces(List<string> places) => places != null && places.Any();

        /// <summary>
        /// Tính chi phí vận chuyển từ GPS sử dụng TransportPriceCalculator (ưu tiên)
        /// </summary>


        private decimal CalculateTransportCost(TransportOption transport, LegacyLocation legacy, double km, List<string> placeIds, int days, bool isOtherCity)
        {
            // ✅ FIX: Xe máy và ô tô cá nhân (IsSelfDrive = True) không lấy giá từ database
            // mà tính theo FuelConsumption và FuelPrice
            if (transport.IsSelfDrive)
            {
                // Nếu xuất phát tại Đà Lạt (không phải tỉnh khác), không tính chi phí vận chuyển chính
                // cho phương tiện cá nhân vì chi phí di chuyển nội thành đã được tính riêng ở phần local transport.
                if (!isOtherCity)
                {
                    return 0;
                }

                // Xuất phát từ tỉnh khác: tính theo khoảng cách thực tế
                // Sử dụng khoảng cách từ GPS hoặc ước lượng tối thiểu
                double actualDistance = km > 100 ? km : 300;

                // Ưu tiên 1: Tính theo định mức nhiên liệu (Chính xác nhất)
                if (transport.FuelConsumption > 0 && transport.FuelPrice > 0)
                {
                    decimal fuelUsed = (decimal)actualDistance * transport.FuelConsumption / 100m;
                    decimal fuelCost = fuelUsed * transport.FuelPrice;

                    // Chi phí bảo dưỡng và hao mòn (20% chi phí nhiên liệu)
                    decimal maintenanceCost = fuelCost * 0.2m;

                    return fuelCost + maintenanceCost;
                }
                else
                {
                    // Ưu tiên 2 (Fallback): Nếu thiếu dữ liệu xăng, dùng đơn giá thấp
                    // Xe máy: ~600đ/km | Ô tô: ~1,800đ/km
                    decimal fallbackRate = transport.Name?.ToLower().Contains("máy") == true ? 600m : 1800m;
                    return (decimal)actualDistance * fallbackRate;
                }
            }

            // Phương tiện công cộng: Ưu tiên lấy giá từ database
            decimal basePrice = 0;
            if (legacy != null)
            {
                // Tối ưu: Sử dụng AsNoTracking() cho read-only query
                var priceHistory = _context.TransportPriceHistories
                    .AsNoTracking()
                    .FirstOrDefault(p => p.LegacyLocationId == legacy.Id && p.TransportOptionId == transport.Id);
                if (priceHistory != null)
                {
                    basePrice = priceHistory.Price;
                }
            }

            // Fallback: Lấy giá cố định hoặc giá mặc định
            if (basePrice == 0)
            {
                basePrice = transport.FixedPrice > 0 ? transport.FixedPrice : transport.Price;
            }

            // ✅ QUAN TRỌNG: Giá vé xe công cộng phải tính khứ hồi (x2)
            // Vì người dùng cần đi và về
            return basePrice * 2;
        }

        private string FormatCurrency(decimal value) => $"{value:N0}đ";

        private List<Hotel> GetHotelsByBudgetSegment(List<string> placeIds, decimal budgetPerDay)
        {
            decimal maxPricePerNight = budgetPerDay * 0.4m;

            var hotelsQuery = _context.Hotels
                .AsNoTracking()
                .AsQueryable();

            if (placeIds != null && placeIds.Any())
            {
                hotelsQuery = hotelsQuery.Where(h => placeIds.Contains(h.TouristPlaceId) || h.TouristPlaceId == null);
            }

            var inBudget = hotelsQuery
                .Where(h => h.PricePerNight <= maxPricePerNight)
                .OrderBy(h => Math.Abs(h.PricePerNight - maxPricePerNight))
                .ToList();

            if (inBudget.Any())
            {
                return inBudget;
            }
            else
            {
                return hotelsQuery
                    .OrderBy(h => h.PricePerNight)
                    .ToList();
            }
        }






        private Hotel FindBestHotelForCluster(List<TouristPlace> clusterPlaces, decimal budgetPerNight)
        {
            if (!clusterPlaces.Any()) return null;

            double centerLat = clusterPlaces.Average(p => p.Latitude);
            double centerLng = clusterPlaces.Average(p => p.Longitude);

            var placeIds = clusterPlaces.Select(p => p.Id).ToList();
            var candidateHotels = GetHotelsByBudgetSegment(placeIds, budgetPerNight * 2.5m);

            if (!candidateHotels.Any())
            {
                candidateHotels = _context.Hotels
                    .AsNoTracking()
                    .ToList();
            }

            return candidateHotels
                .Where(h => h.PricePerNight <= budgetPerNight * 1.2m)
                .OrderBy(h => _recommendationService.GetDistance(h.Latitude, h.Longitude, centerLat, centerLng))
                .FirstOrDefault()
                ?? candidateHotels.OrderBy(h => h.PricePerNight).FirstOrDefault();
        }

        // ✅ MỚI: Chọn khách sạn theo mức ngân sách
        private Hotel FindBestHotelForClusterByBudgetPlan(
            List<TouristPlace> clusterPlaces,
            decimal budgetPerNight,
            BudgetPlanType planType)
        {
            if (!clusterPlaces.Any()) return null;

            double centerLat = clusterPlaces.Average(p => p.Latitude);
            double centerLng = clusterPlaces.Average(p => p.Longitude);
            var placeIds = clusterPlaces.Select(p => p.Id).ToList();

            var allHotels = _context.Hotels
                .AsNoTracking()
                .Where(h => placeIds.Contains(h.TouristPlaceId) || h.TouristPlaceId == null)
                .ToList();

            if (!allHotels.Any())
            {
                allHotels = _context.Hotels
                    .AsNoTracking()
                    .ToList();
            }

            switch (planType)
            {
                case BudgetPlanType.Budget:
                    // 🔥 TIẾT KIỆM: Chọn khách sạn giá rẻ nhất, gần nhất
                    return allHotels
                        .Where(h => h.PricePerNight <= budgetPerNight * 0.8m) // Cho phép rẻ hơn 20%
                        .OrderBy(h => h.PricePerNight) // Ưu tiên giá rẻ
                        .ThenBy(h => _recommendationService.GetDistance(h.Latitude, h.Longitude, centerLat, centerLng))
                        .FirstOrDefault()
                        ?? allHotels.OrderBy(h => h.PricePerNight).FirstOrDefault();

                case BudgetPlanType.Balanced:
                    // 🎯 CÂN BẰNG: Khách sạn tầm trung, gần nhất
                    return allHotels
                        .Where(h => h.PricePerNight <= budgetPerNight * 1.2m)
                        .OrderBy(h => _recommendationService.GetDistance(h.Latitude, h.Longitude, centerLat, centerLng))
                        .ThenBy(h => Math.Abs(h.PricePerNight - budgetPerNight * 0.8m)) // Gần với ngân sách
                        .FirstOrDefault()
                        ?? allHotels.OrderBy(h => h.PricePerNight).FirstOrDefault();

                case BudgetPlanType.Premium:
                    // 🌟 CAO CẤP: Resort/KS 3-5 sao, có thể vượt ngân sách một chút
                    return allHotels
                        .Where(h => h.PricePerNight <= budgetPerNight * 1.5m) // Cho phép vượt 50%
                        .OrderByDescending(h => h.PricePerNight) // Ưu tiên đắt hơn (cao cấp hơn)
                        .ThenBy(h => _recommendationService.GetDistance(h.Latitude, h.Longitude, centerLat, centerLng))
                        .FirstOrDefault()
                        ?? allHotels.OrderByDescending(h => h.PricePerNight).FirstOrDefault();

                default:
                    return FindBestHotelForCluster(clusterPlaces, budgetPerNight);
            }
        }


        private List<Restaurant> GetRestaurantsByLocation(List<string> placeIds, decimal dailyBudget)
        {
            decimal targetMealPrice = (dailyBudget * 0.6m) / 2.5m;

            // Lấy cả nhà hàng không liên kết với địa điểm cụ thể
            // Restaurants giờ có tọa độ trực tiếp
            var restaurants = _context.Restaurants
                .AsNoTracking()
                .Where(r => placeIds.Contains(r.TouristPlaceId) || r.TouristPlaceId == null)
                .OrderBy(r => Math.Abs(r.AveragePricePerPerson - targetMealPrice))
                .ToList();

            // Nếu vẫn không có, lấy tất cả nhà hàng
            if (!restaurants.Any())
            {
                restaurants = _context.Restaurants
                    .AsNoTracking()
                    .OrderBy(r => Math.Abs(r.AveragePricePerPerson - targetMealPrice))
                    .ToList();
            }

            return restaurants;
        }

        // ✅ MỚI: Chọn nhà hàng theo mức ngân sách
        private List<Restaurant> GetRestaurantsByBudgetPlan(
            List<string> placeIds,
            decimal dailyBudget,
            BudgetPlanType planType)
        {
            // Restaurants giờ có tọa độ trực tiếp, không cần Include Location
            var allRestaurants = _context.Restaurants
                .AsNoTracking()
                .Where(r => placeIds.Contains(r.TouristPlaceId) || r.TouristPlaceId == null)
                .ToList();

            if (!allRestaurants.Any())
            {
                allRestaurants = _context.Restaurants
                    .AsNoTracking()
                    .ToList();
            }

            switch (planType)
            {
                case BudgetPlanType.Budget:
                    // 🔥 TIẾT KIỆM: Nhà hàng bình dân (30-50k/người)
                    decimal budgetTarget = 40000; // 40k/người
                    return allRestaurants
                        .Where(r => r.AveragePricePerPerson <= 60000) // Tối đa 60k
                        .OrderBy(r => r.AveragePricePerPerson) // Ưu tiên rẻ nhất
                        .ThenBy(r => Math.Abs(r.AveragePricePerPerson - budgetTarget))
                        .ToList();

                case BudgetPlanType.Balanced:
                    // 🎯 CÂN BẰNG: Nhà hàng 70-100k/người
                    decimal balancedTarget = 85000; // 85k/người (trung bình)
                    return allRestaurants
                        .Where(r => r.AveragePricePerPerson >= 50000 && r.AveragePricePerPerson <= 120000)
                        .OrderBy(r => Math.Abs(r.AveragePricePerPerson - balancedTarget))
                        .ToList();

                case BudgetPlanType.Premium:
                    // 🌟 CAO CẤP: Nhà hàng sang trọng (150k+/người)
                    return allRestaurants
                        .Where(r => r.AveragePricePerPerson >= 100000)
                        .OrderByDescending(r => r.AveragePricePerPerson) // Ưu tiên đắt hơn (sang trọng hơn)
                        .ThenBy(r => r.Name) // Sắp xếp theo tên để nhất quán
                        .ToList();

                default:
                    return GetRestaurantsByLocation(placeIds, dailyBudget);
            }
        }
        /// <summary>
        /// ✅ REFACTORED: Tạo SuggestionViewModel + HTML, đồng thời (nếu có TripPlannerViewModel)
        /// sẽ push SuggestionViewModel vào TripPlannerViewModel.SuggestionViewModels.
        /// </summary>
        private string FormatOptimizedSuggestion(
     TransportOption transport,
     decimal transportCost,
     string hotelDetails,
     decimal hotelCost,
     string foodDetails,
     decimal foodCost,
     decimal ticketCost,
     decimal localTransportCost,
     decimal miscCost,
     decimal totalCost,
     decimal remaining,
     int days,
     List<string> ticketDetails,
     List<string> localTransportDetails,
     List<string> warnings,
     string routeDetails,
     string clusterDetails,
     bool basicOnly,
     List<DailyItinerary> dailyItinerary = null)
        {
            return FormatOptimizedSuggestionInternal(
                null,
                transport,
                transportCost,
                hotelDetails,
                hotelCost,
                foodDetails,
                foodCost,
                ticketCost,
                localTransportCost,
                miscCost,
                totalCost,
                remaining,
                days,
                ticketDetails,
                localTransportDetails,
                warnings,
                routeDetails,
                clusterDetails,
                basicOnly,
                dailyItinerary);
        }

        /// <summary>
        /// Overload: cho phép truyền TripPlannerViewModel để lưu lại SuggestionViewModel
        /// </summary>
        private string FormatOptimizedSuggestionInternal(
            TripPlannerViewModel plannerModel,
            TransportOption transport,
            decimal transportCost,
            string hotelDetails,
            decimal hotelCost,
            string foodDetails,
            decimal foodCost,
            decimal ticketCost,
            decimal localTransportCost,
            decimal miscCost,
            decimal totalCost,
            decimal remaining,
            int days,
            List<string> ticketDetails,
            List<string> localTransportDetails,
            List<string> warnings,
            string routeDetails,
            string clusterDetails,
            bool basicOnly,
            List<DailyItinerary> dailyItinerary = null)
        {
            // ✅ Sử dụng Service để tạo ViewModel và render HTML
            bool isTransportPriceCalculated = ViewBag.TransportPriceType != null && ViewBag.TransportPriceType == "Calculated";
            bool isMergedLocation = ViewBag.IsMergedLocation != null && (bool)ViewBag.IsMergedLocation;
            string oldLocationName = ViewBag.OldLocationName as string;
            string locationName = ViewBag.LocationName as string;

            var viewModel = _suggestionFormatter.CreateSuggestionViewModel(
                transport, transportCost, hotelDetails, hotelCost,
                foodDetails, foodCost, ticketCost, localTransportCost,
                miscCost, totalCost, remaining, days, ticketDetails,
                localTransportDetails, warnings, routeDetails, clusterDetails,
                basicOnly, dailyItinerary, isTransportPriceCalculated,
                isMergedLocation, oldLocationName, locationName);

            // Nếu có TripPlannerViewModel, lưu SuggestionViewModel vào đó
            if (plannerModel != null)
            {
                plannerModel.SuggestionViewModels ??= new List<SuggestionViewModel>();
                plannerModel.SuggestionViewModels.Add(viewModel);
            }

            // Expand local transport details nếu cần
            if (localTransportDetails != null && localTransportDetails.Any())
            {
                var expandedDetails = ExpandLocalTransportDetails(localTransportDetails, days);
                viewModel.LocalTransportDetails = expandedDetails;
            }

            return _suggestionFormatter.RenderSuggestionHtml(viewModel);
        }

        /// <summary>
        /// Format suggestion theo mức ngân sách - DẠNG INFOGRAPHIC TỔNG HỢP
        /// ⚠️ TODO: Cần refactor để sử dụng SuggestionFormatterService tương tự như FormatOptimizedSuggestion
        /// Phương thức này vẫn còn nhiều HTML hardcoded (hơn 600 dòng), cần được tách ra Partial View hoặc Service
        /// </summary>
        private string FormatOptimizedSuggestionByBudgetPlan(
            string planName,
            TransportOption transport,
            decimal transportCost,
            string hotelDetails,
            decimal hotelCost,
            string foodDetails,
            decimal foodCost,
            decimal ticketCost,
            decimal localTransportCost,
            decimal miscCost,
            decimal totalCost,
            decimal remaining,
            int days,
            List<string> ticketDetails,
            List<string> localTransportDetails,
            List<string> warnings,
            string routeDetails,
            string clusterDetails,
            bool basicOnly,
            List<DailyItinerary> dailyItinerary = null)
        {
            var sb = new StringBuilder();

            // ✅ BLOCK 1: HEADER - TÊN GỢI Ý THEO MỨC NGÂN SÁCH
            sb.Append($"<div style='background: linear-gradient(135deg, {(planName.Contains("TIẾT KIỆM") ? "#ff6b6b" : planName.Contains("CÂN BẰNG") ? "#4ecdc4" : "#95e1d3")}, {(planName.Contains("TIẾT KIỆM") ? "#ee5a6f" : planName.Contains("CÂN BẰNG") ? "#44a08d" : "#f38181")}); padding: 15px; border-radius: 12px; margin-bottom: 20px; color: white; box-shadow: 0 4px 15px rgba(0,0,0,0.1);'>");
            sb.Append($"<h4 style='margin: 0; font-size: 20px; font-weight: bold;'>{planName}</h4>");
            if (planName.Contains("TIẾT KIỆM"))
                sb.Append("<p style='margin: 8px 0 0 0; font-size: 14px; opacity: 0.95;'>Gói rẻ nhất có thể nhưng vẫn hợp lý</p>");
            else if (planName.Contains("CÂN BẰNG"))
                sb.Append("<p style='margin: 8px 0 0 0; font-size: 14px; opacity: 0.95;'>Gói có trải nghiệm tốt + chi phí hợp lý</p>");
            else
                sb.Append("<p style='margin: 8px 0 0 0; font-size: 14px; opacity: 0.95;'>Gói trải nghiệm cao cấp nhất trong ngân sách</p>");
            sb.Append("</div>");

            // ✅ TÍNH TOÁN THÔNG TIN TỔNG QUAN
            int nights = Math.Max(0, days - 1);
            int totalPlaces = 0;
            decimal totalKm = 0;

            if (dailyItinerary != null && dailyItinerary.Any())
            {
                totalPlaces = dailyItinerary.SelectMany(d => d.Places).Select(p => p.Id).Distinct().Count();
            }

            // Parse tổng km từ localTransportDetails
            if (localTransportDetails != null && localTransportDetails.Any())
            {
                foreach (var detail in localTransportDetails)
                {
                    var kmMatch = System.Text.RegularExpressions.Regex.Match(detail, @"Tổng quãng đường:\s*([\d,]+)\s*km", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (kmMatch.Success)
                    {
                        var kmStr = kmMatch.Groups[1].Value.Replace(",", "").Replace(".", "");
                        if (decimal.TryParse(kmStr, out var km))
                        {
                            totalKm += km;
                        }
                    }
                }
            }

            // ✅ BLOCK 2: TRIP SUMMARY - TỔNG QUAN CHUYẾN ĐI (INFOGRAPHIC) (Đặt ở cột phải)
            sb.Append("<div class='summary-section' style='background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%); padding: 25px; border-radius: 15px; margin-bottom: 20px; box-shadow: 0 4px 15px rgba(0,0,0,0.1);'>");
            sb.Append("<h3 style='margin: 0 0 20px 0; color: #333; font-size: 18px; font-weight: 700; border-bottom: 2px solid rgba(102, 126, 234, 0.3); padding-bottom: 10px;'>📊 TỔNG QUAN CHUYẾN ĐI</h3>");
            sb.Append("<div style='display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px;'>");

            // Tổng chi phí
            sb.Append($"<div style='background: white; padding: 15px; border-radius: 12px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>");
            sb.Append($"<div style='font-size: 24px; margin-bottom: 5px;'>💰</div>");
            sb.Append($"<div style='font-size: 14px; color: #666; margin-bottom: 5px;'>Tổng chi phí</div>");
            sb.Append($"<div style='font-size: 20px; font-weight: 700; color: #667eea;'>{FormatCurrency(totalCost)}</div>");
            sb.Append("</div>");

            // Còn lại
            sb.Append($"<div style='background: white; padding: 15px; border-radius: 12px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>");
            sb.Append($"<div style='font-size: 24px; margin-bottom: 5px;'>💸</div>");
            sb.Append($"<div style='font-size: 14px; color: #666; margin-bottom: 5px;'>Còn lại</div>");
            sb.Append($"<div style='font-size: 20px; font-weight: 700; color: #28a745;'>{FormatCurrency(remaining)}</div>");
            sb.Append("</div>");

            // Tổng ngày - đêm
            sb.Append($"<div style='background: white; padding: 15px; border-radius: 12px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>");
            sb.Append($"<div style='font-size: 24px; margin-bottom: 5px;'>🕒</div>");
            sb.Append($"<div style='font-size: 14px; color: #666; margin-bottom: 5px;'>Thời gian</div>");
            sb.Append($"<div style='font-size: 20px; font-weight: 700; color: #333;'>{days} ngày – {nights} đêm</div>");
            sb.Append("</div>");

            // Số điểm tham quan - với nút "Xem chi tiết" (Layout đẹp và gọn)
            string placesDetailId = $"places-detail-{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
            var uniquePlaces = new List<TouristPlace>();
            if (dailyItinerary != null && dailyItinerary.Any())
            {
                uniquePlaces = dailyItinerary
                    .SelectMany(d => d.Places)
                    .GroupBy(p => p.Id)
                    .Select(g => g.First())
                    .OrderBy(p => p.Name)
                    .ToList();
            }

            sb.Append($"<div style='background: white; padding: 12px; border-radius: 12px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.1); position: relative;'>");
            sb.Append($"<div style='font-size: 22px; margin-bottom: 4px;'>📍</div>");
            sb.Append($"<div style='font-size: 13px; color: #666; margin-bottom: 4px;'>Điểm tham quan</div>");
            sb.Append($"<div style='font-size: 18px; font-weight: 700; color: #333; margin-bottom: 8px;'>{totalPlaces} điểm</div>");

            // Nút "Xem chi tiết" - Compact và đẹp
            if (uniquePlaces.Any())
            {
                sb.Append($"<button onclick='togglePlacesDetail(\"{placesDetailId}\")' style='background: linear-gradient(135deg, #667eea, #764ba2); color: white; border: none; padding: 6px 12px; border-radius: 18px; font-size: 10px; font-weight: 600; cursor: pointer; transition: all 0.3s ease; box-shadow: 0 2px 6px rgba(102, 126, 234, 0.3); display: inline-flex; align-items: center; gap: 4px;' onmouseover='this.style.transform=\"translateY(-2px)\"; this.style.boxShadow=\"0 4px 12px rgba(102, 126, 234, 0.4)\"' onmouseout='this.style.transform=\"translateY(0)\"; this.style.boxShadow=\"0 2px 6px rgba(102, 126, 234, 0.3)\"'>");
                sb.Append($"<span id='{placesDetailId}-btn-text'>👁️ Xem chi tiết</span>");
                sb.Append($"</button>");

                // Danh sách địa điểm - Layout compact và đẹp (2 cột, gọn)
                sb.Append($"<div id='{placesDetailId}' style='display: none; margin-top: 10px; padding-top: 10px; border-top: 1px solid rgba(102, 126, 234, 0.15); animation: fadeIn 0.3s ease;'>");
                sb.Append($"<div style='display: grid; grid-template-columns: repeat(2, 1fr); gap: 5px; max-height: 180px; overflow-y: auto; padding-right: 4px;'>");

                foreach (var place in uniquePlaces)
                {
                    string shortName = place.Name.Length > 25 ? place.Name.Substring(0, 22) + "..." : place.Name;
                    string placeUrl = $"/TouristPlace/Display/{System.Web.HttpUtility.UrlEncode(place.Id)}";
                    sb.Append($"<a href='{placeUrl}' style='text-decoration: none; display: block;' onclick='event.stopPropagation();'>");
                    sb.Append($"<div style='background: linear-gradient(135deg, rgba(102, 126, 234, 0.06), rgba(118, 75, 162, 0.06)); padding: 5px 7px; border-radius: 5px; border-left: 2px solid #667eea; font-size: 10px; color: #333; display: flex; align-items: center; gap: 5px; transition: all 0.2s; cursor: pointer;' onmouseover='this.style.background=\"linear-gradient(135deg, rgba(102, 126, 234, 0.15), rgba(118, 75, 162, 0.15))\"; this.style.transform=\"translateX(3px)\"; this.style.boxShadow=\"0 2px 6px rgba(102, 126, 234, 0.3)\"; this.style.borderLeftColor=\"#764ba2\"' onmouseout='this.style.background=\"linear-gradient(135deg, rgba(102, 126, 234, 0.06), rgba(118, 75, 162, 0.06))\"; this.style.transform=\"translateX(0)\"; this.style.boxShadow=\"none\"; this.style.borderLeftColor=\"#667eea\"'>");
                    sb.Append($"<span style='color: #667eea; font-size: 9px; min-width: 10px; flex-shrink: 0;'>📍</span>");
                    sb.Append($"<span style='flex: 1; line-height: 1.3; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;' title='{System.Web.HttpUtility.HtmlAttributeEncode(place.Name)} - Click để xem chi tiết'>{System.Web.HttpUtility.HtmlEncode(shortName)}</span>");
                    sb.Append($"</div>");
                    sb.Append($"</a>");
                }

                sb.Append($"</div>");
                sb.Append($"</div>");
            }

            sb.Append("</div>");

            // Tổng km nội thành
            if (totalKm > 0)
            {
                sb.Append($"<div style='background: white; padding: 15px; border-radius: 12px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>");
                sb.Append($"<div style='font-size: 24px; margin-bottom: 5px;'>🚖</div>");
                sb.Append($"<div style='font-size: 14px; color: #666; margin-bottom: 5px;'>Tổng km nội thành</div>");
                sb.Append($"<div style='font-size: 20px; font-weight: 700; color: #333;'>{totalKm:F1} km</div>");
                sb.Append("</div>");
            }

            sb.Append("</div></div>");

            // ✅ BLOCK 3: COST BREAKDOWN - CHI PHÍ CHI TIẾT (Đặt ở cột trái) - DẠNG ACCORDION
            string expenseAccordionId = $"expense-accordion-{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
            sb.Append("<div class='expenses-section' style='background: white; padding: 25px; border-radius: 15px; margin-bottom: 20px; box-shadow: 0 4px 15px rgba(0,0,0,0.1);'>");
            sb.Append("<h3 style='margin: 0 0 20px 0; color: #333; font-size: 18px; font-weight: 700; border-bottom: 2px solid rgba(102, 126, 234, 0.3); padding-bottom: 10px;'>💳 CHI PHÍ CHI TIẾT</h3>");
            sb.Append("<div style='display: flex; flex-direction: column; gap: 10px;'>");

            // ✅ NHÓM 1: DI CHUYỂN (Tuyến xa + Nội thành)
            decimal totalTransportCost = transportCost + (basicOnly ? 0 : localTransportCost);
            if (totalTransportCost > 0)
            {
                string transportAccordionId = $"{expenseAccordionId}-transport";
                sb.Append($"<div class='expense-accordion-item' style='border: 1px solid rgba(102, 126, 234, 0.2); border-radius: 10px; overflow: hidden; background: white;'>");
                sb.Append($"<div class='expense-accordion-header' onclick='toggleExpenseAccordion(\"{transportAccordionId}\")' style='display: flex; justify-content: space-between; align-items: center; padding: 15px 18px; cursor: pointer; background: linear-gradient(135deg, rgba(102, 126, 234, 0.08), rgba(118, 75, 162, 0.08)); transition: all 0.3s ease;'>");
                sb.Append($"<div style='display: flex; align-items: center; gap: 12px;'>");
                sb.Append($"<span style='font-size: 22px;'>🚗</span>");
                sb.Append($"<strong style='color: #000 !important; font-size: 16px; font-weight: 700 !important;'>Di chuyển</strong>");
                sb.Append("</div>");
                sb.Append($"<div style='display: flex; align-items: center; gap: 15px;'>");
                sb.Append($"<span style='font-weight: 700; color: #667eea; font-size: 16px;'>{FormatCurrency(totalTransportCost)}</span>");
                sb.Append($"<span id='{transportAccordionId}-icon' style='font-size: 14px; color: #667eea; transition: transform 0.3s ease;'>▼</span>");
                sb.Append("</div>");
                sb.Append("</div>");
                sb.Append($"<div id='{transportAccordionId}' class='expense-accordion-content' style='display: none; padding: 15px 18px; background: rgba(102, 126, 234, 0.02);'>");

                // Di chuyển tuyến xa
                sb.Append($"<div style='margin-bottom: 12px; padding: 12px; background: white; border-radius: 8px; border-left: 3px solid #667eea;'>");
                sb.Append($"<div style='display: flex; justify-content: space-between; align-items: center;'>");
                sb.Append($"<div><span style='font-size: 16px; margin-right: 8px;'>🚌</span><strong style='color: #000; font-size: 14px; font-weight: 700;'>Di chuyển tuyến xa</strong></div>");
                sb.Append($"<span style='font-weight: 600; color: #667eea; font-size: 14px;'>{FormatCurrency(transportCost)}</span>");
                sb.Append("</div>");
                sb.Append($"<div style='margin-top: 6px; color: #444; font-size: 12px;'>{System.Web.HttpUtility.HtmlEncode(transport.Name)} ({transport.Type})</div>");
                sb.Append("</div>");

                // Di chuyển nội thành
                if (!basicOnly && localTransportCost > 0)
                {
                    string kmInfo = "";
                    if (localTransportDetails != null && localTransportDetails.Any())
                    {
                        foreach (var detail in localTransportDetails)
                        {
                            var cleanDetail = System.Text.RegularExpressions.Regex.Replace(detail, @"<[^>]+>", "").Trim();
                            if (cleanDetail.Contains("Tổng quãng đường"))
                            {
                                var kmMatch = System.Text.RegularExpressions.Regex.Match(cleanDetail, @"([\d,]+)\s*km", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                if (kmMatch.Success)
                                {
                                    kmInfo = $" (HSD: {kmMatch.Groups[1].Value} km)";
                                }
                                break;
                            }
                        }
                        if (string.IsNullOrEmpty(kmInfo) && totalKm > 0)
                        {
                            kmInfo = $" (HSD: {totalKm:F0} km)";
                        }
                    }

                    sb.Append($"<div style='padding: 12px; background: white; border-radius: 8px; border-left: 3px solid #4ecdc4;'>");
                    sb.Append($"<div style='display: flex; justify-content: space-between; align-items: center;'>");
                    sb.Append($"<div><span style='font-size: 16px; margin-right: 8px;'>🚖</span><strong style='color: #000; font-size: 14px; font-weight: 700;'>Di chuyển nội thành{kmInfo}</strong></div>");
                    sb.Append($"<span style='font-weight: 600; color: #667eea; font-size: 14px;'>{FormatCurrency(localTransportCost)}</span>");
                    sb.Append("</div>");

                    // Chi tiết di chuyển nội thành
                    if (localTransportDetails != null && localTransportDetails.Any())
                    {
                        var dayDetails = new List<string>();
                        foreach (var detail in localTransportDetails)
                        {
                            var cleanDetail = System.Text.RegularExpressions.Regex.Replace(detail, @"<[^>]+>", "").Trim();
                            if (!string.IsNullOrEmpty(cleanDetail) && cleanDetail.Contains("Ngày") && !cleanDetail.Contains("Tổng quãng đường"))
                            {
                                dayDetails.Add(cleanDetail);
                            }
                        }
                        if (dayDetails.Any())
                        {
                            sb.Append($"<div style='margin-top: 10px; padding-top: 10px; border-top: 1px solid rgba(0,0,0,0.05);'>");
                            foreach (var dayDetail in dayDetails)
                            {
                                sb.Append($"<div style='margin-bottom: 6px; color: #000; font-size: 11px; line-height: 1.4; font-weight: 500;'>• {System.Web.HttpUtility.HtmlEncode(dayDetail)}</div>");
                            }
                            sb.Append("</div>");
                        }
                    }
                    sb.Append("</div>");
                }

                sb.Append("</div></div>");
            }

            if (!basicOnly)
            {
                // ✅ NHÓM 2: LƯU TRÚ (Khách sạn)
                if (hotelCost > 0)
                {
                    string hotelAccordionId = $"{expenseAccordionId}-hotel";
                    sb.Append($"<div class='expense-accordion-item' style='border: 1px solid rgba(102, 126, 234, 0.2); border-radius: 10px; overflow: hidden; background: white;'>");
                    sb.Append($"<div class='expense-accordion-header' onclick='toggleExpenseAccordion(\"{hotelAccordionId}\")' style='display: flex; justify-content: space-between; align-items: center; padding: 15px 18px; cursor: pointer; background: linear-gradient(135deg, rgba(102, 126, 234, 0.08), rgba(118, 75, 162, 0.08)); transition: all 0.3s ease;'>");
                    sb.Append($"<div style='display: flex; align-items: center; gap: 12px;'>");
                    sb.Append($"<span style='font-size: 22px;'>🏨</span>");
                    sb.Append($"<strong style='color: #000 !important; font-size: 16px; font-weight: 700 !important;'>Lưu trú</strong>");
                    sb.Append("</div>");
                    sb.Append($"<div style='display: flex; align-items: center; gap: 15px;'>");
                    sb.Append($"<span style='font-weight: 700; color: #667eea; font-size: 16px;'>{FormatCurrency(hotelCost)}</span>");
                    sb.Append($"<span id='{hotelAccordionId}-icon' style='font-size: 14px; color: #667eea; transition: transform 0.3s ease;'>▼</span>");
                    sb.Append("</div>");
                    sb.Append("</div>");
                    sb.Append($"<div id='{hotelAccordionId}' class='expense-accordion-content' style='display: none; padding: 15px 18px; background: rgba(102, 126, 234, 0.02);'>");

                    // Chi tiết khách sạn
                    if (!string.IsNullOrEmpty(hotelDetails))
                    {
                        var hotelLines = hotelDetails.Split(new[] { "<br/>", "<br>" }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

                        if (hotelLines.Any())
                        {
                            sb.Append($"<div style='display: flex; flex-direction: column; gap: 10px;'>");
                            foreach (var line in hotelLines)
                            {
                                var cleanLine = System.Text.RegularExpressions.Regex.Replace(line, @"<[^>]+>", "").Trim();
                                if (string.IsNullOrEmpty(cleanLine) || !cleanLine.StartsWith("•")) continue;

                                // Parse: "• Tên KS (địa điểm): X đêm × giá"
                                var hotelMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"^•\s*(.+?)\s*\((.+?)\):\s*(.+)$");
                                string hotelName = hotelMatch.Success ? hotelMatch.Groups[1].Value.Trim() : cleanLine.Replace("•", "").Trim();
                                string locations = hotelMatch.Success ? hotelMatch.Groups[2].Value.Trim() : "";
                                string nightsInfo = hotelMatch.Success ? hotelMatch.Groups[3].Value.Trim() : "";

                                sb.Append($"<div style='background: white; padding: 12px 15px; border-radius: 8px; border-left: 3px solid #4ecdc4;'>");
                                sb.Append($"<div style='font-weight: 700; color: #000; font-size: 13px; margin-bottom: 6px;'>{System.Web.HttpUtility.HtmlEncode(hotelName)}</div>");
                                if (!string.IsNullOrEmpty(locations))
                                {
                                    sb.Append($"<div style='color: #444; font-size: 11px; margin-bottom: 6px; line-height: 1.3;'>{System.Web.HttpUtility.HtmlEncode(locations)}</div>");
                                }
                                if (!string.IsNullOrEmpty(nightsInfo))
                                {
                                    sb.Append($"<div style='color: #667eea; font-weight: 600; font-size: 12px;'>{System.Web.HttpUtility.HtmlEncode(nightsInfo)}</div>");
                                }
                                sb.Append("</div>");
                            }
                            sb.Append("</div>");
                        }
                    }
                    sb.Append("</div></div>");
                }

                // ✅ NHÓM 3: VÉ THAM QUAN
                if (ticketCost > 0 || (ticketDetails != null && ticketDetails.Any()))
                {
                    string ticketAccordionId = $"{expenseAccordionId}-ticket";
                    sb.Append($"<div class='expense-accordion-item' style='border: 1px solid rgba(102, 126, 234, 0.2); border-radius: 10px; overflow: hidden; background: white;'>");
                    sb.Append($"<div class='expense-accordion-header' onclick='toggleExpenseAccordion(\"{ticketAccordionId}\")' style='display: flex; justify-content: space-between; align-items: center; padding: 15px 18px; cursor: pointer; background: linear-gradient(135deg, rgba(102, 126, 234, 0.08), rgba(118, 75, 162, 0.08)); transition: all 0.3s ease;'>");
                    sb.Append($"<div style='display: flex; align-items: center; gap: 12px;'>");
                    sb.Append($"<span style='font-size: 22px;'>🎫</span>");
                    sb.Append($"<strong style='color: #000 !important; font-size: 16px; font-weight: 700 !important;'>Vé tham quan</strong>");
                    sb.Append("</div>");
                    sb.Append($"<div style='display: flex; align-items: center; gap: 15px;'>");
                    sb.Append($"<span style='font-weight: 700; color: #667eea; font-size: 16px;'>{FormatCurrency(ticketCost)}</span>");
                    sb.Append($"<span id='{ticketAccordionId}-icon' style='font-size: 14px; color: #667eea; transition: transform 0.3s ease;'>▼</span>");
                    sb.Append("</div>");
                    sb.Append("</div>");
                    sb.Append($"<div id='{ticketAccordionId}' class='expense-accordion-content' style='display: none; padding: 15px 18px; background: rgba(102, 126, 234, 0.02);'>");

                    // Chi tiết vé tham quan
                    if (ticketDetails != null && ticketDetails.Any())
                    {
                        var validDetails = ticketDetails
                            .Select(d => System.Text.RegularExpressions.Regex.Replace(d, @"<[^>]+>", "").Trim())
                            .Where(d => !string.IsNullOrEmpty(d) && !d.StartsWith("⚠️") && !d.StartsWith("Không"))
                            .ToList();

                        if (validDetails.Any())
                        {
                            sb.Append($"<div style='display: flex; flex-direction: column; gap: 8px;'>");
                            foreach (var detail in validDetails)
                            {
                                // Parse tên địa điểm và giá
                                var match = System.Text.RegularExpressions.Regex.Match(detail, @"^•\s*(.+?):\s*Vé tham quan\s*(.+)$");
                                string placeName = match.Success ? match.Groups[1].Value.Trim() : detail;
                                string price = match.Success ? match.Groups[2].Value.Trim() : "";

                                sb.Append($"<div style='background: white; padding: 10px 12px; border-radius: 8px; border-left: 3px solid #4ecdc4; display: flex; justify-content: space-between; align-items: center;'>");
                                sb.Append($"<span style='color: #000; font-size: 12px; font-weight: 600; flex: 1; line-height: 1.3;'>{System.Web.HttpUtility.HtmlEncode(placeName)}</span>");
                                if (!string.IsNullOrEmpty(price))
                                {
                                    sb.Append($"<span style='color: #667eea; font-weight: 700; font-size: 12px; margin-left: 10px; white-space: nowrap;'>{System.Web.HttpUtility.HtmlEncode(price)}</span>");
                                }
                                sb.Append("</div>");
                            }
                            sb.Append("</div>");
                        }
                    }
                    sb.Append("</div></div>");
                }

                // ✅ NHÓM 4: ĂN UỐNG
                if (foodCost > 0)
                {
                    string foodAccordionId = $"{expenseAccordionId}-food";
                    sb.Append($"<div class='expense-accordion-item' style='border: 1px solid rgba(102, 126, 234, 0.2); border-radius: 10px; overflow: hidden; background: white;'>");
                    sb.Append($"<div class='expense-accordion-header' onclick='toggleExpenseAccordion(\"{foodAccordionId}\")' style='display: flex; justify-content: space-between; align-items: center; padding: 15px 18px; cursor: pointer; background: linear-gradient(135deg, rgba(102, 126, 234, 0.08), rgba(118, 75, 162, 0.08)); transition: all 0.3s ease;'>");
                    sb.Append($"<div style='display: flex; align-items: center; gap: 12px;'>");
                    sb.Append($"<span style='font-size: 22px;'>🍽️</span>");
                    sb.Append($"<strong style='color: #000 !important; font-size: 16px; font-weight: 700 !important;'>Ăn uống</strong> <span style='color: #444 !important; font-size: 13px; font-weight: 500 !important;'>({days} ngày)</span>");
                    sb.Append("</div>");
                    sb.Append($"<div style='display: flex; align-items: center; gap: 15px;'>");
                    sb.Append($"<span style='font-weight: 700; color: #667eea; font-size: 16px;'>{FormatCurrency(foodCost)}</span>");
                    sb.Append($"<span id='{foodAccordionId}-icon' style='font-size: 14px; color: #667eea; transition: transform 0.3s ease;'>▼</span>");
                    sb.Append("</div>");
                    sb.Append("</div>");
                    sb.Append($"<div id='{foodAccordionId}' class='expense-accordion-content' style='display: none; padding: 15px 18px; background: rgba(102, 126, 234, 0.02);'>");

                    // Chi tiết ăn uống từng ngày - Responsive layout (compact cho chuyến đi dài)
                    var foodLines = foodDetails.Split(new[] { "<br/>", "<br>" }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(l => l.Contains("Ngày"))
                        .Take(days)
                        .ToList();

                    if (foodLines.Any())
                    {
                        // ✅ Nếu > 5 ngày: hiển thị dạng compact table
                        // ✅ Nếu <= 5 ngày: hiển thị card đẹp như cũ
                        if (days > 5)
                        {
                            // Compact table layout cho chuyến đi dài
                            sb.Append($"<div style='background: white; border-radius: 10px; overflow: hidden; box-shadow: 0 2px 6px rgba(0,0,0,0.08); margin-bottom: 15px;'>");
                            sb.Append($"<table style='width: 100%; border-collapse: collapse;'>");
                            sb.Append($"<thead>");
                            sb.Append($"<tr style='background: linear-gradient(135deg, rgba(240, 147, 251, 0.2), rgba(245, 87, 108, 0.2));'>");
                            sb.Append($"<th style='padding: 10px 12px; text-align: left; font-weight: 700; color: #333; font-size: 12px; width: 60px;'>Ngày</th>");
                            sb.Append($"<th style='padding: 10px 12px; text-align: left; font-weight: 700; color: #333; font-size: 12px;'>🌅 Sáng</th>");
                            sb.Append($"<th style='padding: 10px 12px; text-align: left; font-weight: 700; color: #333; font-size: 12px;'>☀️ Trưa</th>");
                            sb.Append($"<th style='padding: 10px 12px; text-align: left; font-weight: 700; color: #333; font-size: 12px;'>🌙 Tối</th>");
                            sb.Append($"</tr>");
                            sb.Append($"</thead>");
                            sb.Append($"<tbody>");

                            foreach (var line in foodLines)
                            {
                                var cleanLine = System.Text.RegularExpressions.Regex.Replace(line, @"<[^>]+>", "").Trim();
                                if (string.IsNullOrEmpty(cleanLine)) continue;

                                // Parse: "Ngày X (địa điểm): Sáng: ... | Trưa: ... | Tối: ..."
                                var dayMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"^•\s*Ngày\s*(\d+)\s*(?:\((.+?)\))?:\s*(.+)$");
                                string dayNum = dayMatch.Success ? dayMatch.Groups[1].Value : "";
                                string meals = dayMatch.Success ? dayMatch.Groups[3].Value : cleanLine.Replace("•", "").Trim();

                                // Parse các bữa ăn
                                var mealParts = meals.Split('|').Select(m => m.Trim()).Where(m => !string.IsNullOrEmpty(m)).ToList();

                                // Tìm từng bữa
                                string morningMeal = "";
                                string lunchMeal = "";
                                string dinnerMeal = "";

                                foreach (var meal in mealParts)
                                {
                                    var mealMatch = System.Text.RegularExpressions.Regex.Match(meal, @"^(Sáng|Trưa|Tối):\s*(.+?)\s*\((.+?)\)$");
                                    string mealType = mealMatch.Success ? mealMatch.Groups[1].Value : "";
                                    string restaurant = mealMatch.Success ? mealMatch.Groups[2].Value.Trim() : "";
                                    string price = mealMatch.Success ? mealMatch.Groups[3].Value.Trim() : "";

                                    string mealText = "";
                                    if (!string.IsNullOrEmpty(restaurant))
                                    {
                                        string shortRestaurant = restaurant.Length > 18 ? restaurant.Substring(0, 15) + "..." : restaurant;
                                        // ✅ Chỉ encode text content, không encode HTML tags
                                        string encodedRestaurant = System.Web.HttpUtility.HtmlEncode(shortRestaurant);
                                        string encodedPrice = System.Web.HttpUtility.HtmlEncode(price);
                                        mealText = $"{encodedRestaurant}<br/><span style='color: #667eea; font-size: 10px;'>{encodedPrice}</span>";
                                    }

                                    if (mealType == "Sáng") morningMeal = mealText;
                                    else if (mealType == "Trưa") lunchMeal = mealText;
                                    else if (mealType == "Tối") dinnerMeal = mealText;
                                }

                                sb.Append($"<tr style='border-bottom: 1px solid rgba(0,0,0,0.05);'>");
                                sb.Append($"<td style='padding: 10px 12px; text-align: center;'>");
                                sb.Append($"<span style='background: linear-gradient(135deg, #f093fb, #f5576c); color: white; padding: 4px 8px; border-radius: 6px; font-weight: 700; font-size: 11px; display: inline-block;'>{dayNum}</span>");
                                sb.Append($"</td>");
                                // ✅ Không encode HTML, chỉ render trực tiếp (đã encode text content rồi)
                                sb.Append($"<td style='padding: 10px 12px; font-size: 11px; color: #333; line-height: 1.4;'>{morningMeal}</td>");
                                sb.Append($"<td style='padding: 10px 12px; font-size: 11px; color: #333; line-height: 1.4;'>{lunchMeal}</td>");
                                sb.Append($"<td style='padding: 10px 12px; font-size: 11px; color: #333; line-height: 1.4;'>{dinnerMeal}</td>");
                                sb.Append($"</tr>");
                            }

                            sb.Append($"</tbody>");
                            sb.Append($"</table>");
                            sb.Append($"</div>");
                        }
                        else
                        {
                            // Card layout đẹp cho chuyến đi ngắn (<= 5 ngày)
                            sb.Append($"<div style='display: flex; flex-direction: column; gap: 10px; margin-bottom: 15px;'>");
                            foreach (var line in foodLines)
                            {
                                var cleanLine = System.Text.RegularExpressions.Regex.Replace(line, @"<[^>]+>", "").Trim();
                                if (string.IsNullOrEmpty(cleanLine)) continue;

                                // Parse: "Ngày X (địa điểm): Sáng: ... | Trưa: ... | Tối: ..."
                                var dayMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"^•\s*Ngày\s*(\d+)\s*(?:\((.+?)\))?:\s*(.+)$");
                                string dayNum = dayMatch.Success ? dayMatch.Groups[1].Value : "";
                                string dayPlaces = dayMatch.Success ? dayMatch.Groups[2].Value : "";
                                string meals = dayMatch.Success ? dayMatch.Groups[3].Value : cleanLine.Replace("•", "").Trim();

                                // Parse các bữa ăn
                                var mealParts = meals.Split('|').Select(m => m.Trim()).Where(m => !string.IsNullOrEmpty(m)).ToList();

                                sb.Append($"<div style='background: white; padding: 12px 15px; border-radius: 10px; border-left: 4px solid #f093fb; box-shadow: 0 2px 6px rgba(0,0,0,0.08);'>");
                                sb.Append($"<div style='display: flex; align-items: center; gap: 8px; margin-bottom: 8px;'>");
                                sb.Append($"<span style='background: linear-gradient(135deg, #f093fb, #f5576c); color: white; width: 28px; height: 28px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: 700; font-size: 12px;'>Ngày {dayNum}</span>");
                                if (!string.IsNullOrEmpty(dayPlaces))
                                {
                                    string shortPlaces = dayPlaces.Length > 40 ? dayPlaces.Substring(0, 37) + "..." : dayPlaces;
                                    sb.Append($"<span style='color: #666; font-size: 11px;'>{System.Web.HttpUtility.HtmlEncode(shortPlaces)}</span>");
                                }
                                sb.Append("</div>");

                                // Hiển thị 3 bữa trong grid
                                sb.Append($"<div style='display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px;'>");
                                foreach (var meal in mealParts)
                                {
                                    var mealMatch = System.Text.RegularExpressions.Regex.Match(meal, @"^(Sáng|Trưa|Tối):\s*(.+?)\s*\((.+?)\)$");
                                    string mealType = mealMatch.Success ? mealMatch.Groups[1].Value : meal.Split(':').FirstOrDefault()?.Trim() ?? "";
                                    string restaurant = mealMatch.Success ? mealMatch.Groups[2].Value : "";
                                    string price = mealMatch.Success ? mealMatch.Groups[3].Value : "";

                                    if (string.IsNullOrEmpty(mealType)) continue;

                                    string mealIcon = mealType == "Sáng" ? "🌅" : mealType == "Trưa" ? "☀️" : "🌙";
                                    string mealColor = mealType == "Sáng" ? "#ffa726" : mealType == "Trưa" ? "#4ecdc4" : "#f093fb";

                                    sb.Append($"<div style='background: rgba(240, 147, 251, 0.05); padding: 8px 10px; border-radius: 8px; border-left: 2px solid {mealColor};'>");
                                    sb.Append($"<div style='display: flex; align-items: center; gap: 4px; margin-bottom: 4px;'>");
                                    sb.Append($"<span style='font-size: 14px;'>{mealIcon}</span>");
                                    sb.Append($"<span style='color: {mealColor}; font-weight: 700; font-size: 11px;'>{mealType}</span>");
                                    sb.Append("</div>");
                                    if (!string.IsNullOrEmpty(restaurant))
                                    {
                                        string shortRestaurant = restaurant.Length > 20 ? restaurant.Substring(0, 17) + "..." : restaurant;
                                        sb.Append($"<div style='color: #333; font-size: 11px; font-weight: 500; margin-bottom: 2px;'>{System.Web.HttpUtility.HtmlEncode(shortRestaurant)}</div>");
                                    }
                                    if (!string.IsNullOrEmpty(price))
                                    {
                                        sb.Append($"<div style='color: #667eea; font-weight: 600; font-size: 11px;'>{System.Web.HttpUtility.HtmlEncode(price)}</div>");
                                    }
                                    sb.Append("</div>");
                                }
                                sb.Append("</div>"); // End grid
                                sb.Append("</div>"); // End day card
                            }
                            sb.Append("</div>"); // End days container
                        }
                    }
                    sb.Append("</div></div>");
                }

                // ✅ NHÓM 5: CHI PHÍ KHÁC
                if (miscCost > 0)
                {
                    string miscAccordionId = $"{expenseAccordionId}-misc";
                    sb.Append($"<div class='expense-accordion-item' style='border: 1px solid rgba(102, 126, 234, 0.2); border-radius: 10px; overflow: hidden; background: white;'>");
                    sb.Append($"<div class='expense-accordion-header' onclick='toggleExpenseAccordion(\"{miscAccordionId}\")' style='display: flex; justify-content: space-between; align-items: center; padding: 15px 18px; cursor: pointer; background: linear-gradient(135deg, rgba(102, 126, 234, 0.08), rgba(118, 75, 162, 0.08)); transition: all 0.3s ease;'>");
                    sb.Append($"<div style='display: flex; align-items: center; gap: 12px;'>");
                    sb.Append($"<span style='font-size: 22px;'>💡</span>");
                    sb.Append($"<strong style='color: #000 !important; font-size: 16px; font-weight: 700 !important;'>Chi phí khác</strong>");
                    sb.Append("</div>");
                    sb.Append($"<div style='display: flex; align-items: center; gap: 15px;'>");
                    sb.Append($"<span style='font-weight: 700; color: #667eea; font-size: 16px;'>{FormatCurrency(miscCost)}</span>");
                    sb.Append($"<span id='{miscAccordionId}-icon' style='font-size: 14px; color: #667eea; transition: transform 0.3s ease;'>▼</span>");
                    sb.Append("</div>");
                    sb.Append("</div>");
                    sb.Append($"<div id='{miscAccordionId}' class='expense-accordion-content' style='display: none; padding: 15px 18px; background: rgba(102, 126, 234, 0.02);'>");
                    sb.Append($"<div style='background: white; padding: 12px 15px; border-radius: 8px; border-left: 3px solid #667eea;'>");
                    sb.Append($"<div style='color: #000; font-size: 12px; line-height: 1.5; font-weight: 500;'>");
                    sb.Append($"Chi phí phát sinh bao gồm: quà lưu niệm, cà phê, thuê áo mưa, phí gửi xe và các chi phí khác không dự kiến trước.");
                    sb.Append($"</div>");
                    sb.Append($"<div style='margin-top: 8px; color: #667eea; font-weight: 700; font-size: 13px;'>Tỷ lệ: 10% tổng chi phí</div>");
                    sb.Append("</div>");
                    sb.Append("</div></div>");
                }
            }

            sb.Append("</div></div>");

            // ✅ CẢNH BÁO (nếu có)
            if (ViewBag.TransportPriceType != null && ViewBag.TransportPriceType == "Calculated")
            {
                sb.Append("<div style='background: rgba(255, 193, 7, 0.1); border-left: 4px solid #ffc107; padding: 12px; border-radius: 8px; margin-bottom: 15px; color: #856404;'>");
                sb.Append("⚠️ <strong>Lưu ý:</strong> Giá vận chuyển được tính ước lượng theo khoảng cách GPS. ");
                sb.Append("Vui lòng liên hệ nhà xe để biết giá chính xác.");
                sb.Append("</div>");
            }

            // ✅ BLOCK 4: LỊCH TRÌNH CHI TIẾT - VERTICAL TIMELINE (giữ nguyên nội dung text)
            if (!basicOnly && dailyItinerary != null && dailyItinerary.Any())
            {
                sb.Append("<div style='background: white; padding: 25px; border-radius: 15px; margin-bottom: 20px; box-shadow: 0 4px 15px rgba(0,0,0,0.1);'>");
                sb.Append("<h3 style='margin: 0 0 25px 0; color: #333; font-size: 18px; font-weight: 700; border-bottom: 2px solid rgba(102, 126, 234, 0.3); padding-bottom: 10px;'>📅 LỊCH TRÌNH CHI TIẾT</h3>");

                // Timeline container - vertical timeline
                sb.Append("<div style='position: relative; padding: 20px 0 20px 60px; background: linear-gradient(to bottom, rgba(240, 248, 255, 0.5), rgba(255, 250, 240, 0.5)); border-radius: 12px;'>");

                // Vertical timeline line
                sb.Append("<div style='position: absolute; left: 30px; top: 0; bottom: 0; width: 3px; background: linear-gradient(to bottom, #4ecdc4, #95e1d3); border-radius: 2px;'></div>");

                var groupedByCluster = dailyItinerary.GroupBy(d => d.ClusterIndex).OrderBy(g => g.Key).ToList();
                var colors = new[] { "#4ecdc4", "#95e1d3", "#f093fb", "#ff6b6b", "#ffa726", "#667eea" };
                int itemIndex = 0;

                foreach (var clusterGroup in groupedByCluster)
                {
                    int clusterIdx = clusterGroup.Key;
                    var daysInCluster = clusterGroup.OrderBy(d => d.DayNumber).ToList();
                    var firstDay = daysInCluster.First();
                    string hotelName = firstDay.Hotel?.Name ?? "Chưa chọn KS";
                    int clusterDays = daysInCluster.Count;
                    string color = colors[clusterIdx % colors.Length];

                    // Hotel card (pill-shaped) - Giai đoạn
                    sb.Append($"<div style='position: relative; margin-bottom: 25px; padding-left: 50px;'>");

                    // Timeline dot
                    sb.Append($"<div style='position: absolute; left: -45px; top: 10px; width: 20px; height: 20px; background: {color}; border: 4px solid white; border-radius: 50%; box-shadow: 0 2px 8px rgba(0,0,0,0.2); z-index: 10;'></div>");

                    // Connecting line to timeline
                    sb.Append($"<div style='position: absolute; left: -30px; top: 20px; width: 25px; height: 2px; background: {color}; opacity: 0.5;'></div>");

                    // Hotel card
                    sb.Append($"<div style='background: {color}; color: white; padding: 15px 20px; border-radius: 25px; font-size: 14px; font-weight: 700; box-shadow: 0 3px 10px rgba(0,0,0,0.15); display: inline-block; margin-bottom: 15px;'>");
                    sb.Append($"Giai đoạn {clusterIdx + 1} ({clusterDays} ngày tại {System.Web.HttpUtility.HtmlEncode(hotelName)})");
                    sb.Append("</div>");

                    // Days list
                    sb.Append("<div style='padding-left: 10px;'>");
                    foreach (var day in daysInCluster)
                    {
                        if (day.Places != null && day.Places.Any())
                        {
                            // Place cards (smaller pills)
                            var placeNames = string.Join(", ", day.Places.Select(p => p.Name));
                            sb.Append($"<div style='margin-bottom: 12px; padding-left: 30px; position: relative;'>");

                            // Small dot for day
                            sb.Append($"<div style='position: absolute; left: 0; top: 8px; width: 12px; height: 12px; background: {color}; border: 2px solid white; border-radius: 50%; box-shadow: 0 1px 4px rgba(0,0,0,0.15);'></div>");

                            // Connecting line
                            sb.Append($"<div style='position: absolute; left: 6px; top: 20px; width: 2px; height: 100%; background: {color}; opacity: 0.3;'></div>");

                            // Day label + places
                            sb.Append($"<div style='background: rgba(78, 205, 196, 0.1); padding: 10px 15px; border-radius: 15px; border-left: 3px solid {color}; margin-left: 20px;'>");
                            sb.Append($"<span style='color: {color}; font-weight: 700; font-size: 13px;'>Ngày {day.DayNumber}:</span> ");
                            sb.Append($"<span style='color: #333; font-size: 13px;'>{System.Web.HttpUtility.HtmlEncode(placeNames)}</span>");
                            sb.Append("</div>");
                            sb.Append("</div>");
                        }
                        else
                        {
                            // Nghỉ ngơi
                            sb.Append($"<div style='margin-bottom: 12px; padding-left: 30px; position: relative;'>");
                            sb.Append($"<div style='position: absolute; left: 0; top: 8px; width: 12px; height: 12px; background: #ccc; border: 2px solid white; border-radius: 50%;'></div>");
                            sb.Append($"<div style='background: rgba(200, 200, 200, 0.1); padding: 10px 15px; border-radius: 15px; border-left: 3px solid #ccc; margin-left: 20px;'>");
                            sb.Append($"<span style='color: #999; font-weight: 600; font-size: 13px;'>Ngày {day.DayNumber}:</span> ");
                            sb.Append($"<span style='color: #666; font-size: 13px;'>Nghỉ ngơi/tự do</span>");
                            sb.Append("</div>");
                            sb.Append("</div>");
                        }
                    }
                    sb.Append("</div>"); // End days list
                    sb.Append("</div>"); // End cluster item

                    itemIndex++;
                }

                sb.Append("</div>"); // End timeline container
                sb.Append("</div>"); // End main container
            }

            // ✅ CẢNH BÁO (nếu có)
            if (!basicOnly && warnings.Any())
            {
                sb.Append("<div style='background: rgba(255, 193, 7, 0.1); border-left: 4px solid #ffc107; padding: 12px; border-radius: 8px; margin-bottom: 15px; color: #856404;'>");
                sb.Append("⚠️ <strong>Lưu ý:</strong><br/>");
                foreach (var w in warnings) sb.Append($"{w}<br/>");
                sb.Append("</div>");
            }

            var content = sb.ToString();
            return $"<div class='suggestion' data-transport='{transportCost}' data-hotel='{hotelCost}' data-food='{foodCost}' data-ticket='{ticketCost}' data-local='{localTransportCost}' data-misc='{miscCost}' data-total='{totalCost}'>{content}</div>";
        }

        private List<TouristPlace> SuggestDefaultPlaces(int count = 6)
        {
            return _context.TouristPlaces
                .OrderByDescending(p => p.Rating)
                .ThenBy(p => p.Name)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Tái tạo clusters từ dailyItinerary để đảm bảo đồng bộ với lịch trình thực tế
        /// </summary>
        private List<PlaceCluster> ReconstructClustersFromItinerary(List<DailyItinerary> dailyItinerary)
        {
            var clusters = new List<PlaceCluster>();

            if (dailyItinerary == null || !dailyItinerary.Any())
                return clusters;

            // Group theo ClusterIndex
            var groupedByCluster = dailyItinerary
                .Where(d => d.ClusterIndex >= 0)
                .GroupBy(d => d.ClusterIndex)
                .OrderBy(g => g.Key)
                .ToList();

            // ✅ FIX: Tính số đêm thực tế dựa trên khách sạn
            // Đếm số lần khách sạn xuất hiện liên tiếp trong mỗi cluster
            var orderedDays = dailyItinerary
                .Where(d => d.ClusterIndex >= 0)
                .OrderBy(d => d.DayNumber)
                .ToList();

            foreach (var clusterGroup in groupedByCluster)
            {
                var cluster = new PlaceCluster { Places = new List<TouristPlace>() };

                // Lấy tất cả địa điểm từ các ngày trong cluster này (loại bỏ trùng lặp)
                var seenPlaceIds = new HashSet<string>();
                var clusterDays = clusterGroup.OrderBy(d => d.DayNumber).ToList();
                foreach (var day in clusterDays)
                {
                    foreach (var place in day.Places)
                    {
                        if (!seenPlaceIds.Contains(place.Id))
                        {
                            cluster.Places.Add(place);
                            seenPlaceIds.Add(place.Id);
                        }
                    }
                }

                // ✅ FIX: Tính số đêm dựa trên khách sạn thực tế
                // Đếm số đêm = số ngày trong cluster (vì mỗi ngày có 1 đêm sau đó)
                // Nhưng phải xem xét đêm cuối cùng của cluster có thuộc cluster này không
                int daysInCluster = clusterDays.Count;
                int clusterStartDay = clusterDays.First().DayNumber;
                int clusterEndDay = clusterDays.Last().DayNumber;

                // Đếm số đêm: từ đêm sau ngày đầu đến đêm sau ngày cuối của cluster
                // Nếu cluster cuối cùng: bao gồm cả đêm sau ngày cuối
                // Nếu không phải cluster cuối: chỉ tính đến đêm trước khi chuyển cluster
                bool isLastCluster = clusterGroup.Key == groupedByCluster.Last().Key;

                if (isLastCluster)
                {
                    // Cluster cuối: số đêm = số ngày (vì bao gồm cả đêm sau ngày cuối)
                    // Nhưng thực tế với N ngày thì có N-1 đêm giữa các ngày
                    // Và thêm 1 đêm sau ngày cuối = N đêm
                    // Tuy nhiên, theo logic thông thường: N ngày = N-1 đêm
                    // Nhưng nếu tính cả đêm cuối cùng trước khi về thì = N đêm
                    // Để đảm bảo đúng, ta tính: số đêm = số ngày (vì đêm sau ngày cuối vẫn cần nghỉ)
                    cluster.RecommendedNights = daysInCluster;
                }
                else
                {
                    // Cluster không phải cuối: số đêm = số ngày (bao gồm đêm sau ngày cuối trước khi chuyển)
                    cluster.RecommendedNights = daysInCluster;
                }

                // ✅ FIX THỨ 2: Đếm số đêm dựa trên khách sạn thực tế từ dailyItinerary
                // Tìm khách sạn của cluster này
                var clusterHotel = clusterDays.FirstOrDefault()?.Hotel;
                if (clusterHotel != null)
                {
                    // Đếm số ngày liên tiếp sử dụng cùng một khách sạn này
                    int consecutiveNights = 0;
                    int currentDayNum = clusterStartDay;

                    // Đếm từ ngày đầu của cluster đến hết các ngày sử dụng cùng khách sạn
                    foreach (var day in orderedDays.Where(d => d.DayNumber >= clusterStartDay))
                    {
                        if (day.Hotel != null && day.Hotel.Id == clusterHotel.Id)
                        {
                            consecutiveNights++;
                            currentDayNum = day.DayNumber;
                        }
                        else if (day.DayNumber > clusterEndDay)
                        {
                            // Đã vượt quá cluster này
                            break;
                        }
                    }

                    // Số đêm = số ngày sử dụng khách sạn (mỗi ngày có 1 đêm sau đó)
                    // Nhưng với N ngày thì có N-1 đêm giữa các ngày + 1 đêm sau ngày cuối = N đêm
                    // Tuy nhiên, logic chuẩn: N ngày = N-1 đêm
                    // Nhưng trong trường hợp này, ta cần tính cả đêm sau ngày cuối của cluster
                    // Vì vậy: số đêm = số ngày trong cluster
                    cluster.RecommendedNights = daysInCluster;
                }
                else
                {
                    // Fallback: dùng công thức cũ
                    cluster.RecommendedNights = Math.Max(0, daysInCluster - 1);
                }

                clusters.Add(cluster);
            }

            // ✅ FIX CUỐI CÙNG: Điều chỉnh để tổng số đêm = tổng số ngày - 1
            // Tính tổng số đêm hiện tại
            int totalNightsCalculated = clusters.Sum(c => c.RecommendedNights);
            int totalDays = dailyItinerary.Count;
            int expectedTotalNights = Math.Max(0, totalDays - 1);

            if (totalNightsCalculated != expectedTotalNights && expectedTotalNights > 0)
            {
                // Điều chỉnh: phân bổ lại số đêm cho các cluster
                // Cluster cuối cùng sẽ nhận phần chênh lệch
                if (clusters.Any())
                {
                    int nightsAssigned = clusters.Take(clusters.Count - 1).Sum(c => c.RecommendedNights);
                    clusters.Last().RecommendedNights = Math.Max(0, expectedTotalNights - nightsAssigned);
                }
            }

            return clusters;
        }

        // SỬA HÀM CalculatePersonalVehicleTransportWithFullSchedule
        private (decimal TotalCost, List<string> Details) CalculatePersonalVehicleTransportWithFullSchedule(
            List<TouristPlace> places,
            int days,
            double startLat,
            double startLng,
            TransportOption transport,
            List<string> details)
        {
            decimal totalCost = 0;
            double totalDistance = 0;
            var allDayRoutes = new List<string>();

            // Phân bổ địa điểm theo ngày
            // ✅ Sử dụng ItineraryService
            var dailyPlaces = _itineraryService.DistributePlacesAcrossDays(places, days);

            // SỬA: HIỂN THỊ CHÍNH XÁC TẤT CẢ CÁC NGÀY (từ 1 đến days)
            for (int day = 0; day < days; day++) // SỬA: dùng index 0-based
            {
                var dayPlaces = dailyPlaces[day]; // Lấy danh sách địa điểm cho ngày này
                int displayDay = day + 1; // Hiển thị là ngày 1, 2, 3...

                if (!dayPlaces.Any())
                {
                    allDayRoutes.Add($"Ngày {displayDay}: Nghỉ ngơi/tự do khám phá (~5 km)");
                    totalDistance += 5; // Chi phí di chuyển nhỏ cho ngày nghỉ
                }
                else
                {
                    var (dayDistance, routeDescription) = CalculateDayRoute(dayPlaces, startLat, startLng, displayDay);
                    totalDistance += dayDistance;
                    allDayRoutes.Add(routeDescription);
                }
            }

            // Tính chi phí nhiên liệu
            if (transport.FuelConsumption > 0 && transport.FuelPrice > 0)
            {
                decimal fuelUsed = ((decimal)totalDistance * transport.FuelConsumption) / 100m;
                totalCost = fuelUsed * transport.FuelPrice;

                details.Add($"• {transport.Name} (phương tiện cá nhân)");
                details.AddRange(allDayRoutes); // HIỂN THỊ TẤT CẢ NGÀY
                details.Add($"↳ Tổng quãng đường: ~{totalDistance:F1} km");
                details.Add($"↳ Nhiên liệu: {fuelUsed:F2} lít × {transport.FuelPrice:N0}đ = {FormatCurrency(totalCost)}");
            }
            else
            {
                totalCost = (decimal)totalDistance * 3000; // 3,000đ/km ước tính
                details.Add($"• {transport.Name} (ước tính)");
                details.AddRange(allDayRoutes);
                details.Add($"↳ Chi phí ước tính: {FormatCurrency(totalCost)} (~{totalDistance:F1} km)");
            }

            return (totalCost, details);
        }

        // SỬA HÀM CalculateTaxiTransportWithFullSchedule  
        private (decimal TotalCost, List<string> Details) CalculateTaxiTransportWithFullSchedule(
            List<TouristPlace> places,
            int days,
            double startLat,
            double startLng,
            List<string> details)
        {
            decimal totalCost = 0;
            double totalDistance = 0;
            var allDayRoutes = new List<string>();
            decimal taxiRatePerKm = 15000; // 15,000đ/km

            // Phân bổ địa điểm theo ngày
            // ✅ Sử dụng ItineraryService
            var dailyPlaces = _itineraryService.DistributePlacesAcrossDays(places, days);

            // SỬA: HIỂN THỊ CHÍNH XÁC TẤT CẢ CÁC NGÀY
            for (int day = 0; day < days; day++) // SỬA: dùng index 0-based
            {
                var dayPlaces = dailyPlaces[day]; // Lấy danh sách địa điểm cho ngày này
                int displayDay = day + 1; // Hiển thị là ngày 1, 2, 3...

                if (!dayPlaces.Any())
                {
                    allDayRoutes.Add($"Ngày {displayDay}: Nghỉ ngơi/tự do khám phá (~0 km taxi)");
                    // Không tính chi phí taxi cho ngày nghỉ
                }
                else
                {
                    var (dayDistance, routeDescription) = CalculateDayRoute(dayPlaces, startLat, startLng, displayDay);
                    totalDistance += dayDistance;
                    allDayRoutes.Add(routeDescription);
                }
            }

            // Tính tổng chi phí taxi
            totalCost = (decimal)totalDistance * taxiRatePerKm;

            details.Add("• Taxi nội thành");
            details.AddRange(allDayRoutes); // HIỂN THỊ TẤT CẢ NGÀY
            details.Add($"↳ Tổng quãng đường: {totalDistance:F1} km × {taxiRatePerKm:N0}đ/km = {FormatCurrency(totalCost)}");

            if (totalDistance > 50)
            {
                details.Add($"↳ Lưu ý: Quãng đường dài, có thể thương lượng giá theo ngày");
            }

            return (totalCost, details);
        }

        private List<string> ExpandLocalTransportDetails(List<string> details, int days)
        {
            var result = new List<string>(details ?? new List<string>());
            var existingDays = new HashSet<int>();
            var regex = new Regex("^Ngày\\s+(\\d+)");

            foreach (var line in result)
            {
                var match = regex.Match(line);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
                {
                    existingDays.Add(n);
                }
            }

            for (int d = 1; d <= days; d++)
            {
                if (!existingDays.Contains(d))
                {
                    result.Add($"Ngày {d}: Nghỉ ngơi/tự do khám phá (~0 km taxi)");
                }
            }

            // Sắp xếp lại các dòng Ngày X theo thứ tự tăng dần, giữ nguyên các dòng không phải 'Ngày'
            var headerLines = result.Where(l => !regex.IsMatch(l)).ToList();
            var dayLines = result.Where(l => regex.IsMatch(l))
                                 .OrderBy(l => int.Parse(regex.Match(l).Groups[1].Value))
                                 .ToList();

            var merged = new List<string>();
            merged.AddRange(headerLines);
            merged.AddRange(dayLines);
            return merged;
        }


        // ✅ MỚI: Lọc địa điểm theo mức ngân sách (xoay địa điểm theo giá vé)
        private List<TouristPlace> FilterPlacesByBudgetPlan(
            List<TouristPlace> places,
            BudgetPlanType planType)
        {
            if (!places.Any()) return places;

            // Lấy giá vé cho từng địa điểm
            var placeIds = places.Select(p => p.Id).ToList();
            var ticketPrices = _context.Attractions
                .AsNoTracking()
                .Where(a => placeIds.Contains(a.TouristPlaceId))
                .GroupBy(a => a.TouristPlaceId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(a => a.TicketPrice)
                );

            switch (planType)
            {
                case BudgetPlanType.Budget:
                    // 🔥 TIẾT KIỆM: Ưu tiên địa điểm free/giá vé thấp (0-50k)
                    return places
                        .OrderBy(p =>
                        {
                            decimal ticketPrice = ticketPrices.GetValueOrDefault(p.Id, 0);
                            // Ưu tiên free (0đ) hoặc giá rẻ
                            if (ticketPrice == 0) return 0; // Free = ưu tiên cao nhất
                            if (ticketPrice <= 50000) return 1; // Rẻ = ưu tiên cao
                            return 2; // Đắt hơn = ưu tiên thấp
                        })
                        .ThenBy(p => ticketPrices.GetValueOrDefault(p.Id, 0)) // Sắp xếp theo giá tăng dần
                        .ThenByDescending(p => p.Rating) // Sau đó ưu tiên rating cao
                        .ToList();

                case BudgetPlanType.Balanced:
                    // 🎯 CÂN BẰNG: Địa điểm giá vé trung bình (50k-200k), phổ biến
                    return places
                        .OrderBy(p =>
                        {
                            decimal ticketPrice = ticketPrices.GetValueOrDefault(p.Id, 0);
                            // Ưu tiên giá vé trung bình (50k-200k)
                            if (ticketPrice >= 50000 && ticketPrice <= 200000) return 0; // Trung bình = ưu tiên cao nhất
                            if (ticketPrice < 50000) return 1; // Rẻ = ưu tiên trung bình
                            return 2; // Đắt = ưu tiên thấp
                        })
                        .ThenByDescending(p => p.Rating) // Ưu tiên rating cao
                        .ThenBy(p => Math.Abs(ticketPrices.GetValueOrDefault(p.Id, 0) - 100000)) // Gần 100k (trung bình)
                        .ToList();

                case BudgetPlanType.Premium:
                    // 🌟 CAO CẤP: Địa điểm giá vé cao (200k+), đắt tiền
                    return places
                        .OrderByDescending(p => ticketPrices.GetValueOrDefault(p.Id, 0)) // Ưu tiên giá vé cao
                        .ThenByDescending(p => p.Rating) // Sau đó ưu tiên rating cao
                        .ToList();

                default:
                    return places;
            }
        }


        // ============= MULTIPLE-CHOICE KNAPSACK =============
        /// <summary>
        /// Multiple-Choice Knapsack: Chọn tổ hợp khách sạn, điểm tham quan và 
        /// chi phí di chuyển đảm bảo tối ưu trong ngân sách
        /// </summary>
        public class OptimizedTripPlan
        {
            public List<TouristPlace> SelectedPlaces { get; set; } = new List<TouristPlace>();
            public List<Hotel> SelectedHotels { get; set; } = new List<Hotel>();
            public decimal TotalCost { get; set; }
            public decimal RemainingBudget { get; set; }
            public int Days { get; set; }
            public double TotalDistance { get; set; }
            public int TotalTimeMinutes { get; set; } // ✅ MỚI: Tổng thời gian
        }

        /// <summary>
        /// ✅ MỚI: Class lưu thông tin đa chiều cho mỗi địa điểm
        /// - Value (v_i): Điểm hấp dẫn (Score)
        /// - Weight1 (w_1i): Chi phí tiền (Vé + Ăn)
        /// - Weight2 (w_2i): Thời gian tiêu tốn (Tham quan + Di chuyển)
        /// </summary>
        public class PlaceInfo
        {
            public TouristPlace Place { get; set; }
            public double Score { get; set; } // v_i: Điểm hấp dẫn
            public decimal Cost { get; set; } // w_1i: Chi phí tiền
            public int DurationMinutes { get; set; } // w_2i: Thời gian (phút)

            public PlaceInfo(TouristPlace place, double score, decimal cost, int durationMinutes)
            {
                Place = place;
                Score = score;
                Cost = cost;
                DurationMinutes = durationMinutes;
            }
        }

        /// <summary>
        /// Giải bài toán Multiple-Choice Knapsack để tìm tổ hợp tối ưu
        /// ✅ GIẢI PHÁP MỚI: Thử nhiều tổ hợp địa điểm, chọn tổ hợp tối ưu
        /// </summary>
        private List<OptimizedTripPlan> SolveMultipleChoiceKnapsack(
            decimal budget,
            int days,
            List<TouristPlace> candidatePlaces,
            List<Hotel> candidateHotels,
            TransportOption transport,
            double? startLat = null,
            double? startLng = null,
            int? maxPlacesLimit = null,
            decimal? preCalculatedTransportCost = null)
        {
            var solutions = new List<OptimizedTripPlan>();

            if (!candidatePlaces.Any() || budget <= 0 || days <= 0)
                return solutions;

            int nights = Math.Max(0, days - 1);

            // ✅ Sử dụng RecommendationService
            var rankedPlaces = _recommendationService.RankPlaces(candidatePlaces, null, startLat, startLng);

            // ✅ THAY ĐỔI QUAN TRỌNG: Thử từ NHIỀU → ÍT địa điểm
            // Mục tiêu: Tìm tổ hợp NHIỀU địa điểm NHẤT mà vẫn đủ tiền

            int minPlaces = 1; // Tối thiểu 1 địa điểm
            int maxPlaces;

            if (maxPlacesLimit.HasValue && maxPlacesLimit.Value > 0)
            {
                // Người dùng chọn địa điểm cụ thể
                maxPlaces = maxPlacesLimit.Value;
            }
            else
            {
                // Tự động: Thử tối đa days * 2 (ví dụ: 6 ngày → thử tối đa 12 địa điểm)
                maxPlaces = Math.Min(days * 2, candidatePlaces.Count);
                maxPlaces = Math.Min(maxPlaces, 20); // Hard limit: 20 địa điểm
            }

            // ✅ THỬ TỪ NHIỀU → ÍT ĐỊA ĐIỂM (tham lam)
            // Bắt đầu từ số địa điểm nhiều nhất, giảm dần đến khi tìm được tổ hợp phù hợp
            for (int placeCount = maxPlaces; placeCount >= minPlaces; placeCount--)
            {
                // ✅ THỬ 3 CHIẾN LƯỢC KHÁC NHAU
                var strategies = new List<List<TouristPlace>>();

                // Chiến lược 1: Top theo score (chất lượng cao)
                var strategy1 = rankedPlaces.Take(placeCount).ToList();
                if (strategy1.Count == placeCount)
                    strategies.Add(strategy1);

                // Chiến lược 2: Gần vị trí xuất phát (tiết kiệm di chuyển)
                var strategy2 = rankedPlaces
                    .OrderBy(p => _recommendationService.GetDistance(startLat ?? 11.94, startLng ?? 108.46,
                                              p.Latitude, p.Longitude))
                    .Take(placeCount)
                    .ToList();
                if (strategy2.Count == placeCount && !_recommendationService.AreSamePlaces(strategy1, strategy2))
                    strategies.Add(strategy2);

                // Chiến lược 3: Cân bằng (top 2x rồi lọc gần)
                if (placeCount * 2 <= rankedPlaces.Count)
                {
                    var strategy3 = rankedPlaces
                        .Take(placeCount * 2)
                        .OrderBy(p => _recommendationService.GetDistance(startLat ?? 11.94, startLng ?? 108.46,
                                                  p.Latitude, p.Longitude))
                        .Take(placeCount)
                        .ToList();
                    if (strategy3.Count == placeCount &&
                        !_recommendationService.AreSamePlaces(strategy1, strategy3) &&
                        !_recommendationService.AreSamePlaces(strategy2, strategy3))
                        strategies.Add(strategy3);
                }

                // ✅ TÍNH TOÁN CHO TỪNG CHIẾN LƯỢC
                foreach (var selectedPlaces in strategies)
                {
                    try
                    {
                        var result = CalculateTripCostForPlaces(
                            selectedPlaces, budget, days, nights, transport,
                            candidateHotels, preCalculatedTransportCost,
                            startLat, startLng);

                        if (result != null && result.TotalCost <= budget)
                        {
                            solutions.Add(result);
                        }
                    }
                    catch (Exception)
                    {
                        // Bỏ qua lỗi, thử tổ hợp khác
                        continue;
                    }
                }

                // ✅ NẾU TÌM ĐƯỢC GIẢI PHÁP, DỪNG LẠI (ưu tiên nhiều địa điểm)
                if (solutions.Any())
                {
                    break; // Đã tìm được tổ hợp với số địa điểm tối đa
                }
            }

            // ✅ TRẢ VỀ TOP 5 KẾT QUẢ TỐI ƯU
            return solutions
                .OrderByDescending(s => s.SelectedPlaces.Count) // Nhiều địa điểm nhất
                .ThenBy(s => s.TotalCost)                       // Rẻ nhất
                .Take(5)
                .ToList();
        }

        /// <summary>
        /// ✅ NÂNG CẤP: Giải bài toán Multiple-Choice Knapsack bằng Dynamic Programming (2 chiều)
        /// Sử dụng DP trên Thời gian và kiểm tra điều kiện Ngân sách
        /// </summary>
        private List<OptimizedTripPlan> SolveMultipleChoiceKnapsackDP(
            decimal budget,
            int days,
            List<TouristPlace> candidatePlaces,
            List<Hotel> candidateHotels,
            TransportOption transport,
            double? startLat = null,
            double? startLng = null,
            int? maxPlacesLimit = null,
            decimal? preCalculatedTransportCost = null,
            TravelPace pace = TravelPace.Moderate)
        {
            var solutions = new List<OptimizedTripPlan>();

            if (!candidatePlaces.Any() || budget <= 0 || days <= 0)
                return solutions;

            int nights = Math.Max(0, days - 1);

            // Rank địa điểm để có điểm hấp dẫn
            // ✅ Sử dụng RecommendationService
            var rankedPlaces = _recommendationService.RankPlaces(candidatePlaces, null, startLat, startLng);

            // ✅ TÍNH TOÁN ĐỘNG: Xác định số giờ chơi mỗi ngày dựa trên lựa chọn của người dùng
            // Thay vì hardcode 10 tiếng, giờ sẽ tính dựa trên TravelPace
            int hoursPerDay = pace switch
            {
                TravelPace.Relaxed => 7,   // Đi ít, nghỉ nhiều (người già, trẻ em)
                TravelPace.Fast => 13,      // Đi từ sáng sớm đến tối mịt (thanh niên)
                _ => 10                    // Mặc định (Moderate) - Bình thường
            };
            int totalTimeMinutes = days * hoursPerDay * 60; // Tổng thời gian (phút)

            // Trừ chi phí vận chuyển khỏi ngân sách
            decimal transportCost = preCalculatedTransportCost ??
                (transport.FixedPrice > 0 ? transport.FixedPrice :
                (transport.Price > 0 ? transport.Price : 300000));

            decimal remainingBudget = budget - transportCost;
            if (remainingBudget <= 0) return solutions;

            // Tạo danh sách PlaceInfo với đầy đủ thông tin đa chiều
            var placeInfoList = CreatePlaceInfoList(rankedPlaces, rankedPlaces, startLat, startLng);

            // Giới hạn số lượng địa điểm để tối ưu hiệu năng
            int maxCandidates = Math.Min(rankedPlaces.Count, 50); // Tối đa 50 địa điểm
            var candidates = placeInfoList.Take(maxCandidates).ToList();

            // Sử dụng DP để tìm tổ hợp tối ưu
            var selectedPlaceInfo = OptimizeKnapsackDP(candidates, totalTimeMinutes, remainingBudget);

            if (!selectedPlaceInfo.Any())
                return solutions;

            // Chuyển PlaceInfo về TouristPlace
            var selectedPlaces = selectedPlaceInfo.Select(pi => pi.Place).ToList();

            // Tính toán chi phí và tạo OptimizedTripPlan
            try
            {
                var result = CalculateTripCostForPlaces(
                    selectedPlaces, budget, days, nights, transport,
                    candidateHotels, preCalculatedTransportCost,
                    startLat, startLng);

                if (result != null && result.TotalCost <= budget)
                {
                    // Tính tổng thời gian
                    result.TotalTimeMinutes = selectedPlaceInfo.Sum(pi => pi.DurationMinutes);
                    solutions.Add(result);
                }
            }
            catch (Exception)
            {
                // Bỏ qua lỗi
            }

            return solutions;
        }


        // ============= NÂNG CẤP: TÍNH THỜI GIAN CHO ĐỊA ĐIỂM =============
        /// <summary>
        /// ✅ MỚI: Tính thời gian tiêu tốn cho mỗi địa điểm (phút)
        /// Bao gồm: Thời gian tham quan + Thời gian di chuyển trung bình
        /// </summary>
        private int EstimatePlaceDuration(TouristPlace place, double? previousLat = null, double? previousLng = null)
        {
            int durationMinutes = 0;

            // 1. Thời gian tham quan cơ bản (ước tính dựa trên loại địa điểm)
            // Địa điểm tham quan: 60-120 phút
            // Địa điểm vui chơi: 120-180 phút
            // Địa điểm nghỉ dưỡng: 180-240 phút
            int baseVisitTime = 90; // Mặc định 90 phút (1.5 giờ)

            // Có thể điều chỉnh dựa trên rating hoặc số lượng reviews
            if (place.Rating >= 4.5)
                baseVisitTime = 120; // Địa điểm tốt → ở lâu hơn
            else if (place.Rating >= 4.0)
                baseVisitTime = 90;
            else
                baseVisitTime = 60;

            durationMinutes += baseVisitTime;

            // 2. Thời gian di chuyển từ địa điểm trước (nếu có)
            if (previousLat.HasValue && previousLng.HasValue)
            {
                double distanceKm = _recommendationService.GetDistance(
                    previousLat.Value, previousLng.Value,
                    place.Latitude, place.Longitude
                );

                // Ước tính tốc độ trung bình: 40 km/h trong thành phố
                // Thời gian di chuyển = (khoảng cách / tốc độ) * 60 phút
                int travelTime = (int)((distanceKm / 40.0) * 60);
                durationMinutes += travelTime;
            }
            else
            {
                // Nếu là địa điểm đầu tiên, thêm 30 phút để đến từ điểm xuất phát
                durationMinutes += 30;
            }

            return durationMinutes;
        }

        /// <summary>
        /// ✅ MỚI: Tạo danh sách PlaceInfo từ TouristPlace với đầy đủ thông tin đa chiều
        /// </summary>
        private List<PlaceInfo> CreatePlaceInfoList(
            List<TouristPlace> places,
            List<TouristPlace> rankedPlaces,
            double? startLat = null,
            double? startLng = null)
        {
            var placeInfoList = new List<PlaceInfo>();
            var placeScores = rankedPlaces
                .Select((p, index) => new { Place = p, Score = 100.0 - index })
                .ToDictionary(x => x.Place.Id, x => x.Score);

            double? previousLat = startLat;
            double? previousLng = startLng;

            foreach (var place in places)
            {
                // Tính điểm hấp dẫn (từ ranking)
                double score = placeScores.GetValueOrDefault(place.Id, 50.0);

                // Tính chi phí
                decimal cost = _recommendationService.EstimatePlaceCost(place, 1);

                // Tính thời gian
                int durationMinutes = EstimatePlaceDuration(place, previousLat, previousLng);

                placeInfoList.Add(new PlaceInfo(place, score, cost, durationMinutes));

                // Cập nhật vị trí cho địa điểm tiếp theo
                previousLat = place.Latitude;
                previousLng = place.Longitude;
            }

            return placeInfoList;
        }

        // ============= NÂNG CẤP: DYNAMIC PROGRAMMING KNAPSACK (2 CHIỀU) =============
        /// <summary>
        /// ✅ MỚI: Giải bài toán Knapsack 2 chiều bằng Dynamic Programming
        /// Dung tích 1 (W1): Ngân sách (tiền)
        /// Dung tích 2 (W2): Tổng thời gian rảnh (phút)
        /// 
        /// Sử dụng DP trên Thời gian (vì số phút trong ngày khá nhỏ)
        /// và kiểm tra điều kiện Ngân sách kèm theo
        /// </summary>
        private List<PlaceInfo> OptimizeKnapsackDP(
            List<PlaceInfo> candidates,
            int maxTimeMinutes,
            decimal maxMoney)
        {
            if (!candidates.Any() || maxTimeMinutes <= 0 || maxMoney <= 0)
                return new List<PlaceInfo>();

            int n = candidates.Count;

            // Bảng DP: dp[t] = tổng điểm cao nhất có thể đạt được với thời gian t
            double[] dp = new double[maxTimeMinutes + 1];

            // Để truy vết lại các địa điểm đã chọn
            List<PlaceInfo>[] selection = new List<PlaceInfo>[maxTimeMinutes + 1];
            for (int i = 0; i <= maxTimeMinutes; i++)
            {
                selection[i] = new List<PlaceInfo>();
            }

            // Duyệt qua từng địa điểm
            foreach (var item in candidates)
            {
                // Duyệt ngược từ maxTime về thời gian của item (để tránh chọn 1 item nhiều lần)
                for (int t = maxTimeMinutes; t >= item.DurationMinutes; t--)
                {
                    // Nếu chọn item này mà điểm cao hơn trạng thái cũ
                    double newScore = dp[t - item.DurationMinutes] + item.Score;

                    if (newScore > dp[t])
                    {
                        // Kiểm tra thêm điều kiện Ngân sách
                        decimal currentCost = selection[t - item.DurationMinutes].Sum(p => p.Cost);
                        if (currentCost + item.Cost <= maxMoney)
                        {
                            dp[t] = newScore;
                            // Cập nhật danh sách chọn
                            selection[t] = new List<PlaceInfo>(selection[t - item.DurationMinutes]);
                            selection[t].Add(item);
                        }
                    }
                }
            }

            // Kết quả là danh sách tại selection có dp[t] cao nhất
            int bestTime = 0;
            double bestScore = 0;
            for (int t = 0; t <= maxTimeMinutes; t++)
            {
                if (dp[t] > bestScore)
                {
                    bestScore = dp[t];
                    bestTime = t;
                }
            }

            return selection[bestTime];
        }


        // ✅ HÀM HELPER: Tách logic tính toán chi phí
        private OptimizedTripPlan CalculateTripCostForPlaces(
            List<TouristPlace> selectedPlaces,
            decimal budget,
            int days,
            int nights,
            TransportOption transport,
            List<Hotel> candidateHotels,
            decimal? preCalculatedTransportCost,
            double? startLat,
            double? startLng)
        {
            // ✅ Sử dụng ItineraryService và CostCalculationService
            var clusters = _itineraryService.ClusterPlacesByDistance(selectedPlaces, days);
            var selectedHotels = new List<Hotel>();
            var placeIds = selectedPlaces.Select(p => p.Id).ToList();

            // 1. Chi phí vận chuyển xa - Trừ thẳng (Real Cost)
            decimal transportCost = preCalculatedTransportCost ??
                (transport.FixedPrice > 0 ? transport.FixedPrice :
                (transport.Price > 0 ? transport.Price : 300000));

            // Cảnh báo nếu vận chuyển > 40% tổng ngân sách
            // (Cảnh báo sẽ được xử lý ở nơi gọi hàm này)

            decimal remainingBudget = budget - transportCost;
            if (remainingBudget <= 0) return null;

            // 2. Chi phí vé tham quan - Trừ thẳng (Real Cost)
            var ticketCalc = _costCalculationService.CalculateTicketCosts(placeIds);
            decimal ticketCost = ticketCalc.TotalCost;

            remainingBudget -= ticketCost;
            if (remainingBudget <= 0) return null;

            // 3. Chi phí di chuyển nội thành - Trừ thẳng (Estimated)
            // Ước tính: 150k-300k/ngày, lấy trung bình 225k/ngày
            decimal localTransportCost = days * 225000m; // Ước tính trung bình

            // Nếu có candidateHotels, tính chính xác hơn
            var primaryHotel = candidateHotels.FirstOrDefault();
            if (primaryHotel != null && selectedPlaces.Any())
            {
                // Ước tính dựa trên số địa điểm và ngày
                var estimatedLocalTransportCalc = CalculateLocalTransportCosts(
                    placeIds,
                    days,
                    primaryHotel,
                    transport);
                if (estimatedLocalTransportCalc.TotalCost > 0)
                {
                    localTransportCost = estimatedLocalTransportCalc.TotalCost;
                }
            }

            remainingBudget -= localTransportCost;
            if (remainingBudget <= 0) return null;

            // 4. Chi phí phát sinh - Giữ lại 10% Tổng ngân sách
            decimal miscCost = budget * 0.1m;

            remainingBudget -= miscCost;
            if (remainingBudget <= 0) return null;

            // 5. Chi phí khách sạn - Trần 40% - 45% (của số dư còn lại)
            // Sử dụng 42.5% làm trung bình (giữa 40% và 45%)
            decimal hotelBudget = remainingBudget * 0.425m;
            var hotelCalc = _costCalculationService.CalculateOptimizedHotelCosts(
                placeIds, hotelBudget, days, clusters);
            selectedHotels = hotelCalc.SelectedHotels;
            decimal hotelCost = hotelCalc.TotalCost;

            // Đảm bảo không vượt quá 45% số dư còn lại
            decimal maxHotelCost = remainingBudget * 0.45m;
            if (hotelCost > maxHotelCost)
            {
                hotelCost = maxHotelCost;
                // Điều chỉnh lại hotel selection nếu cần
            }

            remainingBudget -= hotelCost;
            if (remainingBudget <= 0) return null;

            // Tính lại chi phí di chuyển nội thành chính xác hơn sau khi có hotel
            if (selectedHotels.Any() && selectedPlaces.Any())
            {
                var finalHotel = selectedHotels.FirstOrDefault();
                if (finalHotel != null)
                {
                    var accurateLocalTransportCalc = CalculateLocalTransportCosts(
                        placeIds,
                        days,
                        finalHotel,
                        transport);
                    if (accurateLocalTransportCalc.TotalCost > 0)
                    {
                        // Điều chỉnh lại remainingBudget nếu có thay đổi
                        decimal oldLocalTransportCost = localTransportCost;
                        localTransportCost = accurateLocalTransportCalc.TotalCost;

                        // Điều chỉnh lại remainingBudget
                        remainingBudget += (oldLocalTransportCost - localTransportCost);
                        if (remainingBudget <= 0) return null;
                    }
                }
            }

            // 6. Chi phí ăn uống - 100% (Phần còn lại)
            // "Thừa thiếu bù trừ" dồn hết vào đây để ăn ngon
            decimal foodBudget = remainingBudget; // 100% phần còn lại
            var foodCalc = CalculateOptimizedFoodCosts(
                placeIds,
                foodBudget,
                days);
            decimal foodCost = foodCalc.TotalCost;

            // Tính tổng chi phí cuối cùng
            decimal totalCost = transportCost + ticketCost + localTransportCost +
                               miscCost + hotelCost + foodCost;

            if (totalCost > budget) return null;

            // 7. Tính tổng khoảng cách
            double totalDistance = 0;
            if (selectedPlaces.Count > 1)
            {
                double currentLat = startLat ?? 11.940419;
                double currentLng = startLng ?? 108.458313;
                foreach (var place in selectedPlaces)
                {
                    totalDistance += _recommendationService.GetDistance(currentLat, currentLng,
                                                 place.Latitude, place.Longitude);
                    currentLat = place.Latitude;
                    currentLng = place.Longitude;
                }
            }

            return new OptimizedTripPlan
            {
                SelectedPlaces = selectedPlaces,
                SelectedHotels = selectedHotels,
                TotalCost = totalCost,
                RemainingBudget = budget - totalCost,
                Days = days,
                TotalDistance = totalDistance
            };
        }

        // ============================================================================
        // 🔴 FIX #2: KIỂM TRA HỆ THỐNG CÓ DỮ LIỆU
        // ============================================================================
        private async Task<bool> ValidateSystemData()
        {
            var hasPlaces = await _context.TouristPlaces.AnyAsync();
            var hasHotels = await _context.Hotels.AnyAsync();
            var hasRestaurants = await _context.Restaurants.AnyAsync();
            var hasTransports = await _context.TransportOptions.AnyAsync();
            return hasPlaces && hasHotels && hasRestaurants && hasTransports;
        }

        private async Task<IActionResult> ReturnViewWithDefaults(TripPlannerViewModel model)
        {
            model.Categories = await _context.Categories.AsNoTracking().ToListAsync();
            model.TouristPlaces = await _context.TouristPlaces.AsNoTracking().ToListAsync();
            model.TransportOptions = await _context.TransportOptions.AsNoTracking().ToListAsync();

            model.TransportSelectList = model.TransportOptions
                .Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = $"{t.Name} ({t.Type})"
                })
                .ToList();
            return View(model);
        }

        // ============================================================================
        // 🔴 FIX #3: TÍNH NGÂN SÁCH TỐI THIỂU - CẢNH BÁO SỚM
        // ============================================================================
        private decimal CalculateMinimumBudget(
            List<string> selectedPlaceIds,
            int days,
            TransportOption transport,
            decimal transportCost)
        {
            if (days <= 0) days = 1;
            int nights = Math.Max(0, days - 1);

            // Chi phí tối thiểu cho mỗi thành phần
            decimal minTransportCost = transportCost;

            // Khách sạn rẻ nhất
            decimal minHotelPricePerNight = _context.Hotels
                .AsNoTracking()
                .Min(h => (decimal?)h.PricePerNight) ?? 200000;
            decimal minHotelCost = minHotelPricePerNight * nights;

            // Ăn uống tối thiểu: 100k/ngày (3 bữa)
            decimal minFoodCost = 100000 * days;

            // Vé tham quan
            decimal minTicketCost = 0;
            if (selectedPlaceIds.Any())
            {
                var ticketCosts = _context.Attractions
                    .AsNoTracking()
                    .Where(a => selectedPlaceIds.Contains(a.TouristPlaceId))
                    .GroupBy(a => a.TouristPlaceId)
                    .Select(g => g.Sum(a => a.TicketPrice))
                    .ToList();

                minTicketCost = ticketCosts.Any() ? ticketCosts.Sum() : 0;
            }

            // Di chuyển nội thành: 50k/ngày
            decimal minLocalTransport = 50000 * days;

            // Phát sinh 10%
            decimal subtotal = minTransportCost + minHotelCost + minFoodCost +
                              minTicketCost + minLocalTransport;
            decimal minMiscCost = subtotal * 0.1m;

            return subtotal + minMiscCost;
        }

        // ============================================================================
        // 🔴 FIX #4: WRAPPER CHO GenerateSmartSuggestions VỚI VALIDATION
        // ============================================================================
        private async Task<(List<string> suggestions, string autoFillMessage)> GenerateSmartSuggestionsWithValidation(
            TripPlannerViewModel model,
            List<string> selectedPlaceIds,
            Dictionary<int, decimal> transportCostMap)
        {
            string autoFillMessage = null;
            int actualDays = model.NumberOfDays > 0
                ? model.NumberOfDays
                : CalculateRecommendedDays(selectedPlaceIds.Count);

            // ✅ MỚI: Logic tự động thêm địa điểm "Must-go" nếu cần
            // Logic: 1 địa điểm = 0.5 ngày (1 buổi)
            // Nếu số địa điểm chọn < actualDays * 2, tự động thêm các địa điểm Must-go

            // ✅ FIX: Tính transport cost và kiểm tra trước
            bool isFromOtherCity = !(model.StartLocation?.Contains("Đà Lạt",
                StringComparison.OrdinalIgnoreCase) ?? false);

            var mainTransports = model.TransportOptions
                .Where(t => isFromOtherCity
                    ? !t.Name.Contains("Taxi nội thành")
                    : t.Name.Contains("Taxi nội thành") || t.IsSelfDrive)
                .ToList();

            if (model.SelectedTransportId.HasValue)
            {
                mainTransports = mainTransports
                    .Where(t => t.Id == model.SelectedTransportId.Value)
                    .ToList();
            }

            if (!mainTransports.Any())
            {
                string reason = model.SelectedTransportId.HasValue
                    ? "Phương tiện bạn chọn không phù hợp với vị trí xuất phát này."
                    : "Không tìm thấy phương tiện nào trong hệ thống.";

                return (new List<string> {
                    $"⚠️ <strong>{reason}</strong><br/><br/>" +
                    $"💡 <strong>Gợi ý:</strong><br/>" +
                    $"- Kiểm tra lại vị trí xuất phát: <strong>{model.StartLocation}</strong><br/>" +
                    $"- Thử bỏ chọn phương tiện để xem tất cả tùy chọn<br/>" +
                    $"- Hoặc liên hệ hỗ trợ nếu vấn đề vẫn tiếp diễn"
                }, null);
            }

            // ✅ FIX: Kiểm tra chi phí vận chuyển
            var primaryTransport = mainTransports.First();
            decimal transportCost = 0;
            if (transportCostMap != null &&
                transportCostMap.TryGetValue(primaryTransport.Id, out var cost))
            {
                transportCost = cost;
            }
            else
            {
                transportCost = primaryTransport.FixedPrice > 0
                    ? primaryTransport.FixedPrice
                    : (primaryTransport.Price > 0 ? primaryTransport.Price : 300000);
            }

            // ✅ FIX: Cảnh báo nếu transport cost > 50% budget
            if (transportCost > model.Budget * 0.5m)
            {
                decimal recommendedBudget = transportCost * 3; // Transport = 33% budget

                return (new List<string> {
                    $"⚠️ <strong>Chi phí vận chuyển quá cao</strong><br/>" +
                    $"• Chi phí vận chuyển: <strong>{FormatCurrency(transportCost)}</strong> " +
                    $"({(transportCost/model.Budget):P0} ngân sách)<br/>" +
                    $"• Ngân sách của bạn: <strong>{FormatCurrency(model.Budget)}</strong><br/>" +
                    $"• Ngân sách còn lại: <strong>{FormatCurrency(model.Budget - transportCost)}</strong><br/><br/>" +
                    $"💡 <strong>Gợi ý:</strong><br/>" +
                    $"- Tăng ngân sách lên <strong>{FormatCurrency(recommendedBudget)}</strong> " +
                    $"để có chuyến đi đầy đủ<br/>" +
                    $"- Hoặc xuất phát từ Đà Lạt (tiết kiệm chi phí vận chuyển)<br/>" +
                    $"- Hoặc chọn phương tiện rẻ hơn " +
                    $"(xe khách {FormatCurrency(transportCost * 0.5m)} thay vì {primaryTransport.Name})"
                }, null);
            }

            // ✅ FIX: Kiểm tra ngân sách tối thiểu
            decimal minBudget = CalculateMinimumBudget(
                selectedPlaceIds, actualDays, primaryTransport, transportCost
            );

            if (model.Budget < minBudget * 0.7m) // < 70% ngân sách tối thiểu
            {
                return (new List<string> {
                    $"⚠️ <strong>Ngân sách quá thấp</strong><br/>" +
                    $"• Ngân sách của bạn: <strong>{FormatCurrency(model.Budget)}</strong><br/>" +
                    $"• Ngân sách tối thiểu cần: <strong>{FormatCurrency(minBudget)}</strong><br/>" +
                    $"• Thiếu: <strong>{FormatCurrency(minBudget - model.Budget)}</strong><br/><br/>" +
                    $"💡 <strong>Gợi ý:</strong><br/>" +
                    $"- Tăng ngân sách lên <strong>{FormatCurrency(minBudget)}</strong><br/>" +
                    $"- Hoặc giảm xuống còn <strong>{Math.Max(1, actualDays - 2)} ngày</strong><br/>" +
                    $"- Hoặc chọn <strong>{Math.Max(1, selectedPlaceIds.Count - 2)} địa điểm</strong><br/>" +
                    $"- Hoặc xuất phát từ Đà Lạt để tiết kiệm chi phí vận chuyển"
                }, null);
            }

            // ✅ Nếu pass tất cả validation, gọi hàm tính toán chính
            return await GenerateSmartSuggestionsAsync(model, selectedPlaceIds, transportCostMap);
        }

        /// <summary>
        /// Tích hợp Recall, Rank và Knapsack vào quy trình gợi ý
        /// </summary>
        private async Task<(List<string> suggestions, string autoFillMessage)> GenerateSmartSuggestionsAsync(
            TripPlannerViewModel model,
            List<string> selectedPlaceIds,
            Dictionary<int, decimal> preCalculatedTransportCosts = null)
        {
            string autoFillMessage = null;
            var suggestions = new List<string>();
            decimal originalBudget = model.Budget;

            if (originalBudget <= 0 || string.IsNullOrEmpty(model.StartLocation))
                return (new List<string> { "⚠️ Vui lòng nhập ngân sách và vị trí bắt đầu hợp lệ." }, null);

            int actualDays = model.NumberOfDays > 0 ? model.NumberOfDays : CalculateRecommendedDays(selectedPlaceIds.Count);

            // ✅ MỚI: Tính số địa điểm cần thiết (1 địa điểm = 0.5 ngày)
            int requiredPlaces = actualDays * 2;

            // Xác định chiến lược chọn địa điểm
            bool hasUserSelectedPlaces = selectedPlaceIds != null && selectedPlaceIds.Any();
            bool hasMaxPlacesLimit = model.MaxPlaces.HasValue && model.MaxPlaces.Value > 0;

            List<TouristPlace> finalCandidatePlaces;
            List<TouristPlace> rankedPlaces;
            int? maxPlacesLimit = null;

            if (hasUserSelectedPlaces)
            {
                // TRƯỜNG HỢP 1: Người dùng đã chọn địa điểm cụ thể
                // ✅ Sử dụng RecommendationService
                var candidatePlaces = _recommendationService.RecallPlaces(
                    budget: originalBudget,
                    days: actualDays,
                    categoryId: model.SelectedCategoryId,
                    startLat: model.StartLatitude,
                    startLng: model.StartLongitude,
                    maxDistanceKm: 50,
                    minRating: 3
                );

                var userSelectedPlaces = candidatePlaces
                    .Where(p => selectedPlaceIds.Contains(p.Id))
                    .ToList();

                if (!userSelectedPlaces.Any())
                {
                    var allPlaces = _context.TouristPlaces
                        .AsNoTracking()
                        .Where(p => selectedPlaceIds.Contains(p.Id))
                        .ToList();
                    userSelectedPlaces = allPlaces;
                }

                if (!userSelectedPlaces.Any())
                {
                    return (new List<string> { "⚠️ Không tìm thấy các địa điểm bạn đã chọn." }, null);
                }

                // ✅ FIX: Chỉ tự động thêm địa điểm khi THIẾU QUÁ NHIỀU
                // Logic mới: 
                // - Nếu số địa điểm chọn >= số ngày → không tự động thêm (1 ngày 1 địa điểm là hợp lý)
                // - Nếu số địa điểm chọn < 50% requiredPlaces VÀ < số ngày → tự động thêm
                // Ví dụ: 1 ngày, 1 địa điểm → không thêm (1 >= 1)
                // Ví dụ: 2 ngày, 1 địa điểm → thêm (1 < 2 và 1 < 50% của 4)
                // Ví dụ: 3 ngày, 2 địa điểm → không thêm (2 >= 2, gần đủ)

                bool shouldAutoAdd = false;

                // Trường hợp 1: Số địa điểm chọn < số ngày VÀ < 50% requiredPlaces → cần thêm
                if (userSelectedPlaces.Count < actualDays && userSelectedPlaces.Count < (requiredPlaces * 0.5))
                {
                    shouldAutoAdd = true;
                }
                // Trường hợp 2: Số địa điểm chọn >= số ngày → đủ hoặc gần đủ, không thêm
                // (1 ngày 1 địa điểm, 2 ngày 2 địa điểm là hợp lý)

                if (shouldAutoAdd)
                {
                    int placesToAdd = requiredPlaces - userSelectedPlaces.Count;
                    // ✅ Sử dụng RecommendationService
                    var mustGoPlaces = _recommendationService.GetMustGoPlaces(
                        count: placesToAdd,
                        categoryId: model.SelectedCategoryId,
                        startLat: model.StartLatitude,
                        startLng: model.StartLongitude,
                        excludePlaceIds: selectedPlaceIds
                    );

                    if (mustGoPlaces.Any())
                    {
                        userSelectedPlaces.AddRange(mustGoPlaces);
                        autoFillMessage = $"💡 Lịch trình {actualDays} ngày còn nhiều chỗ trống, hệ thống đã tự động thêm {mustGoPlaces.Count} điểm 'Must-go' tại Đà Lạt cho bạn: {string.Join(", ", mustGoPlaces.Select(p => p.Name))}";
                    }
                }

                // ✅ Sử dụng RecommendationService
                rankedPlaces = _recommendationService.RankPlaces(
                    userSelectedPlaces,
                    preferredCategoryId: model.SelectedCategoryId,
                    startLat: model.StartLatitude,
                    startLng: model.StartLongitude
                );

                // ✅ FIX: Giới hạn số địa điểm tối đa = requiredPlaces (1 địa điểm = 0.5 ngày)
                // Nếu người dùng chọn nhiều hơn, chỉ lấy requiredPlaces địa điểm đầu tiên (đã được rank)
                if (rankedPlaces.Count > requiredPlaces)
                {
                    var removedPlaces = rankedPlaces.Skip(requiredPlaces).Select(p => p.Name).ToList();
                    rankedPlaces = rankedPlaces.Take(requiredPlaces).ToList();

                    if (string.IsNullOrEmpty(autoFillMessage))
                    {
                        autoFillMessage = $"💡 Lịch trình {actualDays} ngày chỉ phù hợp với tối đa {requiredPlaces} địa điểm (1 địa điểm = 0.5 ngày). Hệ thống đã tự động chọn {requiredPlaces} địa điểm phù hợp nhất từ danh sách bạn đã chọn.";
                    }
                    else
                    {
                        autoFillMessage += $" Lưu ý: Một số địa điểm đã được loại bỏ để phù hợp với {actualDays} ngày (tối đa {requiredPlaces} địa điểm).";
                    }
                }

                maxPlacesLimit = requiredPlaces; // ✅ FIX: Giới hạn theo số ngày, không phải số địa điểm chọn
                finalCandidatePlaces = rankedPlaces;
            }
            else
            {
                // TRƯỜNG HỢP 2: Người dùng chưa chọn địa điểm, hệ thống tự động đề xuất
                // ✅ Sử dụng RecommendationService
                var candidatePlaces = _recommendationService.RecallPlaces(
                    budget: originalBudget,
                    days: actualDays,
                    categoryId: model.SelectedCategoryId,
                    startLat: model.StartLatitude,
                    startLng: model.StartLongitude,
                    maxDistanceKm: 50,
                    minRating: 3
                );

                // ✅ FALLBACK: Nếu vẫn không có địa điểm, lấy top địa điểm phổ biến nhất (không lọc)
                if (!candidatePlaces.Any())
                {
                    // Lấy top địa điểm theo rating (tối ưu: không đếm reviews trong query)
                    var fallbackPlaces = _context.TouristPlaces
                        .AsNoTracking()
                        .AsQueryable();

                    // Vẫn lọc theo category nếu có
                    if (model.SelectedCategoryId.HasValue)
                    {
                        fallbackPlaces = fallbackPlaces.Where(p => p.CategoryId == model.SelectedCategoryId.Value);
                    }

                    candidatePlaces = fallbackPlaces
                        .OrderByDescending(p => p.Rating)
                        .Take(Math.Max(10, actualDays * 3))
                        .ToList();

                    // Nếu vẫn không có (do category filter), lấy bất kỳ địa điểm nào
                    if (!candidatePlaces.Any())
                    {
                        candidatePlaces = _context.TouristPlaces
                            .AsNoTracking()
                            .OrderByDescending(p => p.Rating)
                            .Take(Math.Max(5, actualDays * 2))
                            .ToList();
                    }
                }

                // ✅ Sử dụng RecommendationService
                rankedPlaces = _recommendationService.RankPlaces(
                    candidatePlaces,
                    preferredCategoryId: model.SelectedCategoryId,
                    startLat: model.StartLatitude,
                    startLng: model.StartLongitude
                );

                if (hasMaxPlacesLimit)
                {
                    // ✅ FIX: Giới hạn theo cả MaxPlaces (nếu có) và requiredPlaces
                    int effectiveLimit = Math.Min(model.MaxPlaces.Value, requiredPlaces);
                    maxPlacesLimit = effectiveLimit;
                    finalCandidatePlaces = rankedPlaces.Take(effectiveLimit).ToList();
                }
                else
                {
                    // ✅ FIX: Giới hạn theo requiredPlaces (1 địa điểm = 0.5 ngày)
                    maxPlacesLimit = requiredPlaces;
                    finalCandidatePlaces = rankedPlaces.Take(requiredPlaces).ToList();
                }
            }

            // Kiểm tra đảm bảo có địa điểm để gợi ý
            if (finalCandidatePlaces == null || !finalCandidatePlaces.Any())
            {
                return (new List<string> { "⚠️ Không tìm thấy địa điểm phù hợp. Vui lòng thử lại với ngân sách hoặc tiêu chí khác." }, null);
            }

            // ✅ MỚI: Tính khoảng cách từ vị trí bắt đầu đến điểm đầu tiên được gợi ý
            double? firstPlaceDistanceKm = null;
            if (finalCandidatePlaces.Any() && model.StartLatitude.HasValue && model.StartLongitude.HasValue)
            {
                var firstPlace = finalCandidatePlaces.First();
                firstPlaceDistanceKm = _recommendationService.GetDistance(
                    model.StartLatitude.Value,
                    model.StartLongitude.Value,
                    firstPlace.Latitude,
                    firstPlace.Longitude
                );
            }

            // Lưu số địa điểm ban đầu và sau khi tự động đề xuất để so sánh
            int originalPlaceCount = hasUserSelectedPlaces ? selectedPlaceIds.Count : 0;
            int finalPlaceCountAfterAutoSuggest = finalCandidatePlaces.Count;

            // Lấy danh sách phương tiện phù hợp
            bool isFromOtherCity = !(model.StartLocation?.Contains("Đà Lạt") ?? false);
            var mainTransports = model.TransportOptions
                .Where(t => isFromOtherCity ? !t.Name.Contains("Taxi nội thành")
                                            : t.Name.Contains("Taxi nội thành") || t.IsSelfDrive)
                .ToList();

            if (model.SelectedTransportId.HasValue)
            {
                mainTransports = mainTransports
                    .Where(t => t.Id == model.SelectedTransportId.Value)
                    .ToList();
            }

            if (!mainTransports.Any())
                return (new List<string> { "Không có phương tiện phù hợp với vị trí xuất phát." }, null);

            // ✅ Lấy phương tiện chính (ưu tiên phương tiện đã chọn hoặc phương tiện đầu tiên)
            var primaryTransport = mainTransports.FirstOrDefault();
            if (model.SelectedTransportId.HasValue)
            {
                primaryTransport = mainTransports.FirstOrDefault(t => t.Id == model.SelectedTransportId.Value) ?? primaryTransport;
            }

            // ✅ MỚI: Tạo 3 gợi ý theo mức ngân sách (TIẾT KIỆM, CÂN BẰNG, CAO CẤP)
            var budgetPlans = new[] { BudgetPlanType.Budget, BudgetPlanType.Balanced, BudgetPlanType.Premium };

            foreach (var planType in budgetPlans)
            {
                try
                {
                    // ✅ MỚI: Lọc địa điểm theo mức ngân sách (xoay địa điểm theo giá vé)
                    // Chỉ áp dụng khi hệ thống tự động đề xuất, không áp dụng khi người dùng đã chọn địa điểm
                    List<TouristPlace> placesForKnapsack;
                    if (!hasUserSelectedPlaces)
                    {
                        // Hệ thống tự động đề xuất: Lọc theo mức ngân sách
                        placesForKnapsack = FilterPlacesByBudgetPlan(finalCandidatePlaces, planType).Take(30).ToList();

                        // Nếu không có địa điểm phù hợp, fallback về danh sách gốc
                        if (!placesForKnapsack.Any())
                        {
                            placesForKnapsack = finalCandidatePlaces.Take(30).ToList();
                        }
                    }
                    else
                    {
                        // Người dùng đã chọn địa điểm: Giữ nguyên danh sách đã chọn
                        placesForKnapsack = finalCandidatePlaces.Take(30).ToList();
                    }
                    decimal? transportCostFromGps = null;
                    if (preCalculatedTransportCosts != null &&
                        preCalculatedTransportCosts.TryGetValue(primaryTransport.Id, out var costFromMap))
                    {
                        transportCostFromGps = costFromMap;
                    }

                    OptimizedTripPlan bestPlan;

                    // ✅ SỬA: Nếu người dùng đã chọn địa điểm cụ thể, dùng tất cả các địa điểm đó
                    if (hasUserSelectedPlaces && finalCandidatePlaces.Count <= actualDays * 3)
                    {
                        // Người dùng đã chọn địa điểm: Dùng tất cả các địa điểm đã chọn
                        bestPlan = new OptimizedTripPlan
                        {
                            SelectedPlaces = finalCandidatePlaces,
                            SelectedHotels = new List<Hotel>(),
                            TotalCost = 0,
                            RemainingBudget = 0,
                            Days = actualDays,
                            TotalDistance = 0
                        };
                    }
                    else
                    {
                        // ✅ KHÔNG TÍNH LẠI GPS - DÙNG GIÁ ĐÃ TÍNH TỪ Index()
                        var optimizedPlans = SolveMultipleChoiceKnapsack(
                            budget: originalBudget,
                            days: actualDays,
                            candidatePlaces: placesForKnapsack,
                            candidateHotels: model.Hotels,
                            transport: primaryTransport,
                            startLat: model.StartLatitude,
                            startLng: model.StartLongitude,
                            maxPlacesLimit: maxPlacesLimit,
                            preCalculatedTransportCost: transportCostFromGps
                        );

                        if (!optimizedPlans.Any())
                        {
                            // ✅ FALLBACK: Nếu không tìm được plan, tạo plan cơ bản với ít địa điểm nhất
                            if (placesForKnapsack.Any())
                            {
                                // Tạo plan với 1 địa điểm đầu tiên
                                bestPlan = new OptimizedTripPlan
                                {
                                    SelectedPlaces = placesForKnapsack.Take(1).ToList(),
                                    SelectedHotels = new List<Hotel>(),
                                    TotalCost = 0,
                                    RemainingBudget = 0,
                                    Days = actualDays,
                                    TotalDistance = 0
                                };
                            }
                            else
                            {
                                continue; // Không có địa điểm nào, bỏ qua
                            }
                        }
                        else
                        {
                            // ✅ SỬA: ƯU TIÊN LẤY PLAN CÓ NHIỀU ĐỊA ĐIỂM NHẤT
                            bestPlan = optimizedPlans
                                .OrderByDescending(p => p.SelectedPlaces.Count) // ← ƯU TIÊN NHIỀU ĐỊA ĐIỂM
                                .ThenBy(p => p.TotalCost) // ← NẾU BẰNG NHAU, CHỌN RẺ HƠN
                                .First();
                        }
                    }

                    // ✅ SỬ DỤNG GIÁ ĐÃ TÍNH TỪ GPS (HOẶC MẶC ĐỊNH)
                    decimal transportCost = transportCostFromGps ?? 0;

                    if (transportCost == 0)
                    {
                        // Fallback đơn giản nếu không có giá từ GPS
                        transportCost = primaryTransport.FixedPrice > 0 ? primaryTransport.FixedPrice :
                                       (primaryTransport.Price > 0 ? primaryTransport.Price : 300000);
                    }

                    // ✅ MỚI: Tính toán theo mức ngân sách
                    decimal hotelBudgetPercent = planType == BudgetPlanType.Budget ? 0.30m :
                                                 planType == BudgetPlanType.Balanced ? 0.35m : 0.40m;
                    decimal foodBudgetPercent = planType == BudgetPlanType.Budget ? 0.55m :
                                                planType == BudgetPlanType.Balanced ? 0.60m : 0.65m;

                    var hotelCalc = CalculateOptimizedHotelCostsByBudgetPlan(
                        bestPlan.SelectedPlaces.Select(p => p.Id).ToList(),
                        (originalBudget - transportCost) * hotelBudgetPercent,
                        actualDays,
                        planType);

                    // ✅ MỚI: Tính chi phí ăn uống theo mức ngân sách
                    var foodCalc = CalculateOptimizedFoodCostsByBudgetPlan(
                        bestPlan.SelectedPlaces.Select(p => p.Id).ToList(),
                        (originalBudget - transportCost - hotelCalc.TotalCost) * foodBudgetPercent,
                        actualDays,
                        planType);

                    // ✅ Sử dụng CostCalculationService
                    var ticketCalc = _costCalculationService.CalculateTicketCosts(
                        bestPlan.SelectedPlaces.Select(p => p.Id).ToList());

                    // ✅ FIX: TẠO DailyItinerary TRƯỚC KHI TÍNH CHI PHÍ DI CHUYỂN (ĐỒNG BỘ)
                    List<DailyItinerary> dailyItinerary = null;
                    List<string> distanceWarnings = new List<string>();
                    if (bestPlan.SelectedPlaces != null && bestPlan.SelectedPlaces.Any())
                    {
                        // ✅ Sử dụng ItineraryService
                        var clusters = _itineraryService.ClusterPlacesByDistance(bestPlan.SelectedPlaces, actualDays);
                        var (itinerary, itineraryWarnings) = _itineraryService.BuildDailyItinerary(
                            bestPlan.SelectedPlaces,
                            actualDays,
                            bestPlan.SelectedHotels,
                            clusters);
                        dailyItinerary = itinerary;
                        distanceWarnings = itineraryWarnings;
                    }

                    // ✅ FIX: SỬ DỤNG dailyItinerary ĐỂ TÍNH CHI PHÍ DI CHUYỂN (ĐỒNG BỘ)
                    decimal localTransportCost = 0;
                    var localTransportDetails = new List<string>();

                    if (dailyItinerary != null && dailyItinerary.Any())
                    {
                        // Dùng phiên bản MỚI - đồng bộ với lịch trình
                        var localTransportCalc = CalculateLocalTransportCosts(dailyItinerary, primaryTransport);
                        localTransportCost = localTransportCalc.TotalCost;
                        localTransportDetails = localTransportCalc.Details;
                    }
                    else
                    {
                        // Fallback: dùng phiên bản CŨ nếu không có lịch trình
                        var primaryHotel = bestPlan.SelectedHotels.FirstOrDefault() ?? model.Hotels.FirstOrDefault();
                        if (primaryHotel != null)
                        {
                            var localTransportCalc = CalculateLocalTransportCosts(
                                bestPlan.SelectedPlaces.Select(p => p.Id).ToList(),
                                actualDays,
                                primaryHotel,
                                primaryTransport);
                            localTransportCost = localTransportCalc.TotalCost;
                            localTransportDetails = localTransportCalc.Details;
                        }
                    }

                    decimal miscCost = (transportCost + hotelCalc.TotalCost + foodCalc.TotalCost +
                                      ticketCalc.TotalCost + localTransportCost) * 0.1m;
                    decimal totalCost = transportCost + hotelCalc.TotalCost + foodCalc.TotalCost +
                                       ticketCalc.TotalCost + localTransportCost + miscCost;

                    decimal remaining = originalBudget - totalCost;

                    // Tạo lịch trình tối ưu - ✅ ĐỒNG BỘ với dailyItinerary
                    string routeDetails;
                    if (dailyItinerary != null && dailyItinerary.Any())
                    {
                        // Sử dụng dailyItinerary để đảm bảo đồng bộ với lịch trình thực tế
                        // ✅ Sử dụng ItineraryService
                        routeDetails = _itineraryService.GenerateRouteDetailsFromItinerary(dailyItinerary);
                    }
                    else
                    {
                        // Fallback: sử dụng route đơn giản nếu không có dailyItinerary
                        double startLat = model.StartLatitude ?? 11.940419;
                        double startLng = model.StartLongitude ?? 108.458313;
                        // ✅ Sử dụng ItineraryService
                        var route = _itineraryService.OptimizeRoute(startLat, startLng, bestPlan.SelectedPlaces);
                        routeDetails = string.Join("<br/>", route.Select((p, idx) => $"{idx + 1}. {p.Name}"));
                    }

                    // ✅ REMOVED: Cảnh báo về việc tự động đề xuất thêm địa điểm
                    // LÝ DO: Hệ thống không còn tự động thêm địa điểm khi người dùng đã chọn
                    var allWarnings = new List<string>(ticketCalc.Warnings);
                    allWarnings.AddRange(distanceWarnings);

                    // Tạo cluster details - hiển thị phân bổ thời gian dựa trên địa điểm
                    // ✅ FIX: Tái tạo clusters từ dailyItinerary để đảm bảo đồng bộ
                    string clusterDetails = "";
                    if (dailyItinerary != null && dailyItinerary.Any())
                    {
                        // Tái tạo clusters từ dailyItinerary (đảm bảo đồng bộ) - đổi tên biến để tránh conflict
                        var reconstructedClusters2 = ReconstructClustersFromItinerary(dailyItinerary);

                        if (reconstructedClusters2.Any())
                        {
                            clusterDetails = "<br/><b>📍 Phân bổ thời gian:</b><br/>";

                            // ✅ FIX: Sử dụng số ngày thực tế từ dailyItinerary (đã đồng bộ)
                            var groupedByCluster = dailyItinerary
                                .Where(d => d.ClusterIndex >= 0)
                                .GroupBy(d => d.ClusterIndex)
                                .OrderBy(g => g.Key)
                                .ToList();

                            // ✅ FIX: Tính số đêm chính xác để tổng = tổng số ngày - 1
                            int totalDays = dailyItinerary.Count;
                            int expectedTotalNights = Math.Max(0, totalDays - 1);

                            int phaseNumber = 1;
                            int totalNightsCalculated = 0;

                            foreach (var clusterGroup in groupedByCluster)
                            {
                                var cluster = reconstructedClusters2[clusterGroup.Key];
                                int daysInCluster = clusterGroup.Count();

                                // Tính số đêm cho cluster này
                                int nightsInCluster;
                                bool isLastCluster = clusterGroup.Key == groupedByCluster.Last().Key;

                                if (isLastCluster)
                                {
                                    // Cluster cuối: nhận phần còn lại để tổng đúng
                                    nightsInCluster = Math.Max(0, expectedTotalNights - totalNightsCalculated);
                                }
                                else
                                {
                                    // Các cluster khác: số đêm = số ngày (bao gồm đêm sau ngày cuối trước khi chuyển)
                                    nightsInCluster = daysInCluster;
                                    totalNightsCalculated += nightsInCluster;
                                }

                                // ✅ FIX: Hiển thị số ngày thực tế từ dailyItinerary
                                clusterDetails += $"Giai đoạn {phaseNumber}: {daysInCluster} ngày " +
                                                $"({nightsInCluster} đêm) tại " +
                                                $"{string.Join(", ", cluster.Places.Take(2).Select(p => p.Name))}" +
                                                $"{(cluster.Places.Count > 2 ? "..." : "")}<br/>";
                                phaseNumber++;
                            }
                        }
                    }
                    else if (bestPlan.SelectedPlaces != null && bestPlan.SelectedPlaces.Any())
                    {
                        // ✅ Sử dụng ItineraryService - Fallback: Nếu không có dailyItinerary, tạo clusters
                        var clusters = _itineraryService.ClusterPlacesByDistance(bestPlan.SelectedPlaces, actualDays);
                        if (clusters.Any())
                        {
                            clusterDetails = "<br/><b>📍 Phân bổ thời gian:</b><br/>";

                            var validClusters = clusters.Where(c => c.RecommendedNights >= 0).ToList();
                            int phaseNumber = 1;
                            int totalNights = validClusters.Sum(c => c.RecommendedNights);

                            var daysAllocation = new int[validClusters.Count];
                            int allocatedDays = 0;

                            for (int i = 0; i < validClusters.Count; i++)
                            {
                                var cluster = validClusters[i];
                                int daysToShow;

                                if (i == validClusters.Count - 1)
                                {
                                    daysToShow = actualDays - allocatedDays;
                                    if (daysToShow < 1) daysToShow = 1;
                                }
                                else
                                {
                                    if (totalNights > 0)
                                    {
                                        double ratio = (double)cluster.RecommendedNights / totalNights;
                                        daysToShow = (int)Math.Round(ratio * actualDays);
                                        int daysRemaining = actualDays - allocatedDays - (validClusters.Count - i - 1);
                                        daysToShow = Math.Min(daysToShow, daysRemaining);
                                    }
                                    else
                                    {
                                        daysToShow = actualDays / validClusters.Count;
                                    }
                                    if (daysToShow < 1) daysToShow = 1;
                                }

                                daysAllocation[i] = daysToShow;
                                allocatedDays += daysToShow;
                            }

                            if (allocatedDays != actualDays)
                            {
                                int diff = actualDays - allocatedDays;
                                daysAllocation[validClusters.Count - 1] += diff;
                                if (daysAllocation[validClusters.Count - 1] < 1)
                                {
                                    daysAllocation[validClusters.Count - 1] = 1;
                                }
                            }

                            for (int i = 0; i < validClusters.Count; i++)
                            {
                                var cluster = validClusters[i];
                                int daysToShow = daysAllocation[i];

                                clusterDetails += $"Giai đoạn {phaseNumber}: {daysToShow} ngày " +
                                                $"({cluster.RecommendedNights} đêm) tại " +
                                                $"{string.Join(", ", cluster.Places.Take(2).Select(p => p.Name))}" +
                                                $"{(cluster.Places.Count > 2 ? "..." : "")}<br/>";
                                phaseNumber++;
                            }
                        }
                    }

                    // ✅ MỚI: Format suggestion với tên gợi ý theo mức ngân sách
                    string planName = planType == BudgetPlanType.Budget ? "🔥 TIẾT KIỆM" :
                                      planType == BudgetPlanType.Balanced ? "🎯 CÂN BẰNG" : "🌟 CAO CẤP";

                    string suggestion = FormatOptimizedSuggestionByBudgetPlan(
                        planName, primaryTransport, transportCost,
                        hotelCalc.Details, hotelCalc.TotalCost,
                        foodCalc.Details, foodCalc.TotalCost,
                        ticketCalc.TotalCost, localTransportCost, miscCost,
                        totalCost, remaining, actualDays,
                        ticketCalc.TicketDetails, localTransportDetails, allWarnings,
                        routeDetails, clusterDetails, false,
                        dailyItinerary);

                    suggestions.Add(suggestion);
                }
                catch (Exception ex)
                {
                    string planName = planType == BudgetPlanType.Budget ? "TIẾT KIỆM" :
                                      planType == BudgetPlanType.Balanced ? "CÂN BẰNG" : "CAO CẤP";
                    suggestions.Add($"⚠️ Lỗi tính toán cho gói {planName}: {ex.Message}");
                }
            }

            if (!suggestions.Any())
                return (GenerateBudgetWarning(originalBudget, actualDays, rankedPlaces.Count), autoFillMessage);

            // ✅ MỚI: Loại bỏ duplicate dựa trên mức ngân sách (thay vì phương tiện)
            suggestions = RemoveDuplicateSuggestionsByBudgetPlan(suggestions)
                         .OrderBy(s => ExtractTotalCost(s))
                         .ToList();

            // Đảm bảo có đủ 3 gợi ý (TIẾT KIỆM, CÂN BẰNG, CAO CẤP)
            // Nếu thiếu, giữ lại tất cả các gợi ý có sẵn

            // ✅ MỚI: Lưu khoảng cách đến điểm đầu tiên vào ViewBag (sẽ được set ở Index action)
            // Note: firstPlaceDistanceKm đã được tính ở trên

            return (suggestions, autoFillMessage);
        }

        private string GetLocationNamesDisplay(List<TouristPlace> places)
        {
            if (places.Count <= 2)
            {
                return string.Join(", ", places.Select(p => p.Name));
            }
            else if (places.Count == 3)
            {
                return string.Join(", ", places.Select(p => p.Name));
            }
            else
            {
                var firstTwo = string.Join(", ", places.Take(2).Select(p => p.Name));
                return $"{firstTwo} và {places.Count - 2} địa điểm khác";
            }
        }

        // ============================================================================
        // 🔴 FIX #5: SAFE HOTEL & RESTAURANT SELECTION
        // ============================================================================
        private (decimal TotalCost, string Details, List<Hotel> SelectedHotels)
            CalculateOptimizedHotelCostsSafe(
                List<string> selectedPlaceIds,
                decimal hotelBudget,
                int days)
        {
            int nights = Math.Max(0, days - 1);

            if (nights == 0)
                return (0, "Chuyến đi trong ngày - không cần nghỉ đêm", new List<Hotel>());
            if (hotelBudget <= 0)
                return (0, "Không có ngân sách cho khách sạn", new List<Hotel>());

            // ✅ FIX: Kiểm tra có khách sạn trong database không
            var allHotels = _context.Hotels
                .AsNoTracking()
                .ToList();

            if (!allHotels.Any())
            {
                // Tạo khách sạn ước tính
                decimal estimatedCost = Math.Min(hotelBudget, nights * 300000);
                return (
                    estimatedCost,
                    $"⚠️ Khách sạn (ước tính): {nights} đêm × 300,000đ/đêm<br/>" +
                    $"<em>Hệ thống chưa có dữ liệu khách sạn. Vui lòng tự tìm kiếm.</em>",
                    new List<Hotel>()
                );
            }

            // ✅ Sử dụng CostCalculationService
            var places = _context.TouristPlaces
                .AsNoTracking()
                .Where(p => selectedPlaceIds.Contains(p.Id))
                .ToList();
            var clusters = places.Any() ? _itineraryService.ClusterPlacesByDistance(places, days) : null;
            var result = _costCalculationService.CalculateOptimizedHotelCosts(selectedPlaceIds, hotelBudget, days, clusters);

            // ✅ FIX: Đảm bảo luôn trả về ít nhất 1 khách sạn
            if (!result.SelectedHotels.Any())
            {
                var cheapestHotel = allHotels.OrderBy(h => h.PricePerNight).First();
                result.SelectedHotels.Add(cheapestHotel);

                decimal cost = Math.Min(cheapestHotel.PricePerNight * nights, hotelBudget);
                var detailsList = result.Details.Split(new[] { "<br/>" }, StringSplitOptions.None).ToList();
                detailsList.Add($"• {cheapestHotel.Name} (mặc định): " +
                              $"{nights} đêm × {FormatCurrency(cost / nights)}");
                result = (cost, string.Join("<br/>", detailsList), result.SelectedHotels);
            }

            return result;
        }


    }

}
