using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BondsServer
{
    [ApiController]
    [Route("api/[controller]")]
    public class BondsController : ControllerBase
    {
        private readonly CacheUpdateWorker _cacheWorker;

        public BondsController(CacheUpdateWorker cacheWorker)
        {
            _cacheWorker = cacheWorker;
        }

        [HttpGet]
        public async Task<ActionResult<BondPageResponse>> GetBonds(
            [FromQuery] int page = 1,
            [FromQuery] int size = 100,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortDir = "asc",
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] decimal? minYield = null,
            [FromQuery] decimal? maxYield = null)
        {
            try
            {
                // Get all bonds from cache
                var allBondsData = _cacheWorker.GetLatestStatuses();
                var allBonds = new List<BondWithStatistics>();

                // Parse JSON data
                for (int i = 0; i < allBondsData.Length; i++)
                {
                    try
                    {
                        var bond = JsonSerializer.Deserialize<BondWithStatistics>(allBondsData.Span[i]);
                        if (bond != null)
                            allBonds.Add(bond);
                    }
                    catch
                    {
                        // Skip invalid entries
                    }
                }

                // Apply filters
                var filteredBonds = allBonds.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    filteredBonds = filteredBonds.Where(b => 
                        b.Bond.id.Contains(search, StringComparison.OrdinalIgnoreCase));
                }

                if (minPrice.HasValue)
                    filteredBonds = filteredBonds.Where(b => b.Bond.price >= minPrice.Value);

                if (maxPrice.HasValue)
                    filteredBonds = filteredBonds.Where(b => b.Bond.price <= maxPrice.Value);

                if (minYield.HasValue)
                    filteredBonds = filteredBonds.Where(b => b.Yield >= (float)minYield.Value);

                if (maxYield.HasValue)
                    filteredBonds = filteredBonds.Where(b => b.Yield <= (float)maxYield.Value);

                // Apply sorting
                if (!string.IsNullOrEmpty(sortBy))
                {
                    switch (sortBy.ToLower())
                    {
                        case "id":
                            filteredBonds = sortDir == "desc" 
                                ? filteredBonds.OrderByDescending(b => b.Bond.id)
                                : filteredBonds.OrderBy(b => b.Bond.id);
                            break;
                        case "price":
                            filteredBonds = sortDir == "desc"
                                ? filteredBonds.OrderByDescending(b => b.Bond.price)
                                : filteredBonds.OrderBy(b => b.Bond.price);
                            break;
                        case "yield":
                            filteredBonds = sortDir == "desc"
                                ? filteredBonds.OrderByDescending(b => b.Yield)
                                : filteredBonds.OrderBy(b => b.Yield);
                            break;
                        case "coupon":
                            filteredBonds = sortDir == "desc"
                                ? filteredBonds.OrderByDescending(b => b.Bond.coupon)
                                : filteredBonds.OrderBy(b => b.Bond.coupon);
                            break;
                        default:
                            filteredBonds = filteredBonds.OrderBy(b => b.Bond.id);
                            break;
                    }
                }

                var totalCount = filteredBonds.Count();

                // Apply pagination
                var pagedBonds = filteredBonds
                    .Skip((page - 1) * size)
                    .Take(size)
                    .ToList();

                var response = new BondPageResponse
                {
                    Data = pagedBonds,
                    Page = page,
                    Size = size,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / size),
                    HasNext = page * size < totalCount,
                    HasPrevious = page > 1
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("summary")]
        public ActionResult<BondSummary> GetSummary()
        {
            try
            {
                var allBondsData = _cacheWorker.GetLatestStatuses();
                var allBonds = new List<BondWithStatistics>();

                for (int i = 0; i < allBondsData.Length; i++)
                {
                    try
                    {
                        var bond = JsonSerializer.Deserialize<BondWithStatistics>(allBondsData.Span[i]);
                        if (bond != null)
                            allBonds.Add(bond);
                    }
                    catch { }
                }

                if (!allBonds.Any())
                {
                    return Ok(new BondSummary());
                }

                var summary = new BondSummary
                {
                    TotalBonds = allBonds.Count,
                    AveragePrice = (decimal)allBonds.Average(b => b.Bond.price),
                    AverageYield = allBonds.Average(b => b.Yield),
                    MinPrice = allBonds.Min(b => b.Bond.price),
                    MaxPrice = allBonds.Max(b => b.Bond.price),
                    MinYield = allBonds.Min(b => b.Yield),
                    MaxYield = allBonds.Max(b => b.Yield)
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class BondPageResponse
    {
        public List<BondWithStatistics> Data { get; set; } = new();
        public int Page { get; set; }
        public int Size { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasNext { get; set; }
        public bool HasPrevious { get; set; }
    }

    public class BondSummary
    {
        public int TotalBonds { get; set; }
        public decimal AveragePrice { get; set; }
        public float AverageYield { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public float MinYield { get; set; }
        public float MaxYield { get; set; }
    }
}