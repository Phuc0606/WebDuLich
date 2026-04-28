using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using WebDuLichDaLat.Models;

public class RoutePrice
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string FromProvince { get; set; } // "Long An", "TP.HCM", ...

    [Required, StringLength(100)]
    public string ToProvince { get; set; }   // "Lâm Đồng" (Đà Lạt)

    [Required]
    public int TransportOptionId { get; set; }
    public TransportOption TransportOption { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
}