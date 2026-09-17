using ITBees.Interfaces.Repository;
using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.Deliveries;
using ITBees.Products.Services.Deliveries;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

[Authorize(Roles = Role.PlatformOperator)]
public class ProductDeliveriesController : RestfulControllerBase<ProductDeliveriesController>
{
    private readonly IProductDeliveryService _productDeliveryService;

    public ProductDeliveriesController(ILogger<ProductDeliveriesController> logger,
        IProductDeliveryService productDeliveryService) : base(logger)
    {
        _productDeliveryService = productDeliveryService;
    }

    [HttpGet]
    [Produces(typeof(PaginatedResult<ProductDeliveryVm>))]
    public IActionResult Get(string? search, [FromQuery] Guid[]? warehouseGuids, int? page, int? pageSize,
        string? sortColumn, SortOrder? sortOrder)
    {
        return ReturnOkResult(() =>
            _productDeliveryService.GetPaginated(search, warehouseGuids, page, pageSize, sortColumn, sortOrder));
    }
}
