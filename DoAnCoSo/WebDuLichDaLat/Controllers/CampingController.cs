using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebDuLichDaLat.Controllers
{
    public class CampingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CampingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hiển thị danh sách khu cắm trại
        public async Task<IActionResult> Index(string? search, decimal? minPrice, decimal? maxPrice)
        {
            var campingSites = _context.CampingSites
                .Include(c => c.Location)
                .Include(c => c.TouristPlace)
                .AsQueryable();

            // Tìm kiếm theo tên hoặc địa chỉ
            if (!string.IsNullOrWhiteSpace(search))
            {
                campingSites = campingSites.Where(c => 
                    c.Name.Contains(search) || 
                    (c.Address != null && c.Address.Contains(search)));
            }

            // Lọc theo giá
            if (minPrice.HasValue)
            {
                campingSites = campingSites.Where(c => c.PricePerNight >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                campingSites = campingSites.Where(c => c.PricePerNight <= maxPrice.Value);
            }

            var campingSitesList = await campingSites.OrderBy(c => c.PricePerNight).ToListAsync();

            ViewBag.Search = search;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.TotalCount = campingSitesList.Count;

            return View(campingSitesList);
        }

        // Chi tiết khu cắm trại
        public async Task<IActionResult> Detail(int id)
        {
            var campingSite = await _context.CampingSites
                .Include(c => c.Location)
                .Include(c => c.TouristPlace)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (campingSite == null)
                return NotFound();

            return View(campingSite);
        }
    }
}





















