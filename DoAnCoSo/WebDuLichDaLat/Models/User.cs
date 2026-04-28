using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace WebDuLichDaLat.Models
{
    public class User : IdentityUser
    {
        [Required]
        public string FullName { get; set; }
    
    }
}
