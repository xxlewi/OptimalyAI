// using OAI.Core.Entities;
// using OAI.Core.DTOs;
// using OAI.Core.Mapping;

// namespace OAI.ServiceLayer.Mapping;

// /// <summary>
// /// Příklad mapperu - odkomentujte a upravte podle potřeby
// /// </summary>
// public class ExampleMapper : BaseMapper<ExampleEntity, ExampleDto>, IExampleMapper
// {
//     public override ExampleDto ToDto(ExampleEntity entity)
//     {
//         return new ExampleDto
//         {
//             Id = entity.Id,
//             Name = entity.Name,
//             CreatedAt = entity.CreatedAt,
//             UpdatedAt = entity.UpdatedAt
//         };
//     }

//     public override ExampleEntity ToEntity(ExampleDto dto)
//     {
//         return new ExampleEntity
//         {
//             Id = dto.Id,
//             Name = dto.Name,
//             // CreatedAt a UpdatedAt se nastaví automaticky
//         };
//     }
// }

// public interface IExampleMapper : IMapper<ExampleEntity, ExampleDto>
// {
// }