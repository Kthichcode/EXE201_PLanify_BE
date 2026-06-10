using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.PlanTemplates;
using Planify.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

[ApiController]
[Route("api/admin/plan-templates")]
[Authorize(Roles = "Admin")]
public class AdminPlanTemplatesController : ControllerBase
{
    private readonly IPlanTemplateService _service;

    public AdminPlanTemplatesController(IPlanTemplateService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ResponseDto<IEnumerable<PlanTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _service.GetAllTemplatesAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ResponseDto<PlanTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _service.GetTemplateByIdAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("framework/{frameworkId}")]
    [ProducesResponseType(typeof(ResponseDto<IEnumerable<PlanTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByFrameworkId(Guid frameworkId)
    {
        var response = await _service.GetTemplatesByFrameworkIdAsync(frameworkId);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Tạo template bằng JSON (templateContent là mảng các dòng).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ResponseDto<PlanTemplateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreatePlanTemplateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var adminId = GetAdminId();
        if (adminId is null)
            return Unauthorized(ResponseDto<PlanTemplateDto>.Fail("Không xác định được admin.", 401));

        var response = await _service.CreateTemplateAsync(dto, adminId.Value);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Tạo template bằng plain text (copy thẳng từ Word/internet, không cần format JSON).
    /// Dùng Content-Type: multipart/form-data — điền vào các field bên dưới.
    /// </summary>
    [HttpPost("from-text")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ResponseDto<PlanTemplateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateFromPlainText(
        [FromForm] Guid? frameworkId,
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] string templateContent,
        [FromForm] bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { error = "Tiêu đề template không được để trống." });
        if (string.IsNullOrWhiteSpace(templateContent))
            return BadRequest(new { error = "Nội dung template không được để trống." });

        var adminId = GetAdminId();
        if (adminId is null)
            return Unauthorized(ResponseDto<PlanTemplateDto>.Fail("Không xác định được admin.", 401));

        // Chuyển plain text thành DTO — mỗi dòng là 1 phần tử
        var dto = new CreatePlanTemplateDto
        {
            FrameworkId     = frameworkId,
            Title           = title,
            Description     = description,
            TemplateContent = new List<string>(
                templateContent.Split('\n', StringSplitOptions.None)),
            IsActive = isActive
        };

        var response = await _service.CreateTemplateAsync(dto, adminId.Value);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ResponseDto<PlanTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlanTemplateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _service.UpdateTemplateAsync(id, dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id}/deactivate")]
    [ProducesResponseType(typeof(ResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var response = await _service.DeactivateTemplateAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await _service.DeleteTemplateAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    private Guid? GetAdminId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
