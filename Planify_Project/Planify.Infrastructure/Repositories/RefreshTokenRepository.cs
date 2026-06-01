using Microsoft.EntityFrameworkCore;
using Planify.Domain.Entities;
using Planify.Domain.Interfaces;
using Planify.Infrastructure.Data;

namespace Planify.Infrastructure.Repositories;

/// <summary>
/// Implements IRefreshTokenRepository bằng ApplicationDbContext (EF Core).
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context;

    public RefreshTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> FindByTokenAsync(string token)
        => await _context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token);

    public async Task AddAsync(RefreshToken refreshToken)
        => await _context.RefreshTokens.AddAsync(refreshToken);

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
