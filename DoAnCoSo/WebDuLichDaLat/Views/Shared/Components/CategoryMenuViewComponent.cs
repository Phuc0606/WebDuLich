// /ViewComponents/CategoryMenuViewComponent.cs
using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;


public class CategoryMenuViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    // Inject ApplicationDbContext để truy vấn cơ sở dữ liệu
    public CategoryMenuViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    // Xử lý logic để lấy dữ liệu danh mục sản phẩm và đếm số lượng sản phẩm theo từng danh mục
    public IViewComponentResult Invoke()
    {
        // Lấy tất cả các danh mục và đếm số lượng sản phẩm trong mỗi danh mục
        var categories = _context.Categories
                                 .Select(c => new CategoryViewModel
                                 {
                                     Id = c.Id,
                                     Name = c.Name,
                                    
                                 }).ToList();

        return View(categories);   // Trả về view hiển thị danh sách category
    }

    // Model dữ liệu truyền vào view
    public class CategoryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
     
    }
}
