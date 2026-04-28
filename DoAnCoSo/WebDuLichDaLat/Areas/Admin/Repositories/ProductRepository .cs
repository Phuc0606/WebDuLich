using Microsoft.EntityFrameworkCore;
using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Areas.Admin.Controllers.Repositories
{
    public class TouristPlaceRepository : ITouristPlaceRepository
    {
        private readonly ApplicationDbContext _context;

        public TouristPlaceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<TouristPlace> GetAll()
        {
            return _context.TouristPlaces
                .Include(p => p.Category)
                .Include(p => p.Region)
                .ToList();
        }

        public TouristPlace? GetById(string id)
        {
            return _context.TouristPlaces
                .Include(p => p.Category)
                .Include(p => p.Region)
                .FirstOrDefault(p => p.Id == id);
        }

        public void Add(TouristPlace touristPlace)
        {
            _context.TouristPlaces.Add(touristPlace);
            _context.SaveChanges();
        }

        public void Update(TouristPlace touristPlace)
        {
            _context.TouristPlaces.Update(touristPlace);
            _context.SaveChanges();
        }

        public void Delete(string id)
        {
            var touristPlace = GetById(id);
            if (touristPlace != null)
            {
                _context.TouristPlaces.Remove(touristPlace);
                _context.SaveChanges();
            }
        }

        // 🔽 Mới thêm để lấy trung bình đánh giá
        public double GetAverageRating(string touristPlaceId)
        {
            return _context.Reviews
                .Where(r => r.TouristPlaceId == touristPlaceId)
                .Select(r => r.Rating)
                .DefaultIfEmpty(0)
                .Average();
        }

        // 🔽 Mới thêm để lấy tổng lượt đánh giá
        public int GetRatingCount(string touristPlaceId)
        {
            return _context.Reviews
                .Count(r => r.TouristPlaceId == touristPlaceId);
        }
    }
}
