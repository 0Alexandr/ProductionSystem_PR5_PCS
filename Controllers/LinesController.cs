using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionSystem.Data;
using ProductionSystem.Models;

namespace ProductionSystem.Controllers
{
    public class LinesController : Controller
    {
        private readonly AppDbContext _db;

        public LinesController(AppDbContext db)
        {
            _db = db;
        }

        // GET /Lines
        public async Task<IActionResult> Index()
        {
            var lines = await _db.ProductionLines
                .Include(l => l.CurrentWorkOrder)
                .ThenInclude(wo => wo!.Product)
                .Include(l => l.WorkOrders.Where(wo => wo.Status != "Completed" && wo.Status != "Cancelled"))
                .ThenInclude(wo => wo.Product)
                .ToListAsync();

            return View(lines);
        }

        // GET /Lines/Create
        public IActionResult Create()
        {
            return View(new ProductionLine());
        }

        // POST /Lines/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductionLine line)
        {
            if (ModelState.IsValid)
            {
                _db.ProductionLines.Add(line);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Линия «{line.Name}» создана.";
                return RedirectToAction(nameof(Index));
            }
            return View(line);
        }

        // POST /Lines/SetStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, string status)
        {
            var line = await _db.ProductionLines.FindAsync(id);
            if (line == null) return NotFound();
            line.Status = status;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Статус линии изменён на «{status}».";
            return RedirectToAction(nameof(Index));
        }

        // POST /Lines/SetEfficiency
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetEfficiency(int id, float factor)
        {
            var line = await _db.ProductionLines.FindAsync(id);
            if (line == null) return NotFound();
            line.EfficiencyFactor = Math.Clamp(factor, 0.5f, 2.0f);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Коэффициент эффективности обновлён.";
            return RedirectToAction(nameof(Index));
        }

        // POST /Lines/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var line = await _db.ProductionLines.FindAsync(id);
            if (line != null)
            {
                _db.ProductionLines.Remove(line);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Линия удалена.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ====== API ======

        // GET /api/lines?available=true
        [HttpGet("/api/lines")]
        public async Task<IActionResult> ApiGetLines(bool available = false)
        {
            var query = _db.ProductionLines.AsQueryable();
            if (available)
                query = query.Where(l => l.Status == "Active" && l.CurrentWorkOrderId == null);

            var result = await query.Select(l => new
            {
                l.Id, l.Name, l.Status, l.EfficiencyFactor, l.CurrentWorkOrderId
            }).ToListAsync();

            return Json(result);
        }

        // PUT /api/lines/{id}/status
        [HttpPut("/api/lines/{id}/status")]
        public async Task<IActionResult> ApiSetStatus(int id, [FromBody] StatusDto dto)
        {
            var line = await _db.ProductionLines.FindAsync(id);
            if (line == null) return NotFound();
            line.Status = dto.Status;
            await _db.SaveChangesAsync();
            return Json(new { line.Id, line.Status });
        }

        // GET /api/lines/{id}/schedule
        [HttpGet("/api/lines/{id}/schedule")]
        public async Task<IActionResult> ApiGetSchedule(int id)
        {
            var orders = await _db.WorkOrders
                .Where(wo => wo.ProductionLineId == id && wo.Status != "Cancelled")
                .Include(wo => wo.Product)
                .Select(wo => new
                {
                    wo.Id, wo.Status, wo.Quantity, wo.StartDate, wo.EstimatedEndDate, wo.Progress,
                    product = wo.Product!.Name
                })
                .OrderBy(wo => wo.StartDate)
                .ToListAsync();

            return Json(orders);
        }
    }

    public record StatusDto(string Status);
}
