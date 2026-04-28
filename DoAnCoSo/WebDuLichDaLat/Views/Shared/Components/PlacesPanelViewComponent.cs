using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Views.Shared.Components
{
    public class PlacesPanelViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public PlacesPanelViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            var regions = await _context.Regions.AsNoTracking().ToListAsync();
            var touristPlacesList = await _context.TouristPlaces
                .AsNoTracking()
                .Take(10)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Regions = regions;
            ViewBag.TouristPlacesList = touristPlacesList;

            return View();
        }
    }
}






