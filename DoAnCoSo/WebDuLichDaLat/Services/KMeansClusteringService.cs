using WebDuLichDaLat.Models;
using System.Collections.Generic;
using System.Linq;

namespace WebDuLichDaLat.Services
{
    public class KMeansClusteringService
    {
        public class ClusterPoint
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public int PassengerId { get; set; }
            public Passenger? Passenger { get; set; }
        }

        public class Cluster
        {
            public double CenterLatitude { get; set; }
            public double CenterLongitude { get; set; }
            public List<ClusterPoint> Points { get; set; } = new List<ClusterPoint>();
        }

        public List<Cluster> ClusterPassengers(List<Passenger> passengers, int k)
        {
            if (passengers == null || !passengers.Any())
                return new List<Cluster>();

            // Chuyển đổi passengers thành points
            var points = passengers.Select(p => new ClusterPoint
            {
                Latitude = p.PickupLatitude,
                Longitude = p.PickupLongitude,
                PassengerId = p.Id,
                Passenger = p
            }).ToList();

            if (points.Count <= k)
            {
                // Nếu số điểm <= số cluster, mỗi điểm là 1 cluster
                return points.Select(p => new Cluster
                {
                    CenterLatitude = p.Latitude,
                    CenterLongitude = p.Longitude,
                    Points = new List<ClusterPoint> { p }
                }).ToList();
            }

            // Khởi tạo k centroids ngẫu nhiên
            var random = new Random();
            var clusters = new List<Cluster>();
            for (int i = 0; i < k; i++)
            {
                var randomPoint = points[random.Next(points.Count)];
                clusters.Add(new Cluster
                {
                    CenterLatitude = randomPoint.Latitude,
                    CenterLongitude = randomPoint.Longitude,
                    Points = new List<ClusterPoint>()
                });
            }

            // Lặp để tối ưu clusters
            bool changed = true;
            int maxIterations = 100;
            int iteration = 0;

            while (changed && iteration < maxIterations)
            {
                iteration++;
                changed = false;

                // Xóa points khỏi clusters
                foreach (var cluster in clusters)
                {
                    cluster.Points.Clear();
                }

                // Gán mỗi point vào cluster gần nhất
                foreach (var point in points)
                {
                    var nearestCluster = clusters
                        .OrderBy(c => CalculateDistance(
                            point.Latitude, point.Longitude,
                            c.CenterLatitude, c.CenterLongitude))
                        .First();

                    nearestCluster.Points.Add(point);
                }

                // Cập nhật centroids
                foreach (var cluster in clusters)
                {
                    if (cluster.Points.Any())
                    {
                        double newLat = cluster.Points.Average(p => p.Latitude);
                        double newLng = cluster.Points.Average(p => p.Longitude);

                        if (Math.Abs(cluster.CenterLatitude - newLat) > 0.0001 ||
                            Math.Abs(cluster.CenterLongitude - newLng) > 0.0001)
                        {
                            changed = true;
                            cluster.CenterLatitude = newLat;
                            cluster.CenterLongitude = newLng;
                        }
                    }
                }
            }

            // Loại bỏ clusters rỗng
            return clusters.Where(c => c.Points.Any()).ToList();
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Bán kính Trái Đất (km)
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














































