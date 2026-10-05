using ITBees.Products.Controllers.Models.ProductGroups;

namespace ITBees.Products.Services.ProductGroups;

/// <summary>Groups of catalogue entries ("Wykłady", "Akcesoria"...) - platform operators only.</summary>
public interface IProductGroupService
{
    ProductGroupVm Get(int productGroupId);

    /// <summary>Every group, by name, with the number of products in it.</summary>
    List<ProductGroupVm> GetAll();

    ProductGroupVm Create(ProductGroupIm productGroupIm);
    ProductGroupVm Update(ProductGroupUm productGroupUm);

    /// <summary>Removes the group and its memberships; the products themselves stay.</summary>
    void Delete(int productGroupId);
}
