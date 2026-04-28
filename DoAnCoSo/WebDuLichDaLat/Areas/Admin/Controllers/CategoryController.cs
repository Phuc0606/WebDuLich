using WebDuLichDaLat.Areas.Admin.Controllers.Repositories;
using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


[Area("Admin")]
public class CategoryController : Controller
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryController(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public IActionResult Index()
    {
        var categories = _categoryRepository.GetAllCategories();
        return View(categories);
    }

    public IActionResult Add()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Add(Category category)
    {
        if (ModelState.IsValid)
        {
            _categoryRepository.Add(category);
            return RedirectToAction("Index");
        }
        return View(category);
    }

    public IActionResult Edit(int id)
    {
        var category = _categoryRepository.GetById(id);
        if (category == null) return NotFound();
        return View(category);
    }

    [HttpPost]
    public IActionResult Edit(Category category)
    {
        if (ModelState.IsValid)
        {
            _categoryRepository.Update(category);
            return RedirectToAction("Index");
        }
        return View(category);
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        _categoryRepository.Delete(id);
        return RedirectToAction("Index");
    }
}
