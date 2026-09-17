namespace ITBees.Products.Services.Labels;

public interface IStockLabelPdfGenerator
{
    /// <summary>
    /// Renders one label per page, in the given order. Every page has exactly the size of the
    /// physical label, so the document prints 1:1 on a label printer without any margins.
    /// </summary>
    byte[] Generate(IReadOnlyCollection<StockLabelData> labels);
}
