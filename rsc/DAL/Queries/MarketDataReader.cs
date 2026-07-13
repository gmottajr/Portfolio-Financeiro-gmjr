using Application.Contracts;
using DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace DAL.Queries;

/// <summary>Reads the reference market snapshot loaded from the seed file.</summary>
public sealed class MarketDataReader(PortfolioDbContext context) : IMarketDataReader
{
    public async Task<decimal?> GetSelicRateAsync(CancellationToken ct = default)
    {
        var rate = await context.MarketDataSnapshots
            .AsNoTracking()
            .OrderBy(snapshot => snapshot.Id)
            .Select(snapshot => (decimal?)snapshot.SelicRate)
            .FirstOrDefaultAsync(ct);

        if (rate is null)
        {
            return null;
        }

        // The supplied seed expresses Selic as a fraction (0.12); analytics
        // responses use percentage points, just like portfolio returns.
        return rate is >= 0m and <= 1m ? rate * 100m : rate;
    }
}
