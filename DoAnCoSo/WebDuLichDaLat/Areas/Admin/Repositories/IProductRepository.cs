using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Areas.Admin.Controllers.Repositories
{
    public interface ITouristPlaceRepository
    {
        IEnumerable<TouristPlace> GetAll();
        TouristPlace GetById(string id);
        void Add(TouristPlace touristPlace);
        void Update(TouristPlace touristPlace);
        void Delete(string id);

        // 🔽 Các phương thức mới để xử lý đánh giá
        double GetAverageRating(string touristPlaceId);  // Trung bình điểm đánh giá
        int GetRatingCount(string touristPlaceId);       // Số lượt đánh giá
    }
}
