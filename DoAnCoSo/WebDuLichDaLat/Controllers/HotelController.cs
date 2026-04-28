using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebDuLichDaLat.Controllers
{
    public class HotelController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HotelController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hiển thị danh sách khách sạn
        public async Task<IActionResult> Index(string? search, decimal? minPrice, decimal? maxPrice)
        {
            var hotels = _context.Hotels
                .Include(h => h.TouristPlace)
                .AsQueryable();

            // Tìm kiếm theo tên hoặc địa chỉ
            if (!string.IsNullOrWhiteSpace(search))
            {
                hotels = hotels.Where(h => 
                    h.Name.Contains(search) || 
                    (h.Address != null && h.Address.Contains(search)));
            }

            // Lọc theo giá
            if (minPrice.HasValue)
            {
                hotels = hotels.Where(h => h.PricePerNight >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                hotels = hotels.Where(h => h.PricePerNight <= maxPrice.Value);
            }

            var hotelsList = await hotels.OrderBy(h => h.PricePerNight).ToListAsync();

            ViewBag.Search = search;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.TotalCount = hotelsList.Count;

            return View(hotelsList);
        }

        // Chi tiết khách sạn
        public async Task<IActionResult> Detail(int id)
        {
            var hotel = await _context.Hotels
                .Include(h => h.TouristPlace)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (hotel == null)
                return NotFound();

            return View(hotel);
        }
    }
}


























































