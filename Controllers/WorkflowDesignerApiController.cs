using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;
using OAI.Core.Interfaces;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// API controller pro workflow designer - Demo version
    /// </summary>
    [Route("api/workflow")]
    [ApiController]
    public class WorkflowDesignerApiController : BaseApiController
    {
        private readonly IProjectService _projectService;

        public WorkflowDesignerApiController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        /// <summary>
        /// Get workflow types
        /// </summary>
        [HttpGet("types")]
        public async Task<IActionResult> GetWorkflowTypes()
        {
            var types = await _projectService.GetWorkflowTypesAsync();
            return Ok(types);
        }

        /// <summary>
        /// Create project from workflow
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
        {
            var project = await _projectService.CreateProjectAsync(dto);
            return Ok(project);
        }
    }
}