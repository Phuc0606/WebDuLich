using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebDuLichDaLat.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BlogController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Xem danh sách bài viết
        public async Task<IActionResult> Index()
        {
            var posts = await _context.BlogPosts.OrderByDescending(p => p.PostedDate).ToListAsync();
            return View(posts);
        }

        // Tạo bài viết mới
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(BlogPost post)
        {
            if (ModelState.IsValid)
            {
                post.PostedDate = DateTime.Now;
                _context.BlogPosts.Add(post);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(post);
        }

        // Sửa bài viết
        public async Task<IActionResult> Edit(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null) return NotFound();
            return View(post);
        }

        // Sửa bài viết
        [HttpPost]
        public async Task<IActionResult> Edit(int id, BlogPost post, IFormFile? ImageFile)
        {
            if (!ModelState.IsValid)
            {
                return View(post);
            }

            var existingPost = await _context.BlogPosts.FindAsync(id);
            if (existingPost == null) return NotFound();

            // Cập nhật các thuộc tính cơ bản
            existingPost.Title = post.Title;
            existingPost.Content = post.Content;
            existingPost.PostedDate = DateTime.Now;

            // Nếu có ảnh mới thì xử lý lưu và thay thế
            if (ImageFile != null && ImageFile.Length > 0)
            {
                // Lấy tên file an toàn
                var fileName = Path.GetFileName(ImageFile.FileName);
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Images", fileName);

                // Lưu file vào wwwroot/Images
                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(stream);
                }

                // Cập nhật tên ảnh mới
                existingPost.ImageUrl = fileName;
            }

            // Nếu không có ảnh mới: giữ nguyên ảnh cũ (không làm gì)

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // Xóa bài viết
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null) return NotFound();
            return View(post);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post != null)
            {
                _context.BlogPosts.Remove(post);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }
    }
}
