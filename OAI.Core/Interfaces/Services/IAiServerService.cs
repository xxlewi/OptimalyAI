using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OAI.Core.DTOs;

namespace OAI.Core.Interfaces.Services
{
    /// <summary>
    /// Service for managing AI servers
    /// </summary>
    public interface IAiServerService
    {
        /// <summary>
        /// Get AI server by ID
        /// </summary>
        Task<AiServerDto> GetByIdAsync(Guid id);

        /// <summary>
        /// Get all AI servers
        /// </summary>
        Task<IEnumerable<AiServerDto>> GetAllAsync();

        /// <summary>
        /// Get active AI servers
        /// </summary>
        Task<IEnumerable<AiServerDto>> GetActiveServersAsync();

        /// <summary>
        /// Create new AI server
        /// </summary>
        Task<AiServerDto> CreateAsync(CreateAiServerDto dto);

        /// <summary>
        /// Update AI server
        /// </summary>
        Task<AiServerDto> UpdateAsync(Guid id, UpdateAiServerDto dto);

        /// <summary>
        /// Delete AI server
        /// </summary>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Test AI server connection
        /// </summary>
        Task<bool> TestConnectionAsync(Guid id);
    }

}