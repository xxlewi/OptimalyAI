using Microsoft.AspNetCore.Mvc;
using OAI.Core.Interfaces.Adapters;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OptimalyAI.ViewComponents
{
    public class AdapterSelectorViewComponent : ViewComponent
    {
        private readonly IAdapterRegistry _adapterRegistry;

        public AdapterSelectorViewComponent(IAdapterRegistry adapterRegistry)
        {
            _adapterRegistry = adapterRegistry;
        }

        public async Task<IViewComponentResult> InvokeAsync(
            string elementId,
            string label,
            AdapterType adapterType,
            string? existingConfiguration = null)
        {
            var adapters = await _adapterRegistry.GetAllAdaptersAsync();
            
            // Filter by type
            var filteredAdapters = adapters
                .Where(a => a.Type == adapterType || a.Type == AdapterType.Bidirectional)
                .ToList();

            // Group by category
            var adaptersByCategory = filteredAdapters
                .GroupBy(a => a.Category)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(a => new AdapterInfo
                    {
                        Id = a.Id,
                        Name = a.Name,
                        Description = a.Description,
                        Category = a.Category,
                        Parameters = a.Parameters.Select(p => new ParameterInfo
                        {
                            Name = p.Name,
                            DisplayName = p.DisplayName,
                            Description = p.Description,
                            Type = p.Type.ToString(),
                            IsRequired = p.IsRequired,
                            DefaultValue = p.DefaultValue,
                            UIHints = p.UIHints != null ? new UIHintsInfo
                            {
                                InputType = p.UIHints.InputType.ToString(),
                                Placeholder = p.UIHints.Placeholder,
                                HelpText = p.UIHints.HelpText,
                                Rows = p.UIHints.Rows,
                                Step = p.UIHints.Step,
                                CustomHints = p.UIHints.CustomHints
                            } : null,
                            Validation = p.Validation != null ? new ValidationInfo
                            {
                                MinValue = p.Validation.MinValue,
                                MaxValue = p.Validation.MaxValue,
                                MinLength = p.Validation.MinLength,
                                MaxLength = p.Validation.MaxLength,
                                Pattern = p.Validation.Pattern,
                                AllowedValues = p.Validation.AllowedValues?.ToList()
                            } : null
                        }).ToList()
                    }).ToList()
                );

            var model = new AdapterSelectorViewModel
            {
                ElementId = elementId,
                Label = label,
                AdapterType = adapterType.ToString(),
                AdaptersByCategory = adaptersByCategory,
                ExistingConfiguration = existingConfiguration
            };

            return View("_AdapterSelector", model);
        }
    }

    public class AdapterSelectorViewModel
    {
        public string ElementId { get; set; } = "";
        public string Label { get; set; } = "";
        public string AdapterType { get; set; } = "";
        public Dictionary<string, List<AdapterInfo>> AdaptersByCategory { get; set; } = new();
        public string? ExistingConfiguration { get; set; }
    }

    public class AdapterInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public List<ParameterInfo> Parameters { get; set; } = new();
    }

    public class ParameterInfo
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
        public bool IsRequired { get; set; }
        public object? DefaultValue { get; set; }
        public UIHintsInfo? UIHints { get; set; }
        public ValidationInfo? Validation { get; set; }
    }

    public class UIHintsInfo
    {
        public string InputType { get; set; } = "";
        public string? Placeholder { get; set; }
        public string? HelpText { get; set; }
        public int? Rows { get; set; }
        public double? Step { get; set; }
        public Dictionary<string, object>? CustomHints { get; set; }
    }

    public class ValidationInfo
    {
        public object? MinValue { get; set; }
        public object? MaxValue { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string? Pattern { get; set; }
        public List<object>? AllowedValues { get; set; }
    }
}