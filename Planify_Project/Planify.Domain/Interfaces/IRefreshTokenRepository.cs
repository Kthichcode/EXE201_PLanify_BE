using Planify.Domain.Entities;

namespace Planify.Domain.Interfaces;

/// <summary>
/// Repository interface cho RefreshToken entity.
/// Implementation: Infrastructure/Repositories/RefreshTokenRepository.cs
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByTokenAsync(string token);
    Task AddAsync(RefreshToken refreshToken);
    Task SaveChangesAsync();
}
