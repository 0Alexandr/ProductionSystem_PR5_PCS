using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionSystem.Data;

namespace ProductionSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalOrders = await _db.WorkOrders.CountAsync();
            ViewBag.ActiveOrders = await _db.WorkOrders.CountAsync(o => o.Status == "InProgress");
            ViewBag.PendingOrders = await _db.WorkOrders.CountAsync(o => o.Status == "Pending");
            ViewBag.LowStockCount = await _db.Materials.CountAsync(m => m.Quantity <= m.MinimalStock);
            ViewBag.ActiveLines = await _db.ProductionLines.CountAsync(l => l.Status == "Active");
            ViewBag.TotalProducts = await _db.Products.CountAsync();

            var recentOrders = await _db.WorkOrders
                .Include(o => o.Product)
                .Include(o => o.ProductionLine)
                .OrderByDescending(o => o.StartDate)
                .Take(5)
                .ToListAsync();

            return View(recentOrders);
        }
    }
}
