using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDuLichDaLat.Models;

[Authorize]
public class FavoriteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public FavoriteController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var favorites = await _context.Favorites
                .Include(f => f.TouristPlace)
                .Where(f => f.UserId == userId)
                .ToListAsync();

            return View(favorites);
        }

   
    [HttpPost]
        public async Task<IActionResult> Add(string touristPlaceId)
        {
            var userId = _userManager.GetUserId(User);
            var exists = await _context.Favorites
                .AnyAsync(f => f.UserId == userId && f.TouristPlaceId == touristPlaceId);

            if (!exists)
            {
                _context.Favorites.Add(new Favorite
                {
                    UserId = userId,
                    TouristPlaceId = touristPlaceId
                });

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Remove(string touristPlaceId)
        {
            var userId = _userManager.GetUserId(User);
            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == userId && f.TouristPlaceId == touristPlaceId);

            if (favorite != null)
            {
                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
    }

