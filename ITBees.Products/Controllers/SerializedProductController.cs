using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.Stock;
using ITBees.Products.Services.Stock;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

/// <summary>
/// A single serialized product on stock, addressed by our internal serial number (guid) -
/// this is what the QR code on a warehouse label leads to.
/// </summary>
[Authorize(Roles = Role.PlatformOperator)]
public class SerializedProductController : RestfulControllerBase<SerializedProductController>
{
    private readonly ISerializedProductService _serializedProductService;

    public SerializedProductController(ILogger<SerializedProductController> logger,
        ISerializedProductService serializedProductService) : base(logger)
    {
        _serializedProductService = serializedProductService;
    }

    [HttpGet]
    [Produces(typeof(SerializedProductVm))]
    public IActionResult Get(Guid guid)
    {
        return ReturnOkResult(() => _serializedProductService.Get(guid));
    }

    [HttpPut]
    [Produces(typeof(SerializedProductVm))]
    public IActionResult Put([FromBody] SerializedProductUm serializedProductUm)
    {
        return ReturnOkResult(() => _serializedProductService.Update(serializedProductUm));
    }
}
