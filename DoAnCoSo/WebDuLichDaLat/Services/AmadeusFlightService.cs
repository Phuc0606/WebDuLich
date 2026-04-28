using amadeus;
using amadeus.resources;
using Microsoft.Extensions.Configuration;

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Service để tìm kiếm chuyến bay sử dụng Amadeus API
    /// </summary>
    public class AmadeusFlightService
    {
        private readonly Amadeus _amadeus;
        private readonly ILogger<AmadeusFlightService> _logger;

        public AmadeusFlightService(IConfiguration configuration, ILogger<AmadeusFlightService> logger)
        {
            _logger = logger;

            // Lấy API Key và Secret từ appsettings.json
            string apiKey = configuration["Amadeus:ApiKey"] ?? string.Empty;
            string apiSecret = configuration["Amadeus:ApiSecret"] ?? string.Empty;

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            {
                _logger.LogWarning("Amadeus API credentials chưa được cấu hình trong appsettings.json");
            }

            // Khởi tạo Amadeus Client
            _amadeus = Amadeus
                .builder(apiKey, apiSecret)
                .build();
        }

        /// <summary>
        /// Tìm kiếm chuyến bay
        /// </summary>
        /// <param name="originCode">Mã sân bay đi (VD: HAN, SGN)</param>
        /// <param name="destinationCode">Mã sân bay đến (VD: DLI)</param>
        /// <param name="departureDate">Ngày khởi hành (định dạng: yyyy-MM-dd)</param>
        /// <param name="adults">Số người lớn</param>
        /// <param name="maxResults">Số kết quả tối đa (mặc định: 5)</param>
        /// <returns>Danh sách chuyến bay hoặc null nếu lỗi</returns>
        public async Task<FlightOffer[]?> SearchFlightsAsync(
            string originCode,
            string destinationCode,
            string departureDate,
            int adults = 1,
            int maxResults = 5)
        {
            try
            {
                _logger.LogInformation($"Đang tìm kiếm chuyến bay: {originCode} -> {destinationCode} ngày {departureDate}");

                // Gọi API Amadeus (Flight Offers Search)
                var flightOffers = _amadeus.shopping.flightOffers.get(
                    Params.with("originLocationCode", originCode.ToUpper())
                          .and("destinationLocationCode", destinationCode.ToUpper())
                          .and("departureDate", departureDate)
                          .and("adults", adults.ToString())
                          .and("max", maxResults.ToString()));

                if (flightOffers != null && flightOffers.Length > 0)
                {
                    _logger.LogInformation($"Tìm thấy {flightOffers.Length} chuyến bay");
                    return flightOffers;
                }
                else
                {
                    _logger.LogWarning("Không tìm thấy chuyến bay nào");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi tìm kiếm chuyến bay: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết về một chuyến bay
        /// </summary>
        public FlightOfferDetails? GetFlightDetails(FlightOffer offer)
        {
            if (offer == null || offer.itineraries == null || offer.itineraries.Count == 0)
                return null;

            try
            {
                var itinerary = offer.itineraries[0];
                var segments = itinerary.segments;

                if (segments == null || segments.Count == 0)
                    return null;

                var firstSegment = segments[0];
                var lastSegment = segments[segments.Count - 1];

                return new FlightOfferDetails
                {
                    Price = offer.price?.total ?? "0",
                    Currency = offer.price?.currency ?? "USD",
                    CarrierCode = firstSegment.carrierCode ?? "N/A",
                    DepartureAirport = firstSegment.departure?.iataCode ?? "N/A",
                    ArrivalAirport = lastSegment.arrival?.iataCode ?? "N/A",
                    DepartureTime = firstSegment.departure?.at ?? "N/A",
                    ArrivalTime = lastSegment.arrival?.at ?? "N/A",
                    NumberOfStops = segments.Count - 1,
                    NumberOfBookableSeats = offer.numberOfBookableSeats,
                    Segments = segments.Select(s => new FlightSegmentDetails
                    {
                        CarrierCode = s.carrierCode ?? "N/A",
                        DepartureAirport = s.departure?.iataCode ?? "N/A",
                        ArrivalAirport = s.arrival?.iataCode ?? "N/A",
                        DepartureTime = s.departure?.at ?? "N/A",
                        ArrivalTime = s.arrival?.at ?? "N/A",
                        Duration = s.duration ?? "N/A"
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy chi tiết chuyến bay: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Model để hiển thị thông tin chuyến bay
    /// </summary>
    public class FlightOfferDetails
    {
        public string Price { get; set; } = string.Empty;
        public string Currency { get; set; } = "USD";
        public string CarrierCode { get; set; } = string.Empty;
        public string DepartureAirport { get; set; } = string.Empty;
        public string ArrivalAirport { get; set; } = string.Empty;
        public string DepartureTime { get; set; } = string.Empty;
        public string ArrivalTime { get; set; } = string.Empty;
        public int NumberOfStops { get; set; }
        public int NumberOfBookableSeats { get; set; }
        public List<FlightSegmentDetails> Segments { get; set; } = new();
    }

    /// <summary>
    /// Thông tin chi tiết từng chặng bay
    /// </summary>
    public class FlightSegmentDetails
    {
        public string CarrierCode { get; set; } = string.Empty;
        public string DepartureAirport { get; set; } = string.Empty;
        public string ArrivalAirport { get; set; } = string.Empty;
        public string DepartureTime { get; set; } = string.Empty;
        public string ArrivalTime { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
    }
}

