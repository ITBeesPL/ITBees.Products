using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models;
using ITBees.Products.Services.Producer;
using ITBees.Products.Services.Product;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

[Authorize(Roles = Role.PlatformOperator)]
public class ProducersController : RestfulControllerBase<ProducersController>
{
    private readonly IProducerService _producerService;
    private readonly IProductService _productService;

    public ProducersController(ILogger<ProducersController> logger, IProducerService producerService) : base(logger)
    {
        _producerService = producerService;
    }

    [HttpGet]
    [Produces(typeof(List<ProducerVm>))]
    public IActionResult Get()
    {
        return ReturnOkResult(() => _producerService.GetAll());
    }
}