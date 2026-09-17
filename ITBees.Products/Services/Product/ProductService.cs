using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models.Product;
using ITBees.Products.Entities;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.RestfulApiControllers.Models;
using ITBees.UserManager.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ITBees.Products.Services.Product;

public class ProductService : IProductService
{
    private readonly IAspCurrentUserService _aspCurrentUserService;
    private readonly IWriteOnlyRepository<DbModels.Product> _productWoRepo;
    private readonly IReadOnlyRepository<DbModels.Product> _productRoRepo;

    public ProductService(
        IAspCurrentUserService aspCurrentUserService,
        IWriteOnlyRepository<DbModels.Product> productWoRepo,
        IReadOnlyRepository<DbModels.Product> productRoRepo)
    {
        _aspCurrentUserService = aspCurrentUserService;
        _productWoRepo = productWoRepo;
        _productRoRepo = productRoRepo;
    }

    public ProductVm Get(int productId)
    {
        if (!_aspCurrentUserService.CurrentUserIsPlatformOperator())
        {
            var message = "You don't have enought rights to get this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        
        var product = _productRoRepo.GetData(x => x.Id == productId).FirstOrDefault();
        
        if (product == null)
        {
            throw new FasApiErrorException("Product not found", 404);
        }

        return new ProductVm(product);
    }
    

    public ProductVm Create(ProductIm productIm)
    {
        var cu = _aspCurrentUserService.GetCurrentSessionUser();
        if (!_aspCurrentUserService.CurrentUserIsPlatformOperator())
        {
            var message = "You don't have enought rights to add this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        
        // Thumbnail, descriptions and EAN are optional in the request, but their columns are
        // not nullable - store an empty text instead of failing on a missing value.
        var newProduct = _productWoRepo.InsertData(new DbModels.Product()
        {
            ProducerId = productIm.ProducerId,
            ThumbnailUrl = productIm.Thumbnail?.ImageUrl ?? string.Empty,
            ShortDescription = productIm.ShortDescription ?? string.Empty,
            LongDescription = productIm.LongDescription ?? string.Empty,
            Created = DateTime.UtcNow,
            IsActive = true,
            NetPriceSell = productIm.NetPriceSell,
            VatPercentageSell = productIm.VatPercentageSell,
            NetPriceBuy = productIm.NetPriceBuy,
            VatPercentageBuy = productIm.VatPercentageBuy,
            Ean = productIm.Ean ?? string.Empty,
            AddedByGuid = cu.CurrentUserGuid.Value,
            ProductImages = productIm.ProductImages?.Select(pi => new ProductImage()
            {
                ImageUrl = pi.ImageUrl,
            }).ToList() ?? new List<ProductImage>()
            
        });

        return new ProductVm(newProduct);
    }
    
    public ProductVm Update(ProductUm productUm)
    {
       
        if (!_aspCurrentUserService.CurrentUserIsPlatformOperator())
        {
            var message = "You don't have enought rights to add this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        
        var producer = _productRoRepo.GetData(x => x.Id == productUm.ProductId).FirstOrDefault();
        if (producer == null)
        {
            throw new FasApiErrorException("Producer not found", 404);
        }
        
        // The images are loaded together with the product: replacing them below needs the
        // collection to exist (it is null otherwise) and the old rows to be tracked for removal.
        var updatedProduct = _productWoRepo.UpdateData(x => x.Id == productUm.ProductId, x =>
        {
            x.ProducerId = productUm.ProducerId;
            // A missing thumbnail means "leave it as it is", not "clear it".
            if (productUm.Thumbnail != null)
            {
                x.ThumbnailUrl = productUm.Thumbnail.ImageUrl ?? string.Empty;
            }

            x.ShortDescription = productUm.ShortDescription ?? string.Empty;
            x.LongDescription = productUm.LongDescription ?? string.Empty;
            x.NetPriceSell = productUm.NetPriceSell;
            x.VatPercentageSell = productUm.VatPercentageSell;
            x.NetPriceBuy = productUm.NetPriceBuy;
            x.VatPercentageBuy = productUm.VatPercentageBuy;
            x.Ean = productUm.Ean ?? string.Empty;
            if (productUm.ProductImages != null)
            {
                x.ProductImages.Clear();
                foreach (var y in productUm.ProductImages)
                {
                    x.ProductImages.Add(new ProductImage()
                    {
                        ImageUrl = y.ImageUrl,
                    });
                }
            }
        }, x => x.ProductImages).FirstOrDefault();
        
        return new ProductVm(updatedProduct);
    }

    public List<ProductVm> GetAll()
    {
        var cu = _aspCurrentUserService.GetCurrentSessionUser();
        if (!cu.IsAuthorized)
        {
            var message = "You don't have enought rights to get this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        
        var products = _productRoRepo.GetData(x => x.IsActive,x => x.Producer, x => x.ProductImages).ToList();
        
        return new List<ProductVm>(products.Select(x => new ProductVm(x)));
    }
}