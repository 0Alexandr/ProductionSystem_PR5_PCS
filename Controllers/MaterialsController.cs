using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionSystem.Data;
using ProductionSystem.Models;

namespace ProductionSystem.Controllers
{
    public class MaterialsController : Controller
    {
        private readonly AppDbContext _db;

        public MaterialsController(AppDbContext db)
        {
            _db = db;
        }

        // GET /Materials
        public async Task<IActionResult> Index(bool lowStock = false)
        {
            var query = _db.Materials.AsQueryable();
            if (lowStock)
                query = query.Where(m => m.Quantity <= m.MinimalStock);

            ViewBag.LowStock = lowStock;
            var materials = await query.OrderBy(m => m.Name).ToListAsync();
            return View(materials);
        }

        // GET /Materials/Create
        public IActionResult Create()
        {
            return View(new Material());
        }

        // POST /Materials/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Material material)
        {
            if (ModelState.IsValid)
            {
                _db.Materials.Add(material);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Материал «{material.Name}» добавлен.";
                return RedirectToAction(nameof(Index));
            }
            return View(material);
        }

        // GET /Materials/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var material = await _db.Materials.FindAsync(id);
            if (material == null) return NotFound();
            return View(material);
        }

        // POST /Materials/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Material material)
        {
            if (id != material.Id) return BadRequest();
            if (ModelState.IsValid)
            {
                _db.Materials.Update(material);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Материал «{material.Name}» обновлён.";
                return RedirectToAction(nameof(Index));
            }
            return View(material);
        }

        // POST /Materials/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var material = await _db.Materials.FindAsync(id);
            if (material != null)
            {
                _db.Materials.Remove(material);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Материал удалён.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST /Materials/Replenish/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Replenish(int id, decimal amount)
        {
            var material = await _db.Materials.FindAsync(id);
            if (material == null) return NotFound();
            material.Quantity += amount;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Запас «{material.Name}» пополнен на {amount} {material.UnitOfMeasure}.";
            return RedirectToAction(nameof(Index));
        }

        // ====== API ======

        // GET /api/materials?low_stock=true
        [HttpGet("/api/materials")]
        public async Task<IActionResult> ApiGetMaterials(bool low_stock = false)
        {
            var query = _db.Materials.AsQueryable();
            if (low_stock)
                query = query.Where(m => m.Quantity <= m.MinimalStock);

            var result = await query.Select(m => new
            {
                m.Id, m.Name, m.Quantity, m.UnitOfMeasure, m.MinimalStock,
                isLowStock = m.Quantity <= m.MinimalStock
            }).ToListAsync();

            return Json(result);
        }

        // POST /api/materials
        [HttpPost("/api/materials")]
        public async Task<IActionResult> ApiCreateMaterial([FromBody] ApiMaterialDto dto)
        {
            var mat = new Material
            {
                Name = dto.Name,
                Quantity = dto.Quantity,
                UnitOfMeasure = dto.Unit,
                MinimalStock = dto.MinStock
            };
            _db.Materials.Add(mat);
            await _db.SaveChangesAsync();
            return Json(new { mat.Id, mat.Name, mat.Quantity });
        }

        // PUT /api/materials/{id}/stock
        [HttpPut("/api/materials/{id}/stock")]
        public async Task<IActionResult> ApiUpdateStock(int id, [FromBody] StockUpdateDto dto)
        {
            var mat = await _db.Materials.FindAsync(id);
            if (mat == null) return NotFound();
            mat.Quantity = dto.Amount;
            await _db.SaveChangesAsync();
            return Json(new { mat.Id, mat.Quantity });
        }
    }

    public record ApiMaterialDto(string Name, decimal Quantity, string Unit, decimal MinStock);
    public record StockUpdateDto(decimal Amount);
}
