using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Areas.Admin.Controllers.Repositories
{
    public class RegionRepository : IRegionRepository
    {
        private readonly ApplicationDbContext _context;
        public RegionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Region> GetAllRegions()
        {
            return _context.Regions.ToList();
        }

        public Region GetById(int id)
        {
            return _context.Regions.Find(id);
        }

        public void Add(Region region)
        {
            _context.Regions.Add(region);
            _context.SaveChanges();
        }

        public void Update(Region region)
        {
            _context.Regions.Update(region);
            _context.SaveChanges();
        }

        public void Delete(int id)
        {
            var region = _context.Regions.Find(id);
            if (region != null)
            {
                _context.Regions.Remove(region);
                _context.SaveChanges();
            }
        }
    }
}
