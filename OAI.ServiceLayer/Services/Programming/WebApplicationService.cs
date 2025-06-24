using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Programming;
using OAI.Core.Entities.Programming;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Services;
using OAI.ServiceLayer.Interfaces;

namespace OAI.ServiceLayer.Services.Programming
{
    /// <summary>
    /// Interface pro WebApplicationService
    /// </summary>
    public interface IWebApplicationService : IBaseGuidService<WebApplication>
    {
        Task<IEnumerable<WebApplicationDto>> GetAllWebApplicationsAsync();
        Task<WebApplicationDto?> GetWebApplicationByIdAsync(Guid id);
        Task<WebApplicationDto> CreateWebApplicationAsync(CreateWebApplicationDto dto);
        Task<WebApplicationDto> UpdateWebApplicationAsync(Guid id, UpdateWebApplicationDto dto);
        Task<bool> DeleteWebApplicationAsync(Guid id);
        Task<IEnumerable<WebApplicationDto>> GetWebApplicationsByStatusAsync(string status);
        Task<IEnumerable<WebApplicationDto>> GetActiveWebApplicationsAsync();
        Task<IEnumerable<WebApplicationDto>> SearchWebApplicationsAsync(string searchTerm);
    }

    /// <summary>
    /// Service pro správu webových aplikací
    /// </summary>
    public class WebApplicationService : BaseGuidService<WebApplication>, IWebApplicationService
    {
        private readonly ILogger<WebApplicationService> _logger;

        public WebApplicationService(
            IUnitOfWork unitOfWork,
            ILogger<WebApplicationService> logger) : base(unitOfWork)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<WebApplicationDto>> GetAllWebApplicationsAsync()
        {
            _logger.LogInformation("Getting all web applications");
            
            var entities = await Repository.GetAllAsync();
            return entities.Select(MapToDto).OrderBy(x => x.Name);
        }

        public async Task<WebApplicationDto?> GetWebApplicationByIdAsync(Guid id)
        {
            _logger.LogInformation("Getting web application with ID: {Id}", id);
            
            var entity = await Repository.GetByIdAsync(id);
            return entity != null ? MapToDto(entity) : null;
        }

        public async Task<WebApplicationDto> CreateWebApplicationAsync(CreateWebApplicationDto dto)
        {
            _logger.LogInformation("Creating new web application: {Name}", dto.Name);
            
            var entity = new WebApplication
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                ProjectPath = dto.ProjectPath,
                Url = dto.Url,
                ProgrammingLanguage = dto.ProgrammingLanguage,
                Framework = dto.Framework,
                Architecture = dto.Architecture,
                Database = dto.Database,
                Version = dto.Version,
                Status = dto.Status,
                GitRepository = dto.GitRepository,
                Notes = dto.Notes,
                Tags = dto.Tags,
                LastDeployment = dto.LastDeployment?.ToUniversalTime(),
                IsActive = dto.IsActive,
                Priority = dto.Priority,
                CreatedAt = DateTime.UtcNow
                // UpdatedAt will be set by AppDbContext on save
            };

            await Repository.AddAsync(entity);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Web application created successfully with ID: {Id}", entity.Id);
            return MapToDto(entity);
        }

        public async Task<WebApplicationDto> UpdateWebApplicationAsync(Guid id, UpdateWebApplicationDto dto)
        {
            _logger.LogInformation("Updating web application: {Id}", id);
            
            var entity = await Repository.GetByIdAsync(id);
            if (entity == null)
            {
                throw new InvalidOperationException($"Web application with ID {id} not found");
            }

            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.ProjectPath = dto.ProjectPath;
            entity.Url = dto.Url;
            entity.ProgrammingLanguage = dto.ProgrammingLanguage;
            entity.Framework = dto.Framework;
            entity.Architecture = dto.Architecture;
            entity.Database = dto.Database;
            entity.Version = dto.Version;
            entity.Status = dto.Status;
            entity.GitRepository = dto.GitRepository;
            entity.Notes = dto.Notes;
            entity.Tags = dto.Tags;
            entity.LastDeployment = dto.LastDeployment?.ToUniversalTime();
            entity.IsActive = dto.IsActive;
            entity.Priority = dto.Priority;
            entity.UpdatedAt = DateTime.UtcNow;

            Repository.Update(entity);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Web application updated successfully: {Id}", id);
            return MapToDto(entity);
        }

        public async Task<bool> DeleteWebApplicationAsync(Guid id)
        {
            _logger.LogInformation("Deleting web application: {Id}", id);
            
            var entity = await Repository.GetByIdAsync(id);
            if (entity == null)
            {
                return false;
            }

            await Repository.DeleteAsync(id);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Web application deleted successfully: {Id}", id);
            return true;
        }

        public async Task<IEnumerable<WebApplicationDto>> GetWebApplicationsByStatusAsync(string status)
        {
            _logger.LogInformation("Getting web applications by status: {Status}", status);
            
            var entities = await Repository.GetAllAsync();
            return entities
                .Where(x => x.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .Select(MapToDto)
                .OrderBy(x => x.Name);
        }

        public async Task<IEnumerable<WebApplicationDto>> GetActiveWebApplicationsAsync()
        {
            _logger.LogInformation("Getting active web applications");
            
            var entities = await Repository.GetAllAsync();
            return entities
                .Where(x => x.IsActive)
                .Select(MapToDto)
                .OrderBy(x => x.Name);
        }

        public async Task<IEnumerable<WebApplicationDto>> SearchWebApplicationsAsync(string searchTerm)
        {
            _logger.LogInformation("Searching web applications with term: {SearchTerm}", searchTerm);
            
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetAllWebApplicationsAsync();
            }

            var entities = await Repository.GetAllAsync();
            var lowerSearchTerm = searchTerm.ToLower();
            
            return entities
                .Where(x => 
                    x.Name.ToLower().Contains(lowerSearchTerm) ||
                    x.Description.ToLower().Contains(lowerSearchTerm) ||
                    x.ProgrammingLanguage.ToLower().Contains(lowerSearchTerm) ||
                    x.Framework.ToLower().Contains(lowerSearchTerm) ||
                    x.Tags.ToLower().Contains(lowerSearchTerm))
                .Select(MapToDto)
                .OrderBy(x => x.Name);
        }

        private static WebApplicationDto MapToDto(WebApplication entity)
        {
            return new WebApplicationDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                ProjectPath = entity.ProjectPath,
                Url = entity.Url,
                ProgrammingLanguage = entity.ProgrammingLanguage,
                Framework = entity.Framework,
                Architecture = entity.Architecture,
                Database = entity.Database,
                Version = entity.Version,
                Status = entity.Status,
                GitRepository = entity.GitRepository,
                Notes = entity.Notes,
                Tags = entity.Tags,
                LastDeployment = entity.LastDeployment,
                IsActive = entity.IsActive,
                Priority = entity.Priority,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}