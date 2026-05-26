using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Subscriptions;

namespace Planify.Application.Interfaces;

public interface ISubscriptionService
{
    // Customer APIs
    Task<ResponseDto<IEnumerable<SubscriptionPlanDto>>> GetActivePlansAsync();
    Task<ResponseDto<UserSubscriptionDto>> GetUserSubscriptionAsync(Guid userId);
    Task<ResponseDto<UserSubscriptionDto>> UpgradeSubscriptionAsync(Guid userId, UpgradeSubscriptionRequestDto dto);

    // Admin APIs
    Task<ResponseDto<IEnumerable<SubscriptionPlanDto>>> GetAllPlansAsync();
    Task<ResponseDto<SubscriptionPlanDto>> CreatePlanAsync(CreateSubscriptionPlanDto dto);
    Task<ResponseDto<SubscriptionPlanDto>> UpdatePlanAsync(Guid id, UpdateSubscriptionPlanDto dto);
    Task<ResponseDto<bool>> DeactivatePlanAsync(Guid id);
}
