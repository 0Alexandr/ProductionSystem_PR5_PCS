using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionSystem.Data;
using ProductionSystem.Models;

namespace ProductionSystem.Controllers
{
    public class OrdersController : Controller
    {
        private readonly AppDbContext _db;

        public OrdersController(AppDbContext db)
        {
            _db = db;
        }

        // GET /Orders
        public async Task<IActionResult> Index(string? status, string? date)
        {
            var query = _db.WorkOrders
                .Include(o => o.Product)
                .Include(o => o.ProductionLine)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
                query = query.Where(o => o.Status == status);

            if (date == "today")
                query = query.Where(o => o.StartDate.Date == DateTime.Today);

            ViewBag.StatusFilter = status;
            ViewBag.DateFilter = date;

            return View(await query.OrderByDescending(o => o.StartDate).ToListAsync());
        }

        // GET /Orders/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Products = await _db.Products.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Lines = await _db.ProductionLines
                .Where(l => l.Status == "Active")
                .ToListAsync();
            return View(new WorkOrder { StartDate = DateTime.Now });
        }

        // POST /Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WorkOrder order)
        {
            if (ModelState.IsValid)
            {
                var product = await _db.Products
                    .Include(p => p.ProductMaterials)
                    .ThenInclude(pm => pm.Material)
                    .FirstOrDefaultAsync(p => p.Id == order.ProductId);

                if (product != null)
                {
                    var shortages = new List<string>();
                    foreach (var pm in product.ProductMaterials)
                    {
                        var needed = pm.QuantityNeeded * order.Quantity;
                        if (pm.Material!.Quantity < needed)
                            shortages.Add($"«{pm.Material.Name}»: нужно {needed}, доступно {pm.Material.Quantity} {pm.Material.UnitOfMeasure}");
                    }
                    if (shortages.Any())
                        TempData["Warning"] = "Недостаточно материалов: " + string.Join("; ", shortages);

                    var line = order.ProductionLineId.HasValue
                        ? await _db.ProductionLines.FindAsync(order.ProductionLineId)
                        : null;
                    var efficiency = line?.EfficiencyFactor ?? 1.0f;
                    var totalMinutes = (product.ProductionTimePerUnit * order.Quantity) / efficiency;
                    order.EstimatedEndDate = order.StartDate.AddMinutes(totalMinutes);
                }

                _db.WorkOrders.Add(order);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Заказ #{order.Id} создан.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Products = await _db.Products.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Lines = await _db.ProductionLines.Where(l => l.Status == "Active").ToListAsync();
            return View(order);
        }

        // GET /Orders/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var order = await _db.WorkOrders
                .Include(o => o.Product)
                .ThenInclude(p => p!.ProductMaterials)
                .ThenInclude(pm => pm.Material)
                .Include(o => o.ProductionLine)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // POST /Orders/Launch/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Launch(int id)
        {
            var order = await _db.WorkOrders
                .Include(o => o.Product)
                .ThenInclude(p => p!.ProductMaterials)
                .ThenInclude(pm => pm.Material)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.Status == "Pending")
            {
                var now = DateTime.Now;
                order.Status = "InProgress";
                order.ActualStartDate = now;
                order.StartDate = now;

                var line = order.ProductionLineId.HasValue
                    ? await _db.ProductionLines.FindAsync(order.ProductionLineId)
                    : null;

                if (order.Product != null)
                {
                    var efficiency = line?.EfficiencyFactor ?? 1.0f;
                    var totalMinutes = (order.Product.ProductionTimePerUnit * order.Quantity) / efficiency;
                    order.EstimatedEndDate = now.AddMinutes(totalMinutes);
                }

                if (line != null)
                {
                    line.CurrentWorkOrderId = order.Id;
                    line.Status = "Active";
                }

                // Deduct materials
                if (order.Product != null)
                {
                    foreach (var pm in order.Product.ProductMaterials)
                    {
                        pm.Material!.Quantity -= pm.QuantityNeeded * order.Quantity;
                        if (pm.Material.Quantity < 0) pm.Material.Quantity = 0;
                    }
                }

                await _db.SaveChangesAsync();
                TempData["Success"] = $"Заказ #{order.Id} запущен в производство.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST /Orders/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var order = await _db.WorkOrders
                .Include(o => o.Product)
                .ThenInclude(p => p!.ProductMaterials)
                .ThenInclude(pm => pm.Material)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.Status != "Completed" && order.Status != "Cancelled")
            {
                string refundMsg = "";

                if (order.Status == "InProgress" && order.Product != null && order.ActualStartDate.HasValue)
                {
                    // Получаем коэффициент эффективности линии
                    var line = order.ProductionLineId.HasValue
                        ? await _db.ProductionLines.FindAsync(order.ProductionLineId)
                        : null;
                    var efficiency = (double)(line?.EfficiencyFactor ?? 1.0f);

                    // Время на одну единицу с учётом эффективности (в минутах)
                    var timePerUnitAdjusted = order.Product.ProductionTimePerUnit / efficiency;

                    // Сколько минут прошло с фактического запуска
                    var elapsedMinutes = (DateTime.Now - order.ActualStartDate.Value).TotalMinutes;

                    // Сколько штук произведено (дробное)
                    double producedUnits = timePerUnitAdjusted > 0
                        ? elapsedMinutes / timePerUnitAdjusted
                        : order.Quantity;
                    producedUnits = Math.Min(producedUnits, order.Quantity);
                    producedUnits = Math.Max(producedUnits, 0);

                    // Непроизведённая часть → возврат
                    double unproducedUnits = order.Quantity - producedUnits;

                    var refundDetails = new List<string>();
                    foreach (var pm in order.Product.ProductMaterials)
                    {
                        var refund = (decimal)unproducedUnits * pm.QuantityNeeded;
                        if (refund > 0)
                        {
                            pm.Material!.Quantity += refund;
                            refundDetails.Add($"{pm.Material.Name}: +{refund:F3} {pm.Material.UnitOfMeasure}");
                        }
                    }

                    order.Progress = (int)Math.Round(producedUnits / order.Quantity * 100);

                    if (refundDetails.Any())
                        refundMsg = " Возвращено: " + string.Join(", ", refundDetails) + ".";
                }

                order.Status = "Cancelled";

                if (order.ProductionLineId.HasValue)
                {
                    var line = await _db.ProductionLines.FindAsync(order.ProductionLineId);
                    if (line != null && line.CurrentWorkOrderId == order.Id)
                        line.CurrentWorkOrderId = null;
                }

                await _db.SaveChangesAsync();
                TempData["Success"] = $"Заказ #{order.Id} отменён." + refundMsg;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST /Orders/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var order = await _db.WorkOrders.FindAsync(id);
            if (order == null) return NotFound();

            if (order.Status == "InProgress")
            {
                order.Status = "Completed";
                order.Progress = 100;

                if (order.ProductionLineId.HasValue)
                {
                    var line = await _db.ProductionLines.FindAsync(order.ProductionLineId);
                    if (line != null && line.CurrentWorkOrderId == order.Id)
                        line.CurrentWorkOrderId = null;
                }

                await _db.SaveChangesAsync();
                TempData["Success"] = $"Заказ #{order.Id} завершён.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _db.WorkOrders.FindAsync(id);

            // Проверяем, существует ли заказ и можно ли его удалять (статус Завершен или Отменен)
            if (order != null && (order.Status == "Completed" || order.Status == "Cancelled"))
            {
                _db.WorkOrders.Remove(order);
                await _db.SaveChangesAsync();
            }

            // Возвращаемся на главную страницу заказов
            return RedirectToAction(nameof(Index));
        }

        // ====== API ======

        [HttpGet("/api/orders")]
        public async Task<IActionResult> ApiGetOrders(string? status, string? date)
        {
            var query = _db.WorkOrders.Include(o => o.Product).Include(o => o.ProductionLine).AsQueryable();
            if (status == "active") query = query.Where(o => o.Status == "InProgress");
            if (date == "today") query = query.Where(o => o.StartDate.Date == DateTime.Today);

            var result = await query.Select(o => new
            {
                o.Id,
                o.Status,
                o.Quantity,
                o.StartDate,
                o.EstimatedEndDate,
                o.Progress,
                product = o.Product!.Name,
                line = o.ProductionLine != null ? o.ProductionLine.Name : null
            }).ToListAsync();

            return Json(result);
        }

        [HttpPost("/api/orders")]
        public async Task<IActionResult> ApiCreateOrder([FromBody] ApiOrderDto dto)
        {
            var product = await _db.Products.FindAsync(dto.ProductId);
            if (product == null) return BadRequest("Product not found");

            var line = dto.LineId.HasValue ? await _db.ProductionLines.FindAsync(dto.LineId) : null;
            var efficiency = line?.EfficiencyFactor ?? 1.0f;
            var totalMinutes = (product.ProductionTimePerUnit * dto.Quantity) / efficiency;

            var order = new WorkOrder
            {
                ProductId = dto.ProductId,
                ProductionLineId = dto.LineId,
                Quantity = dto.Quantity,
                StartDate = DateTime.Now,
                EstimatedEndDate = DateTime.Now.AddMinutes(totalMinutes),
                Status = "Pending"
            };
            _db.WorkOrders.Add(order);
            await _db.SaveChangesAsync();
            return Json(new { order.Id, order.Status, order.EstimatedEndDate });
        }

        // PUT /api/orders/{id}/progress  — ручное обновление
        [HttpPut("/api/orders/{id}/progress")]
        public async Task<IActionResult> ApiUpdateProgress(int id, [FromBody] ProgressDto dto)
        {
            var order = await _db.WorkOrders.FindAsync(id);
            if (order == null) return NotFound();
            order.Progress = Math.Clamp(dto.Percent, 0, 100);
            await _db.SaveChangesAsync();
            return Json(new { order.Id, order.Progress });
        }

        // GET /api/orders/{id}/progress/auto — авторасчёт по времени (вызывается каждую секунду)
        [HttpGet("/api/orders/{id}/progress/auto")]
        public async Task<IActionResult> ApiAutoProgress(int id)
        {
            var order = await _db.WorkOrders
                .Include(o => o.Product)
                .Include(o => o.ProductionLine)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.Status != "InProgress" || !order.ActualStartDate.HasValue)
                return Json(new { order.Id, order.Progress, order.Status, autoCalc = false });

            var efficiency = (double)(order.ProductionLine?.EfficiencyFactor ?? 1.0f);
            var totalMinutes = order.Product!.ProductionTimePerUnit * order.Quantity / efficiency;
            var elapsed = (DateTime.Now - order.ActualStartDate.Value).TotalMinutes;

            int newProgress = totalMinutes > 0
                ? (int)Math.Min(Math.Round(elapsed / totalMinutes * 100), 100)
                : 100;

            if (newProgress >= 100)
            {
                order.Progress = 100;
                order.Status = "Completed";
                if (order.ProductionLineId.HasValue)
                {
                    var line = await _db.ProductionLines.FindAsync(order.ProductionLineId);
                    if (line != null && line.CurrentWorkOrderId == order.Id)
                        line.CurrentWorkOrderId = null;
                }
            }
            else
            {
                order.Progress = newProgress;
            }

            await _db.SaveChangesAsync();

            return Json(new
            {
                order.Id,
                order.Progress,
                order.Status,
                autoCalc = true,
                totalMinutes = Math.Round(totalMinutes, 1),
                elapsedMinutes = Math.Round(elapsed, 2)
            });
        }

        // GET /api/orders/{id}/details
        [HttpGet("/api/orders/{id}/details")]
        public async Task<IActionResult> ApiGetDetails(int id)
        {
            var order = await _db.WorkOrders
                .Include(o => o.Product)
                .Include(o => o.ProductionLine)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return Json(new
            {
                order.Id,
                order.Status,
                order.Quantity,
                order.StartDate,
                order.EstimatedEndDate,
                order.Progress,
                order.ActualStartDate,
                product = new { order.Product!.Id, order.Product.Name, order.Product.ProductionTimePerUnit },
                line = order.ProductionLine != null ? new { order.ProductionLine.Id, order.ProductionLine.Name } : null
            });
        }
    }

    public record ApiOrderDto(int ProductId, int Quantity, int? LineId);
    public record ProgressDto(int Percent);
}