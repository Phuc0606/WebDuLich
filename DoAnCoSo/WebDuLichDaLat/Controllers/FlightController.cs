using Microsoft.AspNetCore.Mvc;
using WebDuLichDaLat.Services;
using amadeus.resources;

namespace WebDuLichDaLat.Controllers
{
    /// <summary>
    /// Controller để tìm kiếm và hiển thị chuyến bay
    /// </summary>
    public class FlightController : Controller
    {
        private readonly AmadeusFlightService _flightService;
        private readonly ILogger<FlightController> _logger;

        public FlightController(AmadeusFlightService flightService, ILogger<FlightController> logger)
        {
            _flightService = flightService;
            _logger = logger;
        }

        /// <summary>
        /// Trang tìm kiếm chuyến bay
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Tìm kiếm chuyến bay (POST)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Search(string originCode, string destinationCode, string departureDate, int adults = 1, int maxResults = 5)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(originCode) || string.IsNullOrWhiteSpace(destinationCode) || string.IsNullOrWhiteSpace(departureDate))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin: Sân bay đi, Sân bay đến và Ngày khởi hành";
                return View("Index");
            }

            // Validate date format
            if (!DateTime.TryParse(departureDate, out DateTime departure))
            {
                ViewBag.Error = "Ngày khởi hành không hợp lệ. Vui lòng nhập định dạng: yyyy-MM-dd";
                return View("Index");
            }

            // Validate date is not in the past
            if (departure.Date < DateTime.Today)
            {
                ViewBag.Error = "Ngày khởi hành không thể là ngày trong quá khứ";
                return View("Index");
            }

            try
            {
                // Gọi service để tìm kiếm chuyến bay
                var flightOffers = await _flightService.SearchFlightsAsync(
                    originCode: originCode.ToUpper(),
                    destinationCode: destinationCode.ToUpper(),
                    departureDate: departure.ToString("yyyy-MM-dd"),
                    adults: adults,
                    maxResults: maxResults
                );

                if (flightOffers == null || flightOffers.Length == 0)
                {
                    ViewBag.Error = "Không tìm thấy chuyến bay nào theo tiêu chí này. Vui lòng thử lại với thông tin khác.";
                    ViewBag.OriginCode = originCode;
                    ViewBag.DestinationCode = destinationCode;
                    ViewBag.DepartureDate = departureDate;
                    ViewBag.Adults = adults;
                    return View("Index");
                }

                // Chuyển đổi sang ViewModel để hiển thị
                var flightDetails = flightOffers
                    .Select(offer => _flightService.GetFlightDetails(offer))
                    .Where(details => details != null)
                    .ToList();

                ViewBag.OriginCode = originCode;
                ViewBag.DestinationCode = destinationCode;
                ViewBag.DepartureDate = departureDate;
                ViewBag.Adults = adults;
                ViewBag.ResultsCount = flightDetails.Count;

                return View("Results", flightDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi tìm kiếm chuyến bay: {ex.Message}");
                ViewBag.Error = $"Đã xảy ra lỗi khi tìm kiếm chuyến bay: {ex.Message}";
                ViewBag.OriginCode = originCode;
                ViewBag.DestinationCode = destinationCode;
                ViewBag.DepartureDate = departureDate;
                ViewBag.Adults = adults;
                return View("Index");
            }
        }

        /// <summary>
        /// API endpoint để tìm kiếm chuyến bay (JSON)
        /// </summary>
        [HttpGet]
        [Route("api/flights/search")]
        public async Task<IActionResult> SearchApi(string originCode, string destinationCode, string departureDate, int adults = 1, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(originCode) || string.IsNullOrWhiteSpace(destinationCode) || string.IsNullOrWhiteSpace(departureDate))
            {
                return BadRequest(new { error = "Vui lòng nhập đầy đủ thông tin" });
            }

            try
            {
                var flightOffers = await _flightService.SearchFlightsAsync(
                    originCode: originCode.ToUpper(),
                    destinationCode: destinationCode.ToUpper(),
                    departureDate: departureDate,
                    adults: adults,
                    maxResults: maxResults
                );

                if (flightOffers == null || flightOffers.Length == 0)
                {
                    return Ok(new { results = Array.Empty<object>(), count = 0 });
                }

                var flightDetails = flightOffers
                    .Select(offer => _flightService.GetFlightDetails(offer))
                    .Where(details => details != null)
                    .ToList();

                return Ok(new { results = flightDetails, count = flightDetails.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi API tìm kiếm chuyến bay: {ex.Message}");
                return StatusCode(500, new { error = "Đã xảy ra lỗi khi tìm kiếm chuyến bay" });
            }
        }
    }
}

