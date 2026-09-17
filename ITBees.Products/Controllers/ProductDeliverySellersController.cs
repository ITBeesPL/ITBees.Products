using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.Deliveries;
using ITBees.Products.Services.Deliveries;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

[Authorize(Roles = Role.PlatformOperator)]
public class ProductDeliverySellersController : RestfulControllerBase<ProductDeliverySellersController>
{
    private readonly IProductDeliveryService _productDeliveryService;

    public ProductDeliverySellersController(ILogger<ProductDeliverySellersController> logger,
        IProductDeliveryService productDeliveryService) : base(logger)
    {
        _productDeliveryService = productDeliveryService;
    }

    /// <param name="search">Part of the seller name or tax id; omit to get the most recently used sellers.</param>
    /// <param name="limit">Maximum number of suggestions (20 when omitted).</param>
    [HttpGet]
    [Produces(typeof(List<ProductDeliverySellerVm>))]
    public IActionResult Get(string? search, int? limit)
    {
        return ReturnOkResult(() => _productDeliveryService.GetSellers(search, limit));
    }
}
