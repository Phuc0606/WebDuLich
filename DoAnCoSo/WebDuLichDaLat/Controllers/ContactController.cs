using Microsoft.AspNetCore.Mvc;
using WebDuLichDaLat.Models;

namespace WebDuLichDaLat.Controllers
{
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context; 

        public ContactController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(Contact contact)
        {
            if (ModelState.IsValid)
            {
                contact.CreatedAt = DateTime.Now;
                _context.Contacts.Add(contact); 
                _context.SaveChanges();         

                ViewBag.Success = "Cảm ơn bạn đã liên hệ. Chúng tôi sẽ phản hồi sớm!";
                ModelState.Clear();
            }

            return View();
        }
    }
}
