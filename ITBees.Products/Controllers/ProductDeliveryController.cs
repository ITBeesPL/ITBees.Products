using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.Deliveries;
using ITBees.Products.Services.Deliveries;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

/// <summary>
/// A purchase batch received into a warehouse: the purchase data (date, invoice, seller,
/// warranty) plus the scanned serialized products.
/// </summary>
[Authorize(Roles = Role.PlatformOperator)]
public class ProductDeliveryController : RestfulControllerBase<ProductDeliveryController>
{
    private readonly IProductDeliveryService _productDeliveryService;

    public ProductDeliveryController(ILogger<ProductDeliveryController> logger,
        IProductDeliveryService productDeliveryService) : base(logger)
    {
        _productDeliveryService = productDeliveryService;
    }

    [HttpGet]
    [Produces(typeof(ProductDeliveryVm))]
    public IActionResult Get(Guid guid)
    {
        return ReturnOkResult(() => _productDeliveryService.Get(guid));
    }

    [HttpPost]
    [Produces(typeof(ProductDeliveryVm))]
    public IActionResult Post([FromBody] ProductDeliveryIm productDeliveryIm)
    {
        return ReturnOkResult(() => _productDeliveryService.Create(productDeliveryIm));
    }

    [HttpPut]
    [Produces(typeof(ProductDeliveryVm))]
    public IActionResult Put([FromBody] ProductDeliveryUm productDeliveryUm)
    {
        return ReturnOkResult(() => _productDeliveryService.Update(productDeliveryUm));
    }

    [HttpDelete]
    public IActionResult Delete(Guid guid)
    {
        return ReturnOkResult(() => _productDeliveryService.Delete(guid));
    }
}
