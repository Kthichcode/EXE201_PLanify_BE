using Planify.Application.DTOs.Common;
using Planify.Application.DTOs.Notifications;
using Planify.Application.Interfaces;
using Planify.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Planify.Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepo;

    public NotificationService(INotificationRepository notificationRepo)
    {
        _notificationRepo = notificationRepo;
    }

    public async Task<ResponseDto<IEnumerable<NotificationDto>>> GetUserNotificationsAsync(Guid userId, int limit = 50)
    {
        var notifications = await _notificationRepo.GetByUserIdAsync(userId, limit);
        
        var dtos = notifications.Select(n => new NotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type,
            IsRead = n.IsRead,
            ReferenceId = n.ReferenceId,
            CreatedAt = n.CreatedAt
        });

        return ResponseDto<IEnumerable<NotificationDto>>.Success(dtos, "Lấy thông báo thành công.");
    }

    public async Task<ResponseDto<int>> GetUnreadCountAsync(Guid userId)
    {
        var count = await _notificationRepo.GetUnreadCountAsync(userId);
        return ResponseDto<int>.Success(count, "Lấy số lượng chưa đọc thành công.");
    }

    public async Task<ResponseDto<bool>> MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        
        if (notification == null || notification.UserId != userId)
        {
            return ResponseDto<bool>.Fail("Không tìm thấy thông báo.", 404);
        }

        notification.IsRead = true;
        await _notificationRepo.UpdateAsync(notification);
        await _notificationRepo.SaveChangesAsync();

        return ResponseDto<bool>.Success(true, "Đã đánh dấu đã đọc.");
    }

    public async Task<ResponseDto<bool>> MarkAllAsReadAsync(Guid userId)
    {
        await _notificationRepo.MarkAllAsReadAsync(userId);
        await _notificationRepo.SaveChangesAsync();
        
        return ResponseDto<bool>.Success(true, "Đã đánh dấu tất cả là đã đọc.");
    }
}
