using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.Stock;
using ITBees.Products.Services.Stock;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

/// <summary>
/// Stock per product and warehouse - products kept without serial numbers (their quantities)
/// and serialized ones (their items on stock, counted).
/// </summary>
[Authorize(Roles = Role.PlatformOperator)]
public class ProductStockLevelsController : RestfulControllerBase<ProductStockLevelsController>
{
    private readonly IProductStockService _productStockService;

    public ProductStockLevelsController(ILogger<ProductStockLevelsController> logger,
        IProductStockService productStockService) : base(logger)
    {
        _productStockService = productStockService;
    }

    /// <param name="search">Part of the product, producer, EAN or warehouse name.</param>
    /// <param name="withoutSerialNumbers">True: only products counted by quantity; false: only serialized ones.</param>
    /// <param name="includeEmpty">Also list the pairs whose pieces have all left (quantity 0).</param>
    [HttpGet]
    [Produces(typeof(List<ProductStockLevelVm>))]
    public IActionResult Get(string? search, [FromQuery] Guid[]? warehouseGuids, [FromQuery] int[]? productIds,
        bool? withoutSerialNumbers, bool? includeEmpty)
    {
        return ReturnOkResult(() =>
            _productStockService.GetLevels(search, warehouseGuids, productIds, withoutSerialNumbers, includeEmpty));
    }
}
