using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Controller for rendering ViewComponents via AJAX
    /// </summary>
    public class ComponentsController : Controller
    {
        /// <summary>
        /// Render AdapterSelector ViewComponent
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AdapterSelector(
            string elementId,
            string label,
            string adapterType,
            string? existingConfiguration = null)
        {
            // Parse adapter type enum
            OAI.Core.Interfaces.Adapters.AdapterType type;
            if (!System.Enum.TryParse<OAI.Core.Interfaces.Adapters.AdapterType>(adapterType, out type))
            {
                type = OAI.Core.Interfaces.Adapters.AdapterType.Input; // Default
            }

            return ViewComponent("AdapterSelector", new
            {
                elementId = elementId,
                label = label,
                adapterType = type,
                existingConfiguration = existingConfiguration
            });
        }
    }
}