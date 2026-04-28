using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Areas.Admin.Controllers.Repositories
{
    public interface ICategoryRepository
    {
        IEnumerable <Category> GetAllCategories();
        Category GetById(int id);
        void Add(Category category);
        void Update(Category category);
        void Delete(int id);
    }
}
