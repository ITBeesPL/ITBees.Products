using ITBees.Models.Roles;
using ITBees.Products.Services.Labels;
using ITBees.RestfulApiControllers;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.RestfulApiControllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ITBees.Products.Controllers;

/// <summary>
/// Warehouse labels as a PDF: one 50 x 30 mm page per item. Pass <c>productDeliveryGuid</c> for
/// all labels of a delivery (in scanning order) or <c>guid</c> for the label of a single item.
/// </summary>
[Authorize(Roles = Role.PlatformOperator)]
public class StockLabelsController : RestfulControllerBase<StockLabelsController>
{
    private readonly IStockLabelService _stockLabelService;
    private readonly ILogger<StockLabelsController> _logger;

    public StockLabelsController(ILogger<StockLabelsController> logger, IStockLabelService stockLabelService) :
        base(logger)
    {
        _logger = logger;
        _stockLabelService = stockLabelService;
    }

    [HttpGet]
    public IActionResult Get(Guid? productDeliveryGuid, Guid? guid)
    {
        try
        {
            if (productDeliveryGuid == null && guid == null)
            {
                throw new FasApiErrorException("Wskaż dostawę (productDeliveryGuid) albo urządzenie (guid).", 400);
            }

            var label = productDeliveryGuid.HasValue
                ? _stockLabelService.GetForDelivery(productDeliveryGuid.Value)
                : _stockLabelService.GetForItem(guid!.Value);

            return File(label.Content, "application/pdf", label.FileName);
        }
        catch (FasApiErrorException e)
        {
            // A file result cannot go through ReturnOkResult, so its error mapping is repeated here.
            return StatusCode(e.FasApiErrorVm.StatusCode, e.FasApiErrorVm);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Generating warehouse labels failed: {Message}", e.Message);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new FasApiErrorVm("Nie udało się wygenerować etykiet.", StatusCodes.Status500InternalServerError));
        }
    }
}
