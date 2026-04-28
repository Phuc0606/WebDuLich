using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebDuLichDaLat.Controllers
{
    public class RestaurantController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RestaurantController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hiển thị danh sách nhà hàng/quán ăn
        public async Task<IActionResult> Index(string? search, decimal? minPrice, decimal? maxPrice)
        {
            var restaurants = _context.Restaurants
                .Include(r => r.TouristPlace)
                .AsQueryable();

            // Tìm kiếm theo tên hoặc địa chỉ
            if (!string.IsNullOrWhiteSpace(search))
            {
                restaurants = restaurants.Where(r => 
                    r.Name.Contains(search) || 
                    (r.Address != null && r.Address.Contains(search)));
            }

            // Lọc theo giá
            if (minPrice.HasValue)
            {
                restaurants = restaurants.Where(r => r.AveragePricePerPerson >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                restaurants = restaurants.Where(r => r.AveragePricePerPerson <= maxPrice.Value);
            }

            var restaurantsList = await restaurants.OrderBy(r => r.AveragePricePerPerson).ToListAsync();

            ViewBag.Search = search;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.TotalCount = restaurantsList.Count;

            return View(restaurantsList);
        }

        // Chi tiết nhà hàng
        public async Task<IActionResult> Detail(int id)
        {
            var restaurant = await _context.Restaurants
                .Include(r => r.TouristPlace)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (restaurant == null)
                return NotFound();

            return View(restaurant);
        }
    }
}


























































