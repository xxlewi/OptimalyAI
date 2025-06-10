using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Mapping.Projects;

namespace OAI.ServiceLayer.Services.Projects
{
    public interface IProjectMetricsService
    {
        Task<IEnumerable<ProjectMetricDto>> GetByProjectIdAsync(Guid projectId, string metricType = null);
        Task<ProjectMetricDto> CreateAsync(CreateProjectMetricDto dto);
        Task<ProjectBillingReportDto> GetBillingReportAsync(Guid projectId, DateTime periodStart, DateTime periodEnd);
        Task RecordToolUsageAsync(Guid projectId, string toolId, decimal executionTime, decimal cost);
        Task RecordExecutionMetricsAsync(Guid executionId);
        Task<Dictionary<string, decimal>> GetAggregatedMetricsAsync(Guid projectId, string period);
    }

    public class ProjectMetricsService : IProjectMetricsService
    {
        private readonly IGuidRepository<ProjectMetric> _metricRepository;
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IGuidRepository<ProjectExecution> _executionRepository;
        private readonly IGuidRepository<ProjectTool> _toolRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectMetricMapper _metricMapper;
        private readonly ILogger<ProjectMetricsService> _logger;

        public ProjectMetricsService(
            IGuidRepository<ProjectMetric> metricRepository,
            IGuidRepository<Project> projectRepository,
            IGuidRepository<ProjectExecution> executionRepository,
            IGuidRepository<ProjectTool> toolRepository,
            IUnitOfWork unitOfWork,
            IProjectMetricMapper metricMapper,
            ILogger<ProjectMetricsService> logger)
        {
            _metricRepository = metricRepository;
            _projectRepository = projectRepository;
            _executionRepository = executionRepository;
            _toolRepository = toolRepository;
            _unitOfWork = unitOfWork;
            _metricMapper = metricMapper;
            _logger = logger;
        }

        public async Task<IEnumerable<ProjectMetricDto>> GetByProjectIdAsync(Guid projectId, string metricType = null)
        {
            var query = _metricRepository.GetAsync(
                filter: m => m.ProjectId == projectId);

            if (!string.IsNullOrEmpty(metricType))
            {
                query = _metricRepository.GetAsync(
                    filter: m => m.ProjectId == projectId && m.MetricType == metricType);
            }

            var metrics = await query.OrderByDescending(m => m.MeasuredAt).ToListAsync();
            return metrics.Select(_metricMapper.ToDto);
        }

        public async Task<ProjectMetricDto> CreateAsync(CreateProjectMetricDto dto)
        {
            var projectExists = await _projectRepository.ExistsAsync(p => p.Id == dto.ProjectId);
            if (!projectExists)
                throw new NotFoundException("Project", dto.ProjectId);

            var metric = _metricMapper.ToEntity(dto);
            metric.CreatedAt = DateTime.UtcNow;
            metric.UpdatedAt = DateTime.UtcNow;

            await _metricRepository.CreateAsync(metric);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created metric {MetricName} for project {ProjectId}", 
                metric.MetricName, metric.ProjectId);

            return _metricMapper.ToDto(metric);
        }

