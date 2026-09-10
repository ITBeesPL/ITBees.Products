using ITBees.Products.Entities;

namespace ITBees.Products.Controllers.Models;

public class ProductImageVm
{
    public ProductImageVm() { }
    public ProductImageVm(ProductImage x)
    {
        Id = x.Id;
        ProductId = x.ProductId;
        ImageUrl = x.ImageUrl;
    }
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ImageUrl { get; set; }
    
    public static List<ProductImageVm> GetSampleProductImages()
    {
        return new List<ProductImageVm>()
        {
            new ProductImageVm()
            {
                Id = 1,
                ImageUrl = "https://teltonika-gps.com/cdn/extras/22516/fmc003-side-840xAuto.webp"
            },
            new ProductImageVm()
            {
                Id = 2,
                ImageUrl =
                    "https://teltonika-gps.com/cdn/extras/11053/effortless-plug-and-play-installation-1920x1280-840xAuto.webp"
            }
        };
    }
}