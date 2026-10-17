using MedCare_Pharmacy.Models.Data;
using MedCare_Pharmacy.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedCare_Pharmacy.ViewComponents
{
    public sealed class FeaturedCategoriesViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FeaturedCategoriesViewComponent> _logger;

        public FeaturedCategoriesViewComponent(
            AppDbContext context,
            ILogger<FeaturedCategoriesViewComponent> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                var categories = await _context.Categories
                    .AsNoTracking()
                    .Where(category => category.Name != "")
                    .OrderBy(category => category.Id)
                    .Select(category => new { category.Id, category.Name })
                    .ToListAsync();

                var model = categories.Select(category => new FeaturedCategoryViewModel
                {
                    Id = category.Id,
                    Name = category.Name,
                    Icon = GetIcon(category.Name)
                }).ToList();

                return View(model);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Không thể tải danh mục nổi bật.");
                return View(new List<FeaturedCategoryViewModel>());
            }
        }

        private static string GetIcon(string name)
        {
            var normalized = name.Trim().ToLowerInvariant();

            if (normalized.Contains("sơ cứu")) return "🩹";
            if (normalized.Contains("dị ứng")) return "🤧";
            if (normalized.Contains("da")) return "🧴";
            if (normalized.Contains("vitamin") || normalized.Contains("khoáng")) return "🍊";
            if (normalized.Contains("tiêu hóa")) return "🌿";
            if (normalized.Contains("tai") || normalized.Contains("mũi") || normalized.Contains("họng")) return "👂";
            if (normalized.Contains("đau")) return "💊";
            if (normalized.Contains("sức khỏe")) return "💪";

            return "💊";
        }
    }
}
