using WebDuLichDaLat.Models;
using WebDuLichDaLat.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WebDuLichDaLat.Models
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; }
        public DbSet<TouristPlace> TouristPlaces { get; set; }
        public DbSet<Region> Regions { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Festival> Festivals { get; set; }

        // Thêm các DbSet mới
        public DbSet<TransportOption> TransportOptions { get; set; }
        public DbSet<Hotel> Hotels { get; set; }
        public DbSet<Restaurant> Restaurants { get; set; }
        public DbSet<Attraction> Attractions { get; set; }
        public DbSet<CampingSite> CampingSites { get; set; }
        public DbSet<LegacyLocation> LegacyLocations { get; set; }
        public DbSet<TransportPriceHistory> TransportPriceHistories { get; set; }
        public DbSet<LocalTransport> LocalTransports { get; set; }
        public DbSet<RoutePrice> RoutePrices { get; set; }
        public DbSet<NearbyPlace> NearbyPlaces { get; set; }

        // Carpooling models
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<Passenger> Passengers { get; set; }
        public DbSet<PassengerGroup> PassengerGroups { get; set; }
        public DbSet<PendingCarpoolRequest> PendingCarpoolRequests { get; set; }
        public DbSet<CompletedTrip> CompletedTrips { get; set; }
        public DbSet<CompletedTripPassenger> CompletedTripPassengers { get; set; }
        public DbSet<VehiclePricingConfig> VehiclePricingConfigs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Khách sạn" },
                new Category { Id = 2, Name = "Nhà hàng/Quán ăn" },
                new Category { Id = 3, Name = "Địa điểm du lịch" }
                
            );

            modelBuilder.Entity<Contact>(entity =>
            {
                entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Email).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Subject).IsRequired().HasMaxLength(150);
                entity.Property(c => c.Message).IsRequired();
            });

            modelBuilder.Entity<TouristPlace>()
                .HasOne(p => p.Category)
                .WithMany(c => c.TouristPlaces)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TouristPlace>()
                .HasOne(p => p.Region)
                .WithMany(c => c.TouristPlaces)
                .HasForeignKey(p => p.RegionId)
                .OnDelete(DeleteBehavior.SetNull);

            // ✅ Location Relationships - Trái tim của hệ thống
            // Location PHẢI thuộc 1 Area (Region)
            modelBuilder.Entity<Location>()
                .HasOne(l => l.Area)
                .WithMany(r => r.Locations)
                .HasForeignKey(l => l.AreaId)
                .OnDelete(DeleteBehavior.Restrict); // Không cho xóa Area nếu còn Location

            // Location có thể thuộc 1 Category (optional)
            modelBuilder.Entity<Location>()
                .HasOne(l => l.Category)
                .WithMany(c => c.Locations)
                .HasForeignKey(l => l.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // ✅ TouristPlace có thể thuộc 1 Region (nullable để tương thích với dữ liệu hiện có)
            modelBuilder.Entity<TouristPlace>()
                .HasOne(tp => tp.Region)
                .WithMany(r => r.TouristPlaces)
                .HasForeignKey(tp => tp.RegionId)
                .OnDelete(DeleteBehavior.SetNull); // Cho phép null khi xóa Region

            // Hotel và Restaurant giờ có tọa độ trực tiếp, không còn relationship với Location
            // TouristPlace giờ có tọa độ trực tiếp và liên kết trực tiếp với Region

            // ✅ Attraction không còn relationship với Location (đã xóa LocationId khỏi database)
            // Migration 20260102165526_XoaIdlocation đã xóa cột LocationId khỏi bảng Attractions

            // ✅ CampingSite PHẢI thuộc 1 Location
            modelBuilder.Entity<CampingSite>()
                .HasOne(c => c.Location)
                .WithMany(l => l.CampingSites)
                .HasForeignKey(c => c.LocationId)
                .OnDelete(DeleteBehavior.Restrict); // Không cho xóa Location nếu còn CampingSite

            // ✅ Index cho tối ưu query Location
            modelBuilder.Entity<Location>()
                .HasIndex(l => new { l.Latitude, l.Longitude })
                .HasDatabaseName("IX_Location_GPS");

            modelBuilder.Entity<Location>()
                .HasIndex(l => l.AreaId)
                .HasDatabaseName("IX_Location_AreaId");

            modelBuilder.Entity<Location>()
                .HasIndex(l => l.CategoryId)
                .HasDatabaseName("IX_Location_CategoryId");

            modelBuilder.Entity<TransportPriceHistory>()
              .HasOne(p => p.TransportOption)
              .WithMany(t => t.PriceHistories)
              .HasForeignKey(p => p.TransportOptionId);

            modelBuilder.Entity<TransportPriceHistory>()
                .HasOne(p => p.LegacyLocation)
                .WithMany(l => l.PriceHistories)
                .HasForeignKey(p => p.LegacyLocationId);
            
            // ⚠️ KHÔNG seed data ở đây vì đã có dữ liệu trong database
            // Seed data sẽ được thực hiện bằng migration riêng hoặc SQL script
            // SeedData.SeedLegacyLocations(modelBuilder);
            // SeedData.SeedTransportPriceHistory(modelBuilder);
            // SeedData.SeedRegions(modelBuilder);
            
            // ✅ Thiết lập Index cho tối ưu query
            modelBuilder.Entity<LegacyLocation>()
                .HasIndex(l => new { l.Latitude, l.Longitude })
                .HasDatabaseName("IX_LegacyLocation_GPS");
            
            modelBuilder.Entity<LegacyLocation>()
                .HasIndex(l => l.IsActive)
                .HasDatabaseName("IX_LegacyLocation_IsActive");
            
            modelBuilder.Entity<LegacyLocation>()
                .HasIndex(l => l.IsMergedLocation)
                .HasDatabaseName("IX_LegacyLocation_IsMerged");
            
            modelBuilder.Entity<TransportPriceHistory>()
                .HasIndex(p => new { p.LegacyLocationId, p.TransportOptionId })
                .HasDatabaseName("IX_TransportPrice_LocationTransport")
                .IsUnique();

            // Carpooling relationships
            modelBuilder.Entity<Passenger>()
                .HasOne(p => p.Group)
                .WithMany(g => g.Passengers)
                .HasForeignKey(p => p.GroupId)
                .OnDelete(DeleteBehavior.SetNull);

            // Pending request relationships
            modelBuilder.Entity<PendingCarpoolRequest>()
                .HasOne(p => p.Group)
                .WithMany()
                .HasForeignKey(p => p.GroupId)
                .OnDelete(DeleteBehavior.SetNull);

            // Completed trip relationships
            modelBuilder.Entity<CompletedTripPassenger>()
                .HasOne(p => p.CompletedTrip)
                .WithMany(t => t.Passengers)
                .HasForeignKey(p => p.CompletedTripId)
                .OnDelete(DeleteBehavior.Cascade);

            // Review relationships
            modelBuilder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.TouristPlace)
                .WithMany(tp => tp.Reviews)
                .HasForeignKey(r => r.TouristPlaceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index để tối ưu query review
            modelBuilder.Entity<Review>()
                .HasIndex(r => new { r.TouristPlaceId, r.UserId })
                .HasDatabaseName("IX_Review_PlaceUser")
                .IsUnique(); // Mỗi user chỉ đánh giá 1 lần cho mỗi địa điểm
        }
    }
}
