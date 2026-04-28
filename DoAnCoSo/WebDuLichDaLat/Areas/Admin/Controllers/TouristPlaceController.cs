using WebDuLichDaLat.Areas.Admin.Controllers.Repositories;
using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace WebDuLichDaLat.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class TouristPlaceController : Controller
    {
        private readonly ITouristPlaceRepository _touristPlaceRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IRegionRepository _regionRepository;
        private readonly ApplicationDbContext _context;
        public TouristPlaceController(ITouristPlaceRepository touristPlaceRepository, ICategoryRepository categoryRepository, IRegionRepository regionRepository, ApplicationDbContext context)
        {
            _touristPlaceRepository = touristPlaceRepository;
            _categoryRepository = categoryRepository;
            _regionRepository = regionRepository;
            _context = context;
        }

        public IActionResult Manage()
        {
            var touristPlaces = _touristPlaceRepository.GetAll();
            return View(touristPlaces);
        }
        public IActionResult Add()
        {
            var categories = _categoryRepository.GetAllCategories();
            var regions = _regionRepository.GetAllRegions();

            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            ViewBag.Regions = new SelectList(regions, "Id", "Name");
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Add(TouristPlace touristPlace, IFormFile imageUrl, List<IFormFile> imageUrls)
        {
            if (ModelState.IsValid)
            {
                // Lưu ảnh đại diện nếu có
                if (imageUrl != null)
                {
                    touristPlace.ImageUrl = await SaveImage(imageUrl);
                }

                // Lưu các ảnh phụ nếu có
                if (imageUrls != null && imageUrls.Count > 0)
                {
                    touristPlace.ImageUrls = new List<string>();
                    foreach (var file in imageUrls)
                    {
                        var savedPath = await SaveImage(file);
                        touristPlace.ImageUrls.Add(savedPath);
                    }
                }

                // Gán category cho touristPlace
                var category = _categoryRepository.GetAllCategories()
                                .FirstOrDefault(c => c.Id == touristPlace.CategoryId);

                if (category == null)
                {
                    ModelState.AddModelError("CategoryId", "Danh mục không hợp lệ");
                    ViewBag.Categories = new SelectList(_categoryRepository.GetAllCategories(), "Id", "Name");
                    ViewBag.Regions = new SelectList(_regionRepository.GetAllRegions(), "Id", "Name");

                    return View(touristPlace);
                }

                touristPlace.Category = category;

                // Thêm touristPlace vào database
                _touristPlaceRepository.Add(touristPlace);

                return RedirectToAction("Index");
            }

            // Nếu có lỗi, load lại danh sách category cho dropdown
            ViewBag.Categories = new SelectList(_categoryRepository.GetAllCategories(), "Id", "Name");
            ViewBag.Regions = new SelectList(_regionRepository.GetAllRegions(), "Id", "Name");
            return View(touristPlace);
        }





        private async Task<string> SaveImage(IFormFile image)
        {
            var savePath = Path.Combine("wwwroot/images", image.FileName);
            using (var fileStream = new FileStream(savePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
            return "/images/" + image.FileName;
        }


        // Các actions khác như Display, Update, Delete
        public IActionResult Index(int? categoryId, int? RegionId)
        {
            var categories = _categoryRepository.GetAllCategories();
            var regions = _regionRepository.GetAllRegions();

            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            ViewBag.Regions = new SelectList(regions, "RegionId", "Name");

            var touristPlaces = _touristPlaceRepository.GetAll();

            if (categoryId.HasValue)
            {
                touristPlaces = touristPlaces.Where(p => p.CategoryId == categoryId.Value);
            }

            if (RegionId.HasValue)
            {
                touristPlaces = touristPlaces.Where(p => p.RegionId == RegionId.Value);
            }

            return View(touristPlaces); // ✅ trả danh sách Địa điểm ra view
        }


        // Display a single touristPlace
        public IActionResult Display(string id)
        {
            var touristPlace = _touristPlaceRepository.GetById(id);
            if (touristPlace == null)
            {
                return NotFound();
            }
            return View(touristPlace);
        }
        // Show the touristPlace update form
        public IActionResult Update(string id)
        {
            var touristPlace = _touristPlaceRepository.GetById(id);
            if (touristPlace == null)
            {
                return NotFound();
            }

            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name", touristPlace.CategoryId);
            ViewBag.Regions = new SelectList(_context.Regions, "Id", "Name", touristPlace.RegionId);

            return View(touristPlace);
        }
        // Process the touristPlace update



        [HttpPost]
        public async Task<IActionResult> Update(TouristPlace touristPlace, IFormFile? ImageFile, List<IFormFile>? AdditionalImages)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name", touristPlace.CategoryId);
                ViewBag.Regions = new SelectList(_context.Regions, "Id", "Name", touristPlace.RegionId);
                return View(touristPlace);
            }

            var existingTouristPlace = _touristPlaceRepository.GetById(touristPlace.Id);
            if (existingTouristPlace == null) return NotFound();

            // Cập nhật
            existingTouristPlace.Name = touristPlace.Name;
            existingTouristPlace.RegionId = touristPlace.RegionId;
            existingTouristPlace.ReviewContent = touristPlace.ReviewContent;
            existingTouristPlace.Description = touristPlace.Description;
            existingTouristPlace.CategoryId = touristPlace.CategoryId;
            existingTouristPlace.Latitude = touristPlace.Latitude;
            existingTouristPlace.Longitude = touristPlace.Longitude;


            if (ImageFile != null)
            {
                existingTouristPlace.ImageUrl = await SaveImage(ImageFile);
            }

            if (AdditionalImages != null && AdditionalImages.Count > 0)
            {
                existingTouristPlace.ImageUrls = existingTouristPlace.ImageUrls ?? new List<string>();
                foreach (var file in AdditionalImages)
                {
                    var savedPath = await SaveImage(file);
                    existingTouristPlace.ImageUrls.Add(savedPath);
                }
            }

            _touristPlaceRepository.Update(existingTouristPlace);

            return RedirectToAction("Index");
        }





        // Hiển thị trang xác nhận xóa (GET)
        public IActionResult Delete(string id)
        {
            var touristPlace = _touristPlaceRepository.GetById(id);
            if (touristPlace == null) return NotFound();
            return View(touristPlace);
        }



        // Xử lý xóa (POST)
        [HttpPost]
        public IActionResult DeleteConfirmed(string id)
        {
            _touristPlaceRepository.Delete(id);
            return RedirectToAction("Index");
        }

        public IActionResult Privacy()
        {
            return View();
        }


    }
}