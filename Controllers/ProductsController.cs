using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProductionSystem.Data;
using ProductionSystem.Models;

namespace ProductionSystem.Controllers
{
    public class ProductsController : Controller
    {
        private readonly AppDbContext _db;

        public ProductsController(AppDbContext db)
        {
            _db = db;
        }

        // GET /Products
        public async Task<IActionResult> Index(string? category, string? search)
        {
            var query = _db.Products
                .Include(p => p.ProductMaterials)
                .ThenInclude(pm => pm.Material)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Name.Contains(search));

            ViewBag.Categories = await _db.Products
                .Where(p => p.Category != null)
                .Select(p => p.Category!)
                .Distinct()
                .ToListAsync();

            ViewBag.SelectedCategory = category;
            ViewBag.Search = search;

            return View(await query.OrderBy(p => p.Name).ToListAsync());
        }

        // GET /Products/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Materials = await _db.Materials.ToListAsync();
            return View(new Product());
        }

        // POST /Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, int[]? materialIds, decimal[]? quantities)
        {
            if (ModelState.IsValid)
            {
                _db.Products.Add(product);
                await _db.SaveChangesAsync();

                if (materialIds != null)
                {
                    for (int i = 0; i < materialIds.Length; i++)
                    {
                        _db.ProductMaterials.Add(new ProductMaterial
                        {
                            ProductId = product.Id,
                            MaterialId = materialIds[i],
                            QuantityNeeded = quantities != null && i < quantities.Length ? quantities[i] : 1
                        });
                    }
                    await _db.SaveChangesAsync();
                }

                TempData["Success"] = $"Продукт «{product.Name}» создан.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Materials = await _db.Materials.ToListAsync();
            return View(product);
        }

        // GET /Products/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _db.Products
                .Include(p => p.ProductMaterials)
                .ThenInclude(pm => pm.Material)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            ViewBag.Materials = await _db.Materials.ToListAsync();
            return View(product);
        }

        // POST /Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        // 1. Изменяем decimal[] на string[] для quantities
        public async Task<IActionResult> Edit(int id, Product product, int[]? materialIds, string[]? quantities)
        {
            if (id != product.Id) return BadRequest();

            // 2. Удаляем ошибки валидации для вложенных материалов, 
            // так как мы их обрабатываем вручную ниже
            ModelState.Remove("ProductMaterials");

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Products.Update(product);

                    // Удаляем старые связи
                    var existing = _db.ProductMaterials.Where(pm => pm.ProductId == id);
                    _db.ProductMaterials.RemoveRange(existing);

                    // Сохраняем изменения удаления перед добавлением новых (надежнее)
                    await _db.SaveChangesAsync();

                    if (materialIds != null && quantities != null)
                    {
                        for (int i = 0; i < materialIds.Length; i++)
                        {
                            // 3. Безопасный парсинг строки в decimal с поддержкой точки и запятой
                            string rawVal = quantities[i].Replace(',', '.');
                            if (decimal.TryParse(rawVal,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out decimal parsedQty))
                            {
                                _db.ProductMaterials.Add(new ProductMaterial
                                {
                                    ProductId = id,
                                    MaterialId = materialIds[i],
                                    QuantityNeeded = parsedQty
                                });
                            }
                        }
                    }

                    await _db.SaveChangesAsync();
                    TempData["Success"] = $"Продукт «{product.Name}» обновлён.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Ошибка при сохранении: " + ex.Message);
                }
            }

            // Если данные невалидны, возвращаем материалы для повторного отображения формы
            ViewBag.Materials = await _db.Materials.ToListAsync();

            // Чтобы при ошибке введенные материалы не пропадали из списка на странице:
            if (materialIds != null)
            {
                product.ProductMaterials = materialIds.Select((mid, idx) => new ProductMaterial
                {
                    MaterialId = mid,
                    QuantityNeeded = decimal.TryParse(quantities?[idx].Replace(',', '.'), out var d) ? d : 0
                }).ToList();
            }

            return View(product);
        }

        // POST /Products/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product != null)
            {
                _db.Products.Remove(product);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Продукт удалён.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ====== API ======

        // GET /api/products?category={cat}
        [HttpGet("/api/products")]
        public async Task<IActionResult> ApiGetProducts(string? category)
        {
            var query = _db.Products.AsQueryable();
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category);

            var result = await query.Select(p => new
            {
                p.Id, p.Name, p.Category, p.ProductionTimePerUnit, p.MinimalStock
            }).ToListAsync();

            return Json(result);
        }

        // GET /api/products/{id}/materials
        [HttpGet("/api/products/{id}/materials")]
        public async Task<IActionResult> ApiGetProductMaterials(int id)
        {
            var mats = await _db.ProductMaterials
                .Where(pm => pm.ProductId == id)
                .Include(pm => pm.Material)
                .Select(pm => new
                {
                    pm.MaterialId,
                    pm.Material!.Name,
                    pm.QuantityNeeded,
                    pm.Material.UnitOfMeasure,
                    available = pm.Material.Quantity
                }).ToListAsync();

            return Json(mats);
        }

        // POST /api/products
        [HttpPost("/api/products")]
        public async Task<IActionResult> ApiCreateProduct([FromBody] ApiProductDto dto)
        {
            var p = new Product
            {
                Name = dto.Name,
                ProductionTimePerUnit = dto.ProdTime,
                Category = dto.Category
            };
            _db.Products.Add(p);
            await _db.SaveChangesAsync();
            return Json(new { p.Id, p.Name });
        }

        // POST /api/calculate/production
        [HttpPost("/api/calculate/production")]
        public async Task<IActionResult> ApiCalculateProduction([FromBody] CalcDto dto)
        {
            var product = await _db.Products.FindAsync(dto.ProductId);
            if (product == null) return NotFound();

            double minutes = (double)(product.ProductionTimePerUnit * dto.Quantity);
            return Json(new { minutes, hours = Math.Round(minutes / 60, 1) });
        }
    }

    public record ApiProductDto(string Name, int ProdTime, string Category);
    public record CalcDto(int ProductId, int Quantity);
}
