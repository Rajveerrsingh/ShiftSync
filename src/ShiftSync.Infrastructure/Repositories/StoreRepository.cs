using Microsoft.EntityFrameworkCore;
using ShiftSync.Application.Abstractions;
using ShiftSync.Domain.Entities;
using ShiftSync.Infrastructure.Persistence;

namespace ShiftSync.Infrastructure.Repositories;

public class StoreRepository : IStoreRepository
{
    private readonly AppDbContext _db;

    public StoreRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Store?> GetWithHoursAsync(int storeId, CancellationToken ct)
        => _db.Stores.AsNoTracking().Include(store => store.Hours).FirstOrDefaultAsync(store => store.Id == storeId, ct);
}
