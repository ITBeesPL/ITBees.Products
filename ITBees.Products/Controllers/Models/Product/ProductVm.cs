namespace ITBees.Products.Controllers.Models.Product;

public class ProductVm
{
    public ProductVm(){}

    public ProductVm(DbModels.Product x)
    {
        Id = x.Id;
        ProducerId = x.ProducerId;
        Producer = x.Producer == null ? null : new ProducerVm(x.Producer);
        ProductImages = x.ProductImages == null ? null : x.ProductImages.Select(y => new ProductImageVm(y)).ToList();
        ThumbnailUrl = x.ThumbnailUrl;
        ShortDescription = x.ShortDescription;
        LongDescription = x.LongDescription;
        Created = x.Created;
        IsActive = x.IsActive;
        NetPriceSell = x.NetPriceSell;
        VatPercentageSell = x.VatPercentageSell;
        NetPriceBuy = x.NetPriceBuy;
        VatPercentageBuy = x.VatPercentageBuy;
        Ean = x.Ean;
        AddedByGuid = x.AddedByGuid;

    }
    
    public int Id { get; set; }
    public ProducerVm Producer { get; set; }
    public int ProducerId { get; set; }
    public List<ProductImageVm> ProductImages { get; set; }
    public string ThumbnailUrl { get; set; }
    public string ShortDescription { get; set; }
    public string LongDescription { get; set; }
    public DateTime Created { get; set; }
    public bool IsActive { get; set; }
    public decimal NetPriceSell { get; set; }
    public int VatPercentageSell { get; set; }
    public decimal NetPriceBuy { get; set; }
    public int VatPercentageBuy { get; set; }
    public string Ean { get; set; }
    public Guid AddedByGuid { get; set; }
}