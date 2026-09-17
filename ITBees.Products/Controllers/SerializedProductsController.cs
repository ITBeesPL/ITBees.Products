using ITBees.Interfaces.Repository;
using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.Stock;
using ITBees.Products.Services.Stock;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

[Authorize(Roles = Role.PlatformOperator)]
public class SerializedProductsController : RestfulControllerBase<SerializedProductsController>
{
    private readonly ISerializedProductService _serializedProductService;

    public SerializedProductsController(ILogger<SerializedProductsController> logger,
        ISerializedProductService serializedProductService) : base(logger)
    {
        _serializedProductService = serializedProductService;
    }

    [HttpGet]
    [Produces(typeof(PaginatedResult<SerializedProductVm>))]
    public IActionResult Get(string? search, [FromQuery] Guid[]? warehouseGuids, [FromQuery] int[]? productIds,
        Guid? productDeliveryGuid, bool? deliveredToEndCustomer, int? page, int? pageSize, string? sortColumn,
        SortOrder? sortOrder)
    {
        return ReturnOkResult(() => _serializedProductService.GetPaginated(search, warehouseGuids, productIds,
            productDeliveryGuid, deliveredToEndCustomer, page, pageSize, sortColumn, sortOrder));
    }
}
