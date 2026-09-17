namespace ITBees.Products.Services.Labels;

public interface IStockLabelService
{
    /// <summary>Labels of every item of a delivery - one page per item, in scanning order.</summary>
    StockLabelFile GetForDelivery(Guid productDeliveryGuid);

    /// <summary>A single label, e.g. to replace a damaged one.</summary>
    StockLabelFile GetForItem(Guid guid);
}

public class StockLabelFile
{
    public StockLabelFile(byte[] content, string fileName)
    {
        Content = content;
        FileName = fileName;
    }

    /// <summary>PDF document.</summary>
    public byte[] Content { get; }
    public string FileName { get; }
}
