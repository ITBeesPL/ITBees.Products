using System.Text;
using ITBees.Interfaces.Repository;
using ITBees.Products.Entities;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.UserManager.Interfaces;

namespace ITBees.Products.Services.Labels;

public class StockLabelService : IStockLabelService
{
    private readonly IAspCurrentUserService _aspCurrentUserService;
    private readonly IReadOnlyRepository<ProductDelivery> _deliveryRoRepo;
    private readonly IReadOnlyRepository<SerializedProductOnStock> _stockRoRepo;
    private readonly IStockLabelPdfGenerator _stockLabelPdfGenerator;
    private readonly ProductsSettings _settings;

    public StockLabelService(
        IAspCurrentUserService aspCurrentUserService,
        IReadOnlyRepository<ProductDelivery> deliveryRoRepo,
        IReadOnlyRepository<SerializedProductOnStock> stockRoRepo,
        IStockLabelPdfGenerator stockLabelPdfGenerator,
        ProductsSettings settings)
    {
        _aspCurrentUserService = aspCurrentUserService;
        _deliveryRoRepo = deliveryRoRepo;
        _stockRoRepo = stockRoRepo;
        _stockLabelPdfGenerator = stockLabelPdfGenerator;
        _settings = settings;
    }

    public StockLabelFile GetForDelivery(Guid productDeliveryGuid)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var delivery = _deliveryRoRepo.GetData(x => x.Guid == productDeliveryGuid).FirstOrDefault();
        if (delivery == null)
        {
            throw new FasApiErrorException("Nie znaleziono dostawy.", 404);
        }

        var labels = _stockRoRepo
            .GetData(x => x.ProductDeliveryGuid == productDeliveryGuid && x.Guid != null)
            .OrderBy(x => x.PositionInDelivery)
            .ThenBy(x => x.Id)
            .Select(x => ToLabel(x, delivery.PurchaseDate))
            .ToList();
        if (labels.Count == 0)
        {
            throw new FasApiErrorException("Ta dostawa nie zawiera żadnych urządzeń do oznaczenia.", 404);
        }

        var name = delivery.InvoiceNumber ?? delivery.PurchaseDate.ToString("yyyy-MM-dd");
        return new StockLabelFile(_stockLabelPdfGenerator.Generate(labels), $"etykiety-{ToFileNamePart(name)}.pdf");
    }

    public StockLabelFile GetForItem(Guid guid)
    {
        _aspCurrentUserService.ThrowIfNotPlatformOperator();

        var item = _stockRoRepo.GetData(x => x.Guid == guid, x => x.ProductDelivery).FirstOrDefault();
        if (item == null)
        {
            throw new FasApiErrorException("Nie znaleziono urządzenia w magazynie.", 404);
        }

        var labels = new[] { ToLabel(item, item.ProductDelivery?.PurchaseDate) };
        return new StockLabelFile(_stockLabelPdfGenerator.Generate(labels),
            $"etykieta-{StockLabelFormat.InternalCode(guid)}.pdf");
    }

    private StockLabelData ToLabel(SerializedProductOnStock item, DateTime? purchaseDate)
    {
        var deviceGuid = item.Guid!.Value;
        return new StockLabelData
        {
            DeviceGuid = deviceGuid,
            SerialNumber = item.SerialNumber,
            PurchaseDate = purchaseDate,
            QrContent = _settings.BuildLabelQrContent(deviceGuid)
        };
    }

    /// <summary>Invoice numbers are full of slashes - keep only what is safe in a file name.</summary>
    private static string ToFileNamePart(string value)
    {
        var safe = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            safe.Append(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '.' ? c : '_');
        }

        return safe.Length == 0 ? "dostawa" : safe.ToString();
    }
}
