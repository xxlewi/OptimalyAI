using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.Core.Mapping;
using OAI.ServiceLayer.Mapping;

namespace OAI.ServiceLayer.Mapping.AI;

public interface IAiModelMapper : IMapper<AiModel, AiModelDto>
{
    CreateAiModelDto ToCreateDto(AiModel entity);
    AiModel ToEntity(CreateAiModelDto dto);
    UpdateAiModelDto ToUpdateDto(AiModel entity);
    AiModel UpdateEntity(AiModel entity, UpdateAiModelDto dto);
}

public class AiModelMapper : BaseMapper<AiModel, AiModelDto>, IAiModelMapper
{
    private readonly IAiServerMapper _aiServerMapper;

    public AiModelMapper(IAiServerMapper aiServerMapper)
    {
        _aiServerMapper = aiServerMapper;
    }

    public override AiModelDto ToDto(AiModel entity)
    {
        if (entity == null) return null!;

        return new AiModelDto
        {
            Id = entity.Id,
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            FilePath = entity.FilePath,
            SizeBytes = entity.SizeBytes,
            Tag = entity.Tag,
            Family = entity.Family,
            ParameterSize = entity.ParameterSize,
            QuantizationLevel = entity.QuantizationLevel,
            LastUsedAt = entity.LastUsedAt,
            IsAvailable = entity.IsAvailable,
            IsDefault = entity.IsDefault,
            AiServerId = entity.AiServerId,
            Metadata = entity.Metadata,
            AiServer = entity.AiServer != null ? _aiServerMapper.ToDto(entity.AiServer) : null,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public override AiModel ToEntity(AiModelDto dto)
    {
        if (dto == null) return null!;

        return new AiModel
        {
            Id = dto.Id,
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            FilePath = dto.FilePath,
            SizeBytes = dto.SizeBytes,
            Tag = dto.Tag,
            Family = dto.Family,
            ParameterSize = dto.ParameterSize,
            QuantizationLevel = dto.QuantizationLevel,
            LastUsedAt = dto.LastUsedAt,
            IsAvailable = dto.IsAvailable,
            IsDefault = dto.IsDefault,
            AiServerId = dto.AiServerId,
            Metadata = dto.Metadata,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }

    public CreateAiModelDto ToCreateDto(AiModel entity)
    {
        if (entity == null) return null!;

        return new CreateAiModelDto
        {
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            FilePath = entity.FilePath,
            SizeBytes = entity.SizeBytes,
            Tag = entity.Tag,
            Family = entity.Family,
            ParameterSize = entity.ParameterSize,
            QuantizationLevel = entity.QuantizationLevel,
            IsAvailable = entity.IsAvailable,
            IsDefault = entity.IsDefault,
            AiServerId = entity.AiServerId,
            Metadata = entity.Metadata
        };
    }

    public AiModel ToEntity(CreateAiModelDto dto)
    {
        if (dto == null) return null!;

        return new AiModel
        {
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            FilePath = dto.FilePath,
            SizeBytes = dto.SizeBytes,
            Tag = dto.Tag,
            Family = dto.Family,
            ParameterSize = dto.ParameterSize,
            QuantizationLevel = dto.QuantizationLevel,
            IsAvailable = dto.IsAvailable,
            IsDefault = dto.IsDefault,
            AiServerId = dto.AiServerId,
            Metadata = dto.Metadata
        };
    }

    public UpdateAiModelDto ToUpdateDto(AiModel entity)
    {
        if (entity == null) return null!;

        return new UpdateAiModelDto
        {
            Id = entity.Id,
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            FilePath = entity.FilePath,
            SizeBytes = entity.SizeBytes,
            Tag = entity.Tag,
            Family = entity.Family,
            ParameterSize = entity.ParameterSize,
            QuantizationLevel = entity.QuantizationLevel,
            LastUsedAt = entity.LastUsedAt,
            IsAvailable = entity.IsAvailable,
            IsDefault = entity.IsDefault,
            AiServerId = entity.AiServerId,
            Metadata = entity.Metadata
        };
    }

    public AiModel UpdateEntity(AiModel entity, UpdateAiModelDto dto)
    {
        if (entity == null || dto == null) return entity!;

        entity.Name = dto.Name;
        entity.DisplayName = dto.DisplayName;
        entity.FilePath = dto.FilePath;
        entity.SizeBytes = dto.SizeBytes;
        entity.Tag = dto.Tag;
        entity.Family = dto.Family;
        entity.ParameterSize = dto.ParameterSize;
        entity.QuantizationLevel = dto.QuantizationLevel;
        entity.LastUsedAt = dto.LastUsedAt;
        entity.IsAvailable = dto.IsAvailable;
        entity.IsDefault = dto.IsDefault;
        entity.AiServerId = dto.AiServerId;
        entity.Metadata = dto.Metadata;

        return entity;
    }
}