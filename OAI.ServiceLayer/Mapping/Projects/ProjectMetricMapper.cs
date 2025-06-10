using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public interface IProjectMetricMapper : IMapper<ProjectMetric, ProjectMetricDto>
    {
        ProjectMetric ToEntity(CreateProjectMetricDto dto);
    }

    public class ProjectMetricMapper : BaseMapper<ProjectMetric, ProjectMetricDto>, IProjectMetricMapper
    {
        public override ProjectMetricDto ToDto(ProjectMetric entity)
        {
            if (entity == null) return null;

            return new ProjectMetricDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ProjectId = entity.ProjectId,
                MetricType = entity.MetricType,
                MetricName = entity.MetricName,
                Value = entity.Value,
                Unit = entity.Unit,
                MeasuredAt = entity.MeasuredAt,
                Period = entity.Period,
                Metadata = entity.Metadata,
                IsBillable = entity.IsBillable,
                BillingRate = entity.BillingRate,
                BillingAmount = entity.BillingAmount
            };
        }

        public override ProjectMetric ToEntity(ProjectMetricDto dto)
        {
            if (dto == null) return null;

            return new ProjectMetric
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                ProjectId = dto.ProjectId,
                MetricType = dto.MetricType,
                MetricName = dto.MetricName,
                Value = dto.Value,
                Unit = dto.Unit,
                MeasuredAt = dto.MeasuredAt,
                Period = dto.Period,
                Metadata = dto.Metadata,
                IsBillable = dto.IsBillable,
                BillingRate = dto.BillingRate,
                BillingAmount = dto.BillingAmount
            };
        }

        public ProjectMetric ToEntity(CreateProjectMetricDto dto)
        {
            if (dto == null) return null;

            var entity = new ProjectMetric
            {
                ProjectId = dto.ProjectId,
                MetricType = dto.MetricType,
                MetricName = dto.MetricName,
                Value = dto.Value,
                Unit = dto.Unit,
                MeasuredAt = dto.MeasuredAt ?? DateTime.UtcNow,
                Period = dto.Period,
                Metadata = dto.Metadata,
                IsBillable = dto.IsBillable,
                BillingRate = dto.BillingRate
            };

            // Automatický výpočet fakturační částky
            if (entity.IsBillable && entity.BillingRate.HasValue)
            {
                entity.BillingAmount = entity.Value * entity.BillingRate.Value;
            }

            return entity;
        }
    }
}