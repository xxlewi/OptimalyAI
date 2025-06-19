using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.ServiceLayer.Services.AI;
using OAI.ServiceLayer.Mapping.AI;

namespace OptimalyAI.Controllers
{
    public class AiServersController : Controller
    {
        private readonly IAiServerService _aiServerService;
        private readonly IAiServerMapper _mapper;
        private readonly ILogger<AiServersController> _logger;

        public AiServersController(
            IAiServerService aiServerService,
            IAiServerMapper mapper,
            ILogger<AiServersController> logger)
        {
            _aiServerService = aiServerService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var servers = await _aiServerService.GetAllAsync();
            var serverDtos = servers.Select(s => _mapper.ToDto(s));
            return View(serverDtos);
        }

        public IActionResult Create()
        {
            return View(new CreateAiServerDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAiServerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            try
            {
                var server = _mapper.ToEntity(dto);
                await _aiServerService.CreateAsync(server);
                
                TempData["Success"] = "AI server byl úspěšně vytvořen.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AI server");
                TempData["Error"] = "Nepodařilo se vytvořit AI server.";
                return View(dto);
            }
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var server = await _aiServerService.GetByIdAsync(id);
            if (server == null)
            {
                return NotFound();
            }

            var dto = new UpdateAiServerDto
            {
                Name = server.Name,
                ServerType = server.ServerType,
                BaseUrl = server.BaseUrl,
                ApiKey = server.ApiKey,
                IsActive = server.IsActive,
                IsDefault = server.IsDefault,
                Description = server.Description,
                TimeoutSeconds = server.TimeoutSeconds,
                MaxRetries = server.MaxRetries,
                SupportsChat = server.SupportsChat,
                SupportsEmbeddings = server.SupportsEmbeddings,
                SupportsImageGeneration = server.SupportsImageGeneration
            };
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, UpdateAiServerDto dto)
        {

            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            try
            {
                var server = await _aiServerService.GetByIdAsync(id);
                if (server == null)
                {
                    return NotFound();
                }

                _mapper.UpdateEntity(server, dto);
                await _aiServerService.UpdateAsync(server);
                
                TempData["Success"] = "AI server byl úspěšně upraven.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating AI server {Id}", id);
                TempData["Error"] = "Nepodařilo se upravit AI server.";
                return View(dto);
            }
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var server = await _aiServerService.GetByIdAsync(id);
            if (server == null)
            {
                return NotFound();
            }

            var dto = _mapper.ToDto(server);
            return View(dto);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            try
            {
                await _aiServerService.DeleteAsync(id);
                TempData["Success"] = "AI server byl úspěšně smazán.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting AI server {Id}", id);
                TempData["Error"] = "Nepodařilo se smazat AI server.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> TestConnection(Guid id)
        {
            try
            {
                var result = await _aiServerService.TestConnectionAsync(id);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for server {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SetDefault(Guid id)
        {
            try
            {
                await _aiServerService.SetDefaultServerAsync(id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default server {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CheckHealth(Guid id)
        {
            try
            {
                var healthy = await _aiServerService.CheckHealthAsync(id);
                return Json(new { success = true, healthy });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health for server {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}