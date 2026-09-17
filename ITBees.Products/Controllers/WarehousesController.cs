using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.Warehouses;
using ITBees.Products.Services.Warehouses;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

[Authorize(Roles = Role.PlatformOperator)]
public class WarehousesController : RestfulControllerBase<WarehousesController>
{
    private readonly IWarehouseService _warehouseService;

    public WarehousesController(ILogger<WarehousesController> logger, IWarehouseService warehouseService) : base(logger)
    {
        _warehouseService = warehouseService;
    }

    /// <param name="isActive">Omit to get every warehouse, including the deactivated ones.</param>
    [HttpGet]
    [Produces(typeof(List<WarehouseVm>))]
    public IActionResult Get(bool? isActive)
    {
        return ReturnOkResult(() => _warehouseService.GetAll(isActive));
    }
}
