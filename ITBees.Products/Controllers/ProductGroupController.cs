using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.ProductGroups;
using ITBees.Products.Services.ProductGroups;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

[Authorize(Roles = Role.PlatformOperator)]
public class ProductGroupController : RestfulControllerBase<ProductGroupController>
{
    private readonly IProductGroupService _productGroupService;

    public ProductGroupController(ILogger<ProductGroupController> logger, IProductGroupService productGroupService)
        : base(logger)
    {
        _productGroupService = productGroupService;
    }

    [HttpGet]
    [Produces(typeof(ProductGroupVm))]
    public IActionResult Get(int productGroupId)
    {
        return ReturnOkResult(() => _productGroupService.Get(productGroupId));
    }

    [HttpPost]
    [Produces(typeof(ProductGroupVm))]
    public IActionResult Post([FromBody] ProductGroupIm productGroupIm)
    {
        return ReturnOkResult(() => _productGroupService.Create(productGroupIm));
    }

    [HttpPut]
    [Produces(typeof(ProductGroupVm))]
    public IActionResult Put([FromBody] ProductGroupUm productGroupUm)
    {
        return ReturnOkResult(() => _productGroupService.Update(productGroupUm));
    }

    /// <summary>Removes the group and its memberships; the products stay in the catalogue.</summary>
    [HttpDelete]
    public IActionResult Delete(int productGroupId)
    {
        return ReturnOkResult(() => _productGroupService.Delete(productGroupId));
    }
}
