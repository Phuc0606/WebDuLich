using WebDuLichDaLat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace WebDuLichDaLat.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<User> _userManager;

        public ConfirmEmailModel(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Không tìm thấy người dùng với ID: '{userId}'.");
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); 

            var result = await _userManager.ConfirmEmailAsync(user, code);

            if (result.Succeeded)
            {
                TempData["StatusMessage"] = "✅ Xác nhận email thành công. Bạn có thể đăng nhập.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }
            else
            {
                StatusMessage = "❌ Xác nhận email thất bại. Liên kết có thể đã hết hạn hoặc không hợp lệ.";
                return Page(); 
            }


            return Page();
        }
    }
}
