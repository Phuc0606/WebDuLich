using WebDuLichDaLat.Areas.Admin.Controllers.Repositories;
using WebDuLichDaLat.Models;
using WebDuLichDaLat.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using WebDuLichDaLat;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repository DI

builder.Services.AddScoped<ITouristPlaceRepository, TouristPlaceRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IRegionRepository, RegionRepository>();

// ✅ Đăng ký TransportPriceCalculator service
builder.Services.AddScoped<TransportPriceCalculator>();

// Trip Planner Services
builder.Services.AddScoped<SuggestionFormatterService>();
builder.Services.AddScoped<CostCalculationService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<ItineraryService>();

// Carpooling Services
builder.Services.AddScoped<CarpoolMatchingService>();
builder.Services.AddScoped<KMeansClusteringService>();
builder.Services.AddScoped<MinCostMaxFlowService>();
builder.Services.AddScoped<PDPTWService>();
builder.Services.AddScoped<OsrmRouteService>();
builder.Services.AddScoped<RouteMatchingService>();

// Flight Search Service
builder.Services.AddScoped<AmadeusFlightService>();

// Identity
builder.Services.AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.SignIn.RequireConfirmedEmail = true;
});

// Email
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Razor & Session
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = false;
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

// API check email
app.MapGet("/api/email-confirmed", async (string email, UserManager<User> userManager) =>
{
    var user = await userManager.FindByEmailAsync(email);
    if (user == null) return Results.NotFound();
    return Results.Ok(new { confirmed = await userManager.IsEmailConfirmedAsync(user) });
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ✅ Route cho Area (Admin)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=TouristPlace}/{action=Index}/{id?}");

// ✅ Route mặc định (trang chủ Home)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.MapControllerRoute(
    name: "blog",
    pattern: "Blog/{action=Index}/{id?}",
    defaults: new { controller = "Blog" });

app.MapRazorPages();

// Seed data - tạm thời comment out
/*
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    SimpleDataSeeder.SeedBasicData(context);
}
*/

// Seed Regions - Bỏ comment để chạy seed Regions khi cần
/*
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await WebDuLichDaLat.Data.SeedData.SeedRegionsAsync(context);
}
*/

app.Run();
