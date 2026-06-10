using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.PlanFrameworks;
using Planify.Application.DTOs.PlanTemplates;
using Planify.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Planify.API.Controllers;

/// <summary>
/// Public API cho user xem danh sách Framework và Template để chọn khi tạo kế hoạch AI.
/// </summary>
[ApiController]
[Route("api/plan-frameworks")]
[Authorize]
public class PlanFrameworksController : ControllerBase
{
    private readonly IPlanFrameworkService _frameworkService;
    private readonly IPlanTemplateService _templateService;

    public PlanFrameworksController(
        IPlanFrameworkService frameworkService,
        IPlanTemplateService templateService)
    {
        _frameworkService = frameworkService;
        _templateService  = templateService;
    }

    /// <summary>
    /// Lấy danh sách tất cả frameworks đang active.
    /// FE dùng để hiển thị dropdown cho user chọn loại kế hoạch trước khi generate.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ResponseDto<IEnumerable<PlanFrameworkDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveFrameworks()
    {
        var response = await _frameworkService.GetAllFrameworksAsync();
        // Chỉ trả về frameworks đang active
        if (response.Data != null)
        {
            response = ResponseDto<IEnumerable<PlanFrameworkDto>>.Success(
                System.Linq.Enumerable.Where(response.Data, f => f.IsActive),
                response.Message ?? "Lấy danh sách framework thành công.");
        }
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Lấy danh sách templates đang active của 1 framework cụ thể.
    /// FE dùng để hiển thị danh sách template khi user đã chọn framework.
    /// </summary>
    [HttpGet("{frameworkId}/templates")]
    [ProducesResponseType(typeof(ResponseDto<IEnumerable<PlanTemplateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTemplatesByFramework(Guid frameworkId)
    {
        var response = await _templateService.GetTemplatesByFrameworkIdAsync(frameworkId);
        // Chỉ trả về templates đang active
        if (response.Data != null)
        {
            response = ResponseDto<IEnumerable<PlanTemplateDto>>.Success(
                System.Linq.Enumerable.Where(response.Data, t => t.IsActive),
                response.Message ?? "Lấy danh sách template thành công.");
        }
        return StatusCode(response.StatusCode, response);
    }
}
