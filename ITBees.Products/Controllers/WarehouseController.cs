using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.Warehouses;
using ITBees.Products.Services.Warehouses;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

[Authorize(Roles = Role.PlatformOperator)]
public class WarehouseController : RestfulControllerBase<WarehouseController>
{
    private readonly IWarehouseService _warehouseService;

    public WarehouseController(ILogger<WarehouseController> logger, IWarehouseService warehouseService) : base(logger)
    {
        _warehouseService = warehouseService;
    }

    [HttpGet]
    [Produces(typeof(WarehouseVm))]
    public IActionResult Get(Guid guid)
    {
        return ReturnOkResult(() => _warehouseService.Get(guid));
    }

    [HttpPost]
    [Produces(typeof(WarehouseVm))]
    public IActionResult Post([FromBody] WarehouseIm warehouseIm)
    {
        return ReturnOkResult(() => _warehouseService.Create(warehouseIm));
    }

    [HttpPut]
    [Produces(typeof(WarehouseVm))]
    public IActionResult Put([FromBody] WarehouseUm warehouseUm)
    {
        return ReturnOkResult(() => _warehouseService.Update(warehouseUm));
    }

    [HttpDelete]
    public IActionResult Delete(Guid guid)
    {
        return ReturnOkResult(() => _warehouseService.Delete(guid));
    }
}
