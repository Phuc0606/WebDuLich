// /ViewComponents/RegionMenuViewComponent.cs
using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;


public class RegionMenuViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    // Inject ApplicationDbContext để truy vấn cơ sở dữ liệu
    public RegionMenuViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    // Xử lý logic để lấy dữ liệu danh mục sản phẩm và đếm số lượng sản phẩm theo từng danh mục
    public IViewComponentResult Invoke()
    {
        // Lấy tất cả các danh mục và đếm số lượng sản phẩm trong mỗi danh mục
        var regions = _context.Regions
                                 .Select(c => new RegionViewModel
                                 {
                                     Id = c.Id,
                                     Name = c.Name,
                                    
                                 }).ToList();

        return View(regions);   // Trả về view hiển thị danh sách Region
    }

    // Model dữ liệu truyền vào view
    public class RegionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        
    }
}
