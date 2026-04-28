using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace WebDuLichDaLat.Services
{
    public class OsrmRouteResult
    {
        public double DistanceMeters { get; set; }
        public double DurationSeconds { get; set; }
        public List<List<double>> Geometry { get; set; } = new List<List<double>>(); // [[lon, lat], ...]
        public List<OsrmLeg> Legs { get; set; } = new List<OsrmLeg>();
    }

    public class OsrmLeg
    {
        public double DistanceMeters { get; set; }
        public double DurationSeconds { get; set; }
        public List<List<double>> Steps { get; set; } = new List<List<double>>(); // [[lon, lat], ...]
    }

    public class OsrmRouteService
    {
        private const string OsrmBaseUrl = "https://router.project-osrm.org";

        /// <summary>
        /// Lấy tuyến đường từ OSRM
        /// </summary>
        /// <param name="pickupLat">Vĩ độ điểm đón</param>
        /// <param name="pickupLon">Kinh độ điểm đón</param>
        /// <param name="dropoffLat">Vĩ độ điểm đến</param>
        /// <param name="dropoffLon">Kinh độ điểm đến</param>
        /// <param name="profile">Loại tuyến: driving, walking, cycling (mặc định: driving)</param>
        /// <returns>Kết quả tuyến đường hoặc null nếu lỗi</returns>
        public async Task<OsrmRouteResult?> GetRouteAsync(
            double pickupLat, double pickupLon,
            double dropoffLat, double dropoffLon,
            string profile = "driving")
        {
            // OSRM yêu cầu format: lon,lat (không phải lat,lon)
            var url = $"{OsrmBaseUrl}/route/v1/{profile}/{pickupLon.ToString(CultureInfo.InvariantCulture)},{pickupLat.ToString(CultureInfo.InvariantCulture)};{dropoffLon.ToString(CultureInfo.InvariantCulture)},{dropoffLat.ToString(CultureInfo.InvariantCulture)}?overview=full&alternatives=false&steps=true&geometries=geojson";

            using var client = new HttpClient();
            try
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var payload = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(payload);

                if (!string.Equals((string?)data["code"], "Ok", StringComparison.OrdinalIgnoreCase))
                    return null;

                var routes = data["routes"] as JArray;
                if (routes == null || routes.Count == 0)
                    return null;

                var route = routes[0];
                var result = new OsrmRouteResult
                {
                    DistanceMeters = (double)(route["distance"] ?? 0),
                    DurationSeconds = (double)(route["duration"] ?? 0)
                };

                // Lấy geometry (polyline)
                var geometry = route["geometry"] as JObject;
                if (geometry != null)
                {
                    var coordinates = geometry["coordinates"] as JArray;
                    if (coordinates != null)
                    {
                        foreach (var coord in coordinates)
                        {
                            var coordArray = coord as JArray;
                            if (coordArray != null && coordArray.Count >= 2)
                            {
                                result.Geometry.Add(new List<double>
                                {
                                    (double)coordArray[0], // lon
                                    (double)coordArray[1]  // lat
                                });
                            }
                        }
                    }
                }

                // Lấy legs
                var legs = route["legs"] as JArray;
                if (legs != null)
                {
                    foreach (var leg in legs)
                    {
                        var legObj = new OsrmLeg
                        {
                            DistanceMeters = (double)(leg["distance"] ?? 0),
                            DurationSeconds = (double)(leg["duration"] ?? 0)
                        };

                        // Lấy steps trong leg
                        var steps = leg["steps"] as JArray;
                        if (steps != null)
                        {
                            foreach (var step in steps)
                            {
                                var stepGeometry = step["geometry"] as JObject;
                                if (stepGeometry != null)
                                {
                                    var stepCoords = stepGeometry["coordinates"] as JArray;
                                    if (stepCoords != null)
                                    {
                                        foreach (var stepCoord in stepCoords)
                                        {
                                            var stepCoordArray = stepCoord as JArray;
                                            if (stepCoordArray != null && stepCoordArray.Count >= 2)
                                            {
                                                legObj.Steps.Add(new List<double>
                                                {
                                                    (double)stepCoordArray[0], // lon
                                                    (double)stepCoordArray[1]  // lat
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        result.Legs.Add(legObj);
                    }
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tính khoảng cách đơn giản (không cần full route)
        /// </summary>
        public async Task<double?> GetDistanceKmAsync(
            double pickupLat, double pickupLon,
            double dropoffLat, double dropoffLon,
            string profile = "driving")
        {
            var route = await GetRouteAsync(pickupLat, pickupLon, dropoffLat, dropoffLon, profile);
            if (route == null || route.DistanceMeters <= 0)
                return null;

            return route.DistanceMeters / 1000.0;
        }
    }
}






