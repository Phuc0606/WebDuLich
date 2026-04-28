using WebDuLichDaLat.Models;


namespace WebDuLichDaLat.Areas.Admin.Controllers.Repositories
{
    public interface IRegionRepository
    {
        IEnumerable<Region> GetAllRegions();
        Region GetById(int id);
        void Add(Region Region);
        void Update(Region Region);
        void Delete(int id);
    }
}