        public async Task<ProjectBillingReportDto> GetBillingReportAsync(Guid projectId, DateTime periodStart, DateTime periodEnd)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == projectId)
                .FirstOrDefaultAsync();

            if (project == null)
                throw new NotFoundException("Project", projectId);

            // Získání metrik za období
            var metrics = await _metricRepository.GetAsync(
                filter: m => m.ProjectId == projectId 
                    && m.MeasuredAt >= periodStart 
                    && m.MeasuredAt <= periodEnd
                    && m.IsBillable)
                .ToListAsync();

            // Výpočet hodin
            var hoursMetrics = metrics.Where(m => m.Unit == "hours").ToList();
            var totalHours = hoursMetrics.Sum(m => m.Value);
            var hourlyAmount = project.HourlyRate.HasValue 
                ? totalHours * project.HourlyRate.Value 
                : 0;

            // Náklady na nástroje
            var toolCosts = metrics.Where(m => m.MetricType == "ToolUsage" && m.Unit == "CZK")
                .Sum(m => m.BillingAmount ?? 0);

            // API volání
            var apiCosts = metrics.Where(m => m.MetricType == "ApiCall" && m.Unit == "CZK")
                .Sum(m => m.BillingAmount ?? 0);

            var report = new ProjectBillingReportDto
            {
                ProjectId = projectId,
                ProjectName = project.Name,
                CustomerName = project.CustomerName,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TotalHours = totalHours,
                HourlyRate = project.HourlyRate ?? 0,
                HourlyAmount = hourlyAmount,
                ToolUsageCost = toolCosts,
                ApiCallsCost = apiCosts,
                TotalAmount = hourlyAmount + toolCosts + apiCosts,
                LineItems = new List<BillingLineItemDto>()
            };

            // Detailní položky
            // Hodiny
            if (totalHours > 0)
            {
                report.LineItems.Add(new BillingLineItemDto
                {
                    Description = "Vývojové hodiny",
                    Category = "Labor",
                    Quantity = totalHours,
                    Unit = "hours",
                    UnitPrice = project.HourlyRate ?? 0,
                    Amount = hourlyAmount,
                    Date = periodEnd
                });
            }

            // Nástroje - agregované podle typu
            var toolItems = metrics
                .Where(m => m.MetricType == "ToolUsage" && m.IsBillable)
                .GroupBy(m => m.MetricName)
                .Select(g => new BillingLineItemDto
                {
                    Description = $"Použití nástroje: {g.Key}",
                    Category = "ToolUsage",
                    Quantity = g.Sum(m => m.Value),
                    Unit = "calls",
                    UnitPrice = g.First().BillingRate ?? 0,
                    Amount = g.Sum(m => m.BillingAmount ?? 0),
                    Date = g.Max(m => m.MeasuredAt)
                });

            report.LineItems.AddRange(toolItems);

            return report;
        }

        public async Task RecordToolUsageAsync(Guid projectId, string toolId, decimal executionTime, decimal cost)
        {
            var metric = new CreateProjectMetricDto
            {
                ProjectId = projectId,
                MetricType = "ToolUsage",
                MetricName = $"Tool_{toolId}",
                Value = 1, // Počet použití
                Unit = "calls",
                Period = "Hour",
                IsBillable = cost > 0,
                BillingRate = cost,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    toolId,
                    executionTime,
                    cost,
                    timestamp = DateTime.UtcNow
                })
            };

            await CreateAsync(metric);

            // Aktualizace statistik nástroje
            var projectTool = await _toolRepository.GetAsync(
                filter: t => t.ProjectId == projectId && t.ToolId == toolId)
                .FirstOrDefaultAsync();

            if (projectTool != null)
            {
                projectTool.TotalUsageCount++;
                projectTool.TodayUsageCount++;
                projectTool.LastUsedAt = DateTime.UtcNow;

                // Aktualizace průměrné doby zpracování
                if (projectTool.AverageExecutionTime.HasValue)
                {
                    projectTool.AverageExecutionTime = 
                        (projectTool.AverageExecutionTime.Value * (projectTool.TotalUsageCount - 1) + (double)executionTime) 
                        / projectTool.TotalUsageCount;
                }
                else
                {
                    projectTool.AverageExecutionTime = (double)executionTime;
                }

                await _toolRepository.UpdateAsync(projectTool);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task RecordExecutionMetricsAsync(Guid executionId)
        {
            var execution = await _executionRepository.GetAsync(
                filter: e => e.Id == executionId)
                .FirstOrDefaultAsync();

            if (execution == null || !execution.DurationSeconds.HasValue)
                return;

            // Záznam doby běhu
            await CreateAsync(new CreateProjectMetricDto
            {
                ProjectId = execution.ProjectId,
                MetricType = "Performance",
                MetricName = "ExecutionTime",
                Value = (decimal)execution.DurationSeconds.Value,
                Unit = "seconds",
                Period = "Execution",
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    executionId = execution.Id,
                    workflowId = execution.WorkflowId,
                    status = execution.Status.ToString()
                })
            });

            // Záznam zpracovaných položek
            if (execution.ItemsProcessedCount > 0)
            {
                await CreateAsync(new CreateProjectMetricDto
                {
                    ProjectId = execution.ProjectId,
                    MetricType = "Usage",
                    MetricName = "ItemsProcessed",
                    Value = execution.ItemsProcessedCount,
                    Unit = "items",
                    Period = "Execution"
                });
            }

            // Záznam nákladů
            if (execution.ExecutionCost.HasValue && execution.ExecutionCost > 0)
            {
                await CreateAsync(new CreateProjectMetricDto
                {
                    ProjectId = execution.ProjectId,
                    MetricType = "Cost",
                    MetricName = "ExecutionCost",
                    Value = execution.ExecutionCost.Value,
                    Unit = "CZK",
                    Period = "Execution",
                    IsBillable = true,
                    BillingRate = 1
                });
            }
        }

        public async Task<Dictionary<string, decimal>> GetAggregatedMetricsAsync(Guid projectId, string period)
        {
            var startDate = GetPeriodStartDate(period);
            
            var metrics = await _metricRepository.GetAsync(
                filter: m => m.ProjectId == projectId && m.MeasuredAt >= startDate)
                .ToListAsync();

            var aggregated = metrics
                .GroupBy(m => m.MetricType)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(m => m.Value)
                );

            return aggregated;
        }

        private DateTime GetPeriodStartDate(string period)
        {
            return period?.ToLower() switch
            {
                "hour" => DateTime.UtcNow.AddHours(-1),
                "day" => DateTime.UtcNow.AddDays(-1),
                "week" => DateTime.UtcNow.AddDays(-7),
                "month" => DateTime.UtcNow.AddMonths(-1),
                "year" => DateTime.UtcNow.AddYears(-1),
                _ => DateTime.UtcNow.AddDays(-30) // Default 30 dní
            };
        }
    }
}