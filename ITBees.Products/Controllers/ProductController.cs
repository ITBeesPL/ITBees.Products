using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.Product;
using ITBees.Products.Services.Product;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

[Authorize(Roles = Role.PlatformOperator)]
public class ProductController : RestfulControllerBase<ProductController>
{
    private readonly IProductService _productService;

    public ProductController(ILogger<ProductController> logger, IProductService productService) : base(logger)
    {
        _productService = productService;
    }

    [HttpGet]
    [Produces(typeof(ProductVm))]
    public IActionResult Get(int productId)
    {
        return ReturnOkResult(() => _productService.Get(productId));
    }

    [HttpPost]
    [Produces(typeof(ProductVm))]
    public IActionResult Post([FromBody] ProductIm productIm)
    {
        return ReturnOkResult(() => _productService.Create(productIm));
    }

    [HttpPut]
    [Produces(typeof(ProductVm))]
    public IActionResult Put([FromBody] ProductUm productUm)
    {
        return ReturnOkResult(() => _productService.Update(productUm));
    }
}