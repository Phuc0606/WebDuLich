using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebDuLichDaLat.Controllers
{
    public class FestivalController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FestivalController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var festivals = await _context.Festivals
                .Where(f => f.IsActive)
                .OrderByDescending(f => f.StartDate)
                .ToListAsync();
            return View(festivals);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var festival = await _context.Festivals.FindAsync(id);
            if (festival == null || !festival.IsActive)
            {
                return NotFound();
            }
            return View(festival);
        }
    }
}























































