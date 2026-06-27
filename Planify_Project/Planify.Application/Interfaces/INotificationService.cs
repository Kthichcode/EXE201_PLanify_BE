using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Notifications;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Planify.Application.Interfaces;

public interface INotificationService
{
    Task<ResponseDto<IEnumerable<NotificationDto>>> GetUserNotificationsAsync(Guid userId, int limit = 50);
    Task<ResponseDto<int>> GetUnreadCountAsync(Guid userId);
    Task<ResponseDto<bool>> MarkAsReadAsync(Guid userId, Guid notificationId);
    Task<ResponseDto<bool>> MarkAllAsReadAsync(Guid userId);
}
