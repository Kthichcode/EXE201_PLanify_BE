using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.PlanFrameworks;
using Planify.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/admin/plan-frameworks")]
[Authorize(Roles = "Admin")]
public class AdminPlanFrameworksController : ControllerBase
{
    private readonly IPlanFrameworkService _service;

    public AdminPlanFrameworksController(IPlanFrameworkService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ResponseDto<IEnumerable<PlanFrameworkDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _service.GetAllFrameworksAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ResponseDto<PlanFrameworkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _service.GetFrameworkByIdAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ResponseDto<PlanFrameworkDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreatePlanFrameworkDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var adminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(adminIdString) || !Guid.TryParse(adminIdString, out var adminId))
        {
            return Unauthorized(ResponseDto<PlanFrameworkDto>.Fail("Không xác định được admin.", 401));
        }

        var response = await _service.CreateFrameworkAsync(dto, adminId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ResponseDto<PlanFrameworkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlanFrameworkDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _service.UpdateFrameworkAsync(id, dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id}/deactivate")]
    [ProducesResponseType(typeof(ResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var response = await _service.DeactivateFrameworkAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await _service.DeleteFrameworkAsync(id);
        return StatusCode(response.StatusCode, response);
    }
}
