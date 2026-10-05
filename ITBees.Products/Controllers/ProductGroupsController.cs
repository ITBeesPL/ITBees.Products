using ITBees.Models.Roles;
using ITBees.Products.Controllers.Models.ProductGroups;
using ITBees.Products.Services.ProductGroups;
using ITBees.RestfulApiControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

[Authorize(Roles = Role.PlatformOperator)]
public class ProductGroupsController : RestfulControllerBase<ProductGroupsController>
{
    private readonly IProductGroupService _productGroupService;

    public ProductGroupsController(ILogger<ProductGroupsController> logger, IProductGroupService productGroupService)
        : base(logger)
    {
        _productGroupService = productGroupService;
    }

    [HttpGet]
    [Produces(typeof(List<ProductGroupVm>))]
    public IActionResult Get()
    {
        return ReturnOkResult(() => _productGroupService.GetAll());
    }
}
