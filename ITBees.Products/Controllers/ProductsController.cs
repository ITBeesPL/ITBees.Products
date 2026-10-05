using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.Product;
using ITBees.Products.Entities;
using ITBees.Products.Services.Product;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

[Authorize(Roles = Role.PlatformOperator)]
public class ProductsController : RestfulControllerBase<ProductsController>
{
    private readonly IProductService _productService;

    public ProductsController(ILogger<ProductsController> logger, IProductService productService) : base(logger)
    {
        _productService = productService;
    }

    /// <summary>
    /// Active catalogue entries; <paramref name="includeInactive"/> adds the archived ones,
    /// <paramref name="productGroupId"/> / <paramref name="kind"/> narrow the list down.
    /// </summary>
    [HttpGet]
    [Produces(typeof(List<ProductVm>))]
    public IActionResult Get(int? productGroupId = null, ProductKind? kind = null, bool includeInactive = false)
    {
        return ReturnOkResult(() => _productService.GetAll(productGroupId, kind, includeInactive));
    }
}