using ITBees.Products.Controllers.Models;
using ITBees.Products.Controllers.Models.Product;

namespace ITBees.Products.Services.Product;

public interface IProductService
{
    ProductVm Get(int productId);
    ProductVm Create(ProductIm productIm);
    ProductVm Update(ProductUm productUm);
    List<ProductVm> GetAll();
}