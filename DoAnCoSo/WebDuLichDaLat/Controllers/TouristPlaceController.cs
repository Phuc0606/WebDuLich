using WebDuLichDaLat.Areas.Admin.Controllers.Repositories;
using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace WebDuLichDaLat.Controllers
{
    public class TouristPlaceController : Controller
    {
        private readonly ITouristPlaceRepository _touristPlaceRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IRegionRepository _regionRepository;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public TouristPlaceController(
            ITouristPlaceRepository touristPlaceRepository,
            ICategoryRepository categoryRepository,
            IRegionRepository regionRepository,
            ApplicationDbContext context,
            UserManager<User> userManager)
        {
            _touristPlaceRepository = touristPlaceRepository;
            _categoryRepository = categoryRepository;
            _regionRepository = regionRepository;
            _context = context;
            _userManager = userManager;
        }

        // Hiển thị danh sách Địa điểm có lọc theo danh mục và Địa điểm
        public IActionResult Index(int? categoryId, int? regionId)
        {
            var categories = _categoryRepository.GetAllCategories();
            var regions = _regionRepository.GetAllRegions();

            ViewBag.Categories = categories;
            ViewBag.Regions = regions;
            ViewBag.Manufacturers = regions; // For compatibility with view

            var allTouristPlaces = _context.TouristPlaces
                .Include(p => p.Category)
                .Include(p => p.Region)
                .Include(p => p.Reviews)
                .AsQueryable();

            if (categoryId.HasValue)
                allTouristPlaces = allTouristPlaces.Where(p => p.CategoryId == categoryId.Value);

            if (regionId.HasValue)
                allTouristPlaces = allTouristPlaces.Where(p => p.RegionId == regionId.Value);

            // Calculate average rating from Reviews for each place
            var placesList = allTouristPlaces.ToList();
            foreach (var place in placesList)
            {
                if (place.Reviews != null && place.Reviews.Any())
                {
                    place.Rating = (int)Math.Round(place.Reviews.Average(r => r.Rating));
                }
            }

            return View(placesList);
        }

        // Chi tiết Địa điểm + đánh giá
        public IActionResult Display(string id)
        {
            var touristPlace = _context.TouristPlaces
                .Include(p => p.Reviews)
                .Include(p => p.Category)
                .Include(p => p.Region)
                .FirstOrDefault(p => p.Id == id);

            if (touristPlace == null)
                return NotFound();

            // Tính trung bình và tổng số đánh giá
            if (touristPlace.Reviews != null && touristPlace.Reviews.Any())
            {
                ViewBag.AverageRating = touristPlace.Reviews.Average(r => r.Rating);
                ViewBag.RatingCount = touristPlace.Reviews.Count();
            }
            else
            {
                ViewBag.AverageRating = 0;
                ViewBag.RatingCount = 0;
            }

            return View(touristPlace);
        }

        // Tìm kiếm Địa điểm theo từ khóa
        public IActionResult Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return RedirectToAction("Index");

            var touristPlaces = _touristPlaceRepository.GetAll()
                .Where(p =>
                    (!string.IsNullOrEmpty(p.Name) && p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(p.Description) && p.Description.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ViewBag.Categories = _categoryRepository.GetAllCategories();
            ViewBag.Regions = _regionRepository.GetAllRegions();

            return View("Index", touristPlaces);
        }


        // Trang bản đồ lớn (sidebar + map)
        public IActionResult Map()
        {
            var categories = _categoryRepository.GetAllCategories();
            var regions = _regionRepository.GetAllRegions();
            var allTouristPlaces = _touristPlaceRepository.GetAll().ToList();

            ViewBag.Categories = categories;
            ViewBag.Manufacturers = regions;
            ViewBag.TouristPlacesJson = System.Text.Json.JsonSerializer.Serialize(
                allTouristPlaces.Select(tp => new { tp.Id, tp.Name, tp.Latitude, tp.Longitude, tp.CategoryId })
            );

            return View(allTouristPlaces);
        }

        // Trang riêng tư
        public IActionResult Privacy()
        {
            return View();
        }

        // API: Gửi đánh giá - Bắt buộc đăng nhập
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Rate(string touristPlaceId, int rating)
        {
            if (string.IsNullOrEmpty(touristPlaceId) || rating < 1 || rating > 5)
                return BadRequest(new { message = "Dữ liệu không hợp lệ" });

            // Lấy UserId từ user đã đăng nhập
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập để đánh giá" });

            var touristPlace = await _context.TouristPlaces
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(p => p.Id == touristPlaceId);
            
            if (touristPlace == null)
                return NotFound(new { message = "Không tìm thấy địa điểm" });

            // Kiểm tra xem user đã đánh giá chưa
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.TouristPlaceId == touristPlaceId && r.UserId == userId);

            if (existingReview != null)
            {
                // Cập nhật đánh giá cũ
                existingReview.Rating = rating;
                existingReview.CreatedAt = DateTime.Now;
            }
            else
            {
                // Tạo đánh giá mới
                var review = new Review
                {
                    TouristPlaceId = touristPlaceId,
                    UserId = userId,
                    Rating = rating,
                    CreatedAt = DateTime.Now
                };
                _context.Reviews.Add(review);
            }

            // Tính lại rating trung bình và cập nhật vào TouristPlace
            var allReviews = await _context.Reviews
                .Where(r => r.TouristPlaceId == touristPlaceId)
                .ToListAsync();

            if (allReviews.Any())
            {
                touristPlace.Rating = (int)Math.Round(allReviews.Average(r => r.Rating));
            }
            else
            {
                touristPlace.Rating = 0;
            }

            await _context.SaveChangesAsync();

            return Ok(new { 
                message = "Cảm ơn bạn đã đánh giá!",
                newRating = touristPlace.Rating,
                reviewCount = allReviews.Count
            });
        }
    }
}
