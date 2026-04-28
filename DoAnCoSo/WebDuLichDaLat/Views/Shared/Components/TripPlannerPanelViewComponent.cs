using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Views.Shared.Components
{
    public class TripPlannerPanelViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public TripPlannerPanelViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Lấy thông tin thời tiết hiện tại (có thể tích hợp API thời tiết sau)
            ViewBag.CurrentWeather = "Nắng đẹp";
            ViewBag.Temperature = "22°C";
            
            // Lấy số lượng địa điểm, khách sạn, nhà hàng
            var placeCount = await _context.TouristPlaces.CountAsync();
            var hotelCount = await _context.Hotels.CountAsync();
            var restaurantCount = await _context.Restaurants.CountAsync();
            
            ViewBag.PlaceCount = placeCount;
            ViewBag.HotelCount = hotelCount;
            ViewBag.RestaurantCount = restaurantCount;
            
            return View();
        }
    }
}






















































