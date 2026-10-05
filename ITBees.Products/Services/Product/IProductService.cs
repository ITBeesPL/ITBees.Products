using ITBees.Products.Controllers.Models;
using ITBees.Products.Controllers.Models.Product;
using ITBees.Products.Entities;

namespace ITBees.Products.Services.Product;

public interface IProductService
{
    ProductVm Get(int productId);
    ProductVm Create(ProductIm productIm);
    ProductVm Update(ProductUm productUm);

    /// <summary>
    /// Catalogue entries - by default only the active ones; optionally only those of one group and/or
    /// of one kind (goods or services).
    /// </summary>
    List<ProductVm> GetAll(int? productGroupId = null, ProductKind? kind = null, bool includeInactive = false);
}