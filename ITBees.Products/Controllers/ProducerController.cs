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
public class ProducerController : RestfulControllerBase<ProducerController>
{
    private readonly IProducerService _producerService;
    private readonly IProductService _productService;

    public ProducerController(ILogger<ProducerController> logger, IProducerService producerService) : base(logger)
    {
        _producerService = producerService;
    }

    [HttpGet]
    [Produces(typeof(ProducerVm))]
    public IActionResult Get(int producerId)
    {
        return ReturnOkResult(() => _producerService.Get(producerId));
    }

    [HttpPost]
    [Produces(typeof(ProducerVm))]
    public IActionResult Post([FromBody] ProducerIm producerIm)
    {
        return ReturnOkResult(() => _producerService.Create(producerIm));
    }

    [HttpPut]
    [Produces(typeof(ProducerVm))]
    public IActionResult Put([FromBody] ProducerUm producerUm)
    {
        return ReturnOkResult(() => _producerService.Update(producerUm));
    }
}