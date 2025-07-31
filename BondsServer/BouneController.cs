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

                List<BondWithStatistics> filteredBonds = new(allBondsData.Length);
                for (int i = 0; i < allBondsData.Length; i++)
                {
                    BondWithStatistics bond = allBondsData.Span[i].Item2;

                    if (!string.IsNullOrEmpty(search))
                    {
                        if (!bond.Bond.id.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
                    }

                    if (minPrice.HasValue && bond.Bond.price < minPrice.Value) continue;
                    if (maxPrice.HasValue && bond.Bond.price > maxPrice.Value) continue;
                    if (minYield.HasValue && bond.Yield < (float)minYield.Value) continue;
                    if (maxYield.HasValue && bond.Yield > (float)maxYield.Value) continue;

                    filteredBonds.Add(bond);
                }

                var sortedBonds = filteredBonds.AsQueryable();

                // Apply sorting
                if (!string.IsNullOrEmpty(sortBy))
                {
                    switch (sortBy.ToLower())
                    {
                        case "id":
                            sortedBonds = sortDir == "desc" 
                                ? sortedBonds.OrderByDescending(b => b.Bond.id)
                                : sortedBonds.OrderBy(b => b.Bond.id);
                            break;
                        case "price":
                            sortedBonds = sortDir == "desc"
                                ? sortedBonds.OrderByDescending(b => b.Bond.price)
                                : sortedBonds.OrderBy(b => b.Bond.price);
                            break;
                        case "yield":
                            sortedBonds = sortDir == "desc"
                                ? sortedBonds.OrderByDescending(b => b.Yield)
                                : sortedBonds.OrderBy(b => b.Yield);
                            break;
                        case "coupon":
                            sortedBonds = sortDir == "desc"
                                ? sortedBonds.OrderByDescending(b => b.Bond.coupon)
                                : sortedBonds.OrderBy(b => b.Bond.coupon);
                            break;
                        default:
                            sortedBonds = sortedBonds.OrderBy(b => b.Bond.id);
                            break;
                    }
                }

                var totalCount = sortedBonds.Count();

                // Apply pagination
                var pagedBonds = sortedBonds
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
                    allBonds.Add(allBondsData.Span[i].Item2);
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