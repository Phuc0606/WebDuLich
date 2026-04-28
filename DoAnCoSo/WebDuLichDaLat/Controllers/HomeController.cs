using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy các địa điểm phổ biến (theo rating, giới hạn 6 địa điểm)
            var popularPlaces = await _context.TouristPlaces
                .AsNoTracking()
                .OrderByDescending(p => p.Rating)
                .ThenByDescending(p => p.Name)
                .Take(6)
                .ToListAsync();

            // Lấy các địa điểm gợi ý (top rated, có hình ảnh, bao gồm Region) cho section gợi ý
            var suggestedPlaces = await _context.TouristPlaces
                .AsNoTracking()
                .Include(p => p.Region)
                .Where(p => !string.IsNullOrEmpty(p.ImageUrl))
                .OrderByDescending(p => p.Rating)
                .ThenByDescending(p => p.Name)
                .Take(8)
                .ToListAsync();

            // Nếu không có địa điểm nào có hình ảnh, lấy tất cả địa điểm
            if (!suggestedPlaces.Any())
            {
                suggestedPlaces = await _context.TouristPlaces
                    .AsNoTracking()
                    .Include(p => p.Region)
                    .OrderByDescending(p => p.Rating)
                    .ThenByDescending(p => p.Name)
                    .Take(8)
                    .ToListAsync();
            }

            // Lấy địa điểm nổi bật nhất (rating cao nhất) cho block giữa
            var featuredPlace = await _context.TouristPlaces
                .AsNoTracking()
                .OrderByDescending(p => p.Rating)
                .FirstOrDefaultAsync();

            // Lấy bài viết blog mới nhất cho block phải
            var latestBlogPost = await _context.BlogPosts
                .AsNoTracking()
                .OrderByDescending(p => p.PostedDate)
                .FirstOrDefaultAsync();

            // Lấy lễ hội/sự kiện sắp diễn ra hoặc mới nhất cho block đầu
            Festival? upcomingFestival = null;
            try
            {
                upcomingFestival = await _context.Festivals
                    .AsNoTracking()
                    .Where(f => f.IsActive && f.StartDate >= DateTime.Now)
                    .OrderBy(f => f.StartDate)
                    .FirstOrDefaultAsync();

                // Nếu không có lễ hội sắp diễn ra, lấy lễ hội mới nhất
                if (upcomingFestival == null)
                {
                    upcomingFestival = await _context.Festivals
                        .AsNoTracking()
                        .Where(f => f.IsActive)
                        .OrderByDescending(f => f.StartDate)
                        .FirstOrDefaultAsync();
                }
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid object name"))
            {
                // Bảng Festivals chưa tồn tại, để null
                upcomingFestival = null;
            }
            catch
            {
                // Xử lý các lỗi khác, để null
                upcomingFestival = null;
            }

            // Lấy 8 lễ hội/sự kiện gần nhất cho section events
            List<Festival>? recentFestivals = null;
            try
            {
                recentFestivals = await _context.Festivals
                    .AsNoTracking()
                    .Where(f => f.IsActive)
                    .OrderByDescending(f => f.StartDate)
                    .Take(8)
                    .ToListAsync();
            }
            catch
            {
                recentFestivals = new List<Festival>();
            }

            ViewBag.FeaturedPlace = featuredPlace;
            ViewBag.LatestBlogPost = latestBlogPost;
            ViewBag.UpcomingFestival = upcomingFestival;
            ViewBag.SuggestedPlaces = suggestedPlaces;
            ViewBag.RecentFestivals = recentFestivals ?? new List<Festival>();

            return View(popularPlaces);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
