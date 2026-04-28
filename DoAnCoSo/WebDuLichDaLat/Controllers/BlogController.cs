using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class BlogController : Controller
{
    private readonly ApplicationDbContext _context;

    public BlogController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var posts = await _context.BlogPosts
            .OrderByDescending(p => p.PostedDate)
            .ToListAsync();
        return View(posts);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var post = await _context.BlogPosts.FindAsync(id);
        if (post == null) return NotFound();
        return View(post);
    }
}
