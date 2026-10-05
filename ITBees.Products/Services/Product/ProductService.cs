using System.Globalization;
using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models.Product;
using ITBees.Products.Entities;
using ITBees.Products.Services.Stock;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.RestfulApiControllers.Models;
using ITBees.UserManager.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ITBees.Products.Services.Product;

public class ProductService : IProductService
{
    private const int MaxEanLength = 50;
    private const int MaxOrderFulfillmentDays = 365;

    private readonly IAspCurrentUserService _aspCurrentUserService;
    private readonly IWriteOnlyRepository<DbModels.Product> _productWoRepo;
    private readonly IReadOnlyRepository<DbModels.Product> _productRoRepo;
    private readonly IReadOnlyRepository<SerializedProductOnStock> _stockRoRepo;
    private readonly IReadOnlyRepository<ProductStockMovement> _movementRoRepo;
    private readonly IReadOnlyRepository<Entities.Producer> _producerRoRepo;
    private readonly IReadOnlyRepository<ProductGroup> _groupRoRepo;

    public ProductService(
        IAspCurrentUserService aspCurrentUserService,
        IWriteOnlyRepository<DbModels.Product> productWoRepo,
        IReadOnlyRepository<DbModels.Product> productRoRepo,
        IReadOnlyRepository<SerializedProductOnStock> stockRoRepo,
        IReadOnlyRepository<ProductStockMovement> movementRoRepo,
        IReadOnlyRepository<Entities.Producer> producerRoRepo,
        IReadOnlyRepository<ProductGroup> groupRoRepo)
    {
        _aspCurrentUserService = aspCurrentUserService;
        _productWoRepo = productWoRepo;
        _productRoRepo = productRoRepo;
        _stockRoRepo = stockRoRepo;
        _movementRoRepo = movementRoRepo;
        _producerRoRepo = producerRoRepo;
        _groupRoRepo = groupRoRepo;
    }

    public ProductVm Get(int productId)
    {
        if (!_aspCurrentUserService.CurrentUserIsPlatformOperator())
        {
            var message = "You don't have enought rights to get this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        
        var product = _productRoRepo.GetData(x => x.Id == productId, x => x.ProductGroups).FirstOrDefault();

        if (product == null)
        {
            throw new FasApiErrorException("Product not found", 404);
        }

        return ToVm(product, GroupsById(new[] { product }));
    }
    

    public ProductVm Create(ProductIm productIm)
    {
        var cu = _aspCurrentUserService.GetCurrentSessionUser();
        if (!_aspCurrentUserService.CurrentUserIsPlatformOperator())
        {
            var message = "You don't have enought rights to add this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        
        var kind = ValidateKind(productIm.Kind);
        var producerId = ValidateProducer(productIm.ProducerId, kind);
        var ean = NormalizeEan(productIm.Ean);
        ThrowIfEanTaken(ean, null);
        var grossPrice = ValidatePrices(productIm.NetPriceSell, productIm.VatPercentageSell, productIm.GrossPriceSell);
        var fulfillmentDays = ValidateFulfillmentDays(productIm.OrderFulfillmentDays);
        var (validityPeriod, validityUnit) = ValidateValidity(kind, productIm.ValidityPeriod, productIm.ValidityUnit);
        var groups = ValidateGroups(productIm.ProductGroupIds);

        // Thumbnail, descriptions and EAN are optional in the request, but their columns are
        // not nullable - store an empty text instead of failing on a missing value.
        var newProduct = _productWoRepo.InsertData(new DbModels.Product()
        {
            ProducerId = producerId,
            Kind = kind,
            ValidityPeriod = validityPeriod,
            ValidityUnit = validityUnit,
            ThumbnailUrl = productIm.Thumbnail?.ImageUrl ?? string.Empty,
            ShortDescription = productIm.ShortDescription ?? string.Empty,
            LongDescription = productIm.LongDescription ?? string.Empty,
            Created = DateTime.UtcNow,
            IsActive = true,
            NetPriceSell = productIm.NetPriceSell,
            VatPercentageSell = productIm.VatPercentageSell,
            GrossPriceSell = grossPrice,
            IsPubliclyAvailable = productIm.IsPubliclyAvailable,
            OrderFulfillmentDays = fulfillmentDays,
            NetPriceBuy = productIm.NetPriceBuy,
            VatPercentageBuy = productIm.VatPercentageBuy,
            Ean = ean,
            // A service is never on stock, so it is not counted in pieces either.
            WithoutSerialNumbers = kind == ProductKind.Goods && productIm.WithoutSerialNumbers,
            AddedByGuid = cu.CurrentUserGuid.Value,
            ProductImages = productIm.ProductImages?.Select(pi => new ProductImage()
            {
                ImageUrl = pi.ImageUrl,
            }).ToList() ?? new List<ProductImage>(),
            ProductGroups = groups.Select(g => new ProductInGroup { ProductGroupId = g.Id }).ToList()
        });

        return ToVm(newProduct, groups.ToDictionary(x => x.Id));
    }
    
    public ProductVm Update(ProductUm productUm)
    {
       
        if (!_aspCurrentUserService.CurrentUserIsPlatformOperator())
        {
            var message = "You don't have enought rights to add this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        
        var product = _productRoRepo.GetData(x => x.Id == productUm.ProductId, x => x.Producer).FirstOrDefault();
        if (product == null)
        {
            throw new FasApiErrorException("Product not found", 404);
        }

        var kind = productUm.Kind == null ? product.Kind : ValidateKind(productUm.Kind.Value);
        if (kind == ProductKind.Service && product.Kind != ProductKind.Service)
        {
            ThrowIfEverOnStock(product);
        }

        var producerId = ValidateProducer(productUm.ProducerId, kind);

        // Only a changed code is checked - entries stored before the rule keep working.
        var ean = NormalizeEan(productUm.Ean);
        if (!string.Equals(ean, product.Ean, StringComparison.Ordinal))
        {
            ThrowIfEanTaken(ean, product.Id);
        }

        var grossPrice = ValidatePrices(productUm.NetPriceSell, productUm.VatPercentageSell, productUm.GrossPriceSell);
        // Null = "leave as it is" for clients that do not know the fields; 0 clears the lead time.
        var fulfillmentDays = productUm.OrderFulfillmentDays == null
            ? product.OrderFulfillmentDays
            : ValidateFulfillmentDays(productUm.OrderFulfillmentDays);
        var isPubliclyAvailable = productUm.IsPubliclyAvailable ?? product.IsPubliclyAvailable;

        // A service is never on stock, so it is not counted in pieces either.
        var withoutSerialNumbers = kind == ProductKind.Goods &&
                                   (productUm.WithoutSerialNumbers ?? product.WithoutSerialNumbers);
        if (kind == ProductKind.Goods && withoutSerialNumbers != product.WithoutSerialNumbers)
        {
            ThrowIfStockKeptTheOtherWay(product, withoutSerialNumbers);
        }

        // Null = "leave as it is"; goods have no validity, so a service turned into goods loses it.
        var (validityPeriod, validityUnit) = productUm.ValidityPeriod == null
            ? kind == ProductKind.Service ? (product.ValidityPeriod, product.ValidityUnit) : (null, null)
            : ValidateValidity(kind, productUm.ValidityPeriod, productUm.ValidityUnit ?? product.ValidityUnit);
        var groups = productUm.ProductGroupIds == null ? null : ValidateGroups(productUm.ProductGroupIds);
        var isActive = productUm.IsActive ?? product.IsActive;

        // The images are loaded together with the product: replacing them below needs the
        // collection to exist (it is null otherwise) and the old rows to be tracked for removal.
        var updatedProduct = _productWoRepo.UpdateData(x => x.Id == productUm.ProductId, x =>
        {
            x.ProducerId = producerId;
            x.Kind = kind;
            x.ValidityPeriod = validityPeriod;
            x.ValidityUnit = validityUnit;
            x.IsActive = isActive;
            // A missing thumbnail means "leave it as it is", not "clear it".
            if (productUm.Thumbnail != null)
            {
                x.ThumbnailUrl = productUm.Thumbnail.ImageUrl ?? string.Empty;
            }

            x.ShortDescription = productUm.ShortDescription ?? string.Empty;
            x.LongDescription = productUm.LongDescription ?? string.Empty;
            x.NetPriceSell = productUm.NetPriceSell;
            x.VatPercentageSell = productUm.VatPercentageSell;
            x.GrossPriceSell = grossPrice;
            x.IsPubliclyAvailable = isPubliclyAvailable;
            x.OrderFulfillmentDays = fulfillmentDays;
            x.NetPriceBuy = productUm.NetPriceBuy;
            x.VatPercentageBuy = productUm.VatPercentageBuy;
            x.Ean = ean;
            x.WithoutSerialNumbers = withoutSerialNumbers;
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

            if (groups != null)
            {
                // Only the difference is written: a membership removed and added back under the
                // same key in one save would collide in the change tracker.
                x.ProductGroups ??= new List<ProductInGroup>();
                var wanted = groups.Select(g => g.Id).ToHashSet();
                x.ProductGroups.RemoveAll(m => !wanted.Contains(m.ProductGroupId));
                foreach (var groupId in wanted.Where(id => x.ProductGroups.All(m => m.ProductGroupId != id)))
                {
                    x.ProductGroups.Add(new ProductInGroup { ProductGroupId = groupId });
                }
            }
        }, x => x.ProductImages, x => x.ProductGroups).First();

        return ToVm(updatedProduct, GroupsById(new[] { updatedProduct }));
    }

    public List<ProductVm> GetAll(int? productGroupId = null, ProductKind? kind = null, bool includeInactive = false)
    {
        var cu = _aspCurrentUserService.GetCurrentSessionUser();
        if (!cu.IsAuthorized)
        {
            var message = "You don't have enought rights to get this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));
        }

        var products = _productRoRepo.GetData(x =>
                    (includeInactive || x.IsActive) &&
                    (kind == null || x.Kind == kind) &&
                    (productGroupId == null || x.ProductGroups!.Any(m => m.ProductGroupId == productGroupId)),
                x => x.Producer, x => x.ProductImages, x => x.ProductGroups)
            .ToList();

        var groups = GroupsById(products);
        return products.Select(x => ToVm(x, groups)).ToList();
    }

    private static ProductVm ToVm(DbModels.Product product, IReadOnlyDictionary<int, ProductGroup> groups)
    {
        var own = (product.ProductGroups ?? new List<ProductInGroup>())
            .Select(m => groups.GetValueOrDefault(m.ProductGroupId))
            .OfType<ProductGroup>();
        return new ProductVm(product, own);
    }

    /// <summary>The groups the given products belong to - the membership rows do not carry their names.</summary>
    private Dictionary<int, ProductGroup> GroupsById(IEnumerable<DbModels.Product> products)
    {
        var ids = products
            .SelectMany(x => x.ProductGroups ?? new List<ProductInGroup>())
            .Select(x => x.ProductGroupId)
            .Distinct()
            .ToList();
        return ids.Count == 0
            ? new Dictionary<int, ProductGroup>()
            : _groupRoRepo.GetData(x => ids.Contains(x.Id)).ToDictionary(x => x.Id);
    }

    private static ProductKind ValidateKind(ProductKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new FasApiErrorException("Wybierz rodzaj: towar albo usługa.", 400);
        }

        return kind;
    }

    /// <summary>
    /// The producer to store: required (and existing) for goods, optional for a service - null when
    /// none was chosen (null or 0).
    /// </summary>
    private int? ValidateProducer(int? producerId, ProductKind kind)
    {
        if (producerId is null or 0)
        {
            if (kind == ProductKind.Goods)
            {
                throw new FasApiErrorException("Wybierz producenta towaru.", 400);
            }

            return null;
        }

        if (!_producerRoRepo.HasData(x => x.Id == producerId))
        {
            throw new FasApiErrorException("Nie znaleziono wybranego producenta.", 400);
        }

        return producerId;
    }

    /// <summary>
    /// The validity to store: none when the period is missing or 0; otherwise only a service may
    /// have one, with a unit and within the <see cref="ProductValidity"/> limits.
    /// </summary>
    private static (int? Period, ProductValidityUnit? Unit) ValidateValidity(ProductKind kind, int? period,
        ProductValidityUnit? unit)
    {
        if (period is null or 0)
        {
            return (null, null);
        }

        if (kind != ProductKind.Service)
        {
            throw new FasApiErrorException("Okres ważności można ustawić tylko usłudze (abonamentowi).", 400);
        }

        if (unit == null || !Enum.IsDefined(unit.Value))
        {
            throw new FasApiErrorException("Wybierz jednostkę okresu ważności: dni, miesiące albo lata.", 400);
        }

        var max = ProductValidity.MaxPeriod(unit.Value);
        if (period < 0 || period > max)
        {
            throw new FasApiErrorException(
                $"Okres ważności może wynosić od 1 do {ProductValidity.Describe(max, unit.Value)} (0 - bez końca).",
                400);
        }

        return (period, unit);
    }

    /// <summary>The existing groups with the given ids (each once) - or 400 naming the missing ones.</summary>
    private List<ProductGroup> ValidateGroups(List<int>? productGroupIds)
    {
        var ids = (productGroupIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new List<ProductGroup>();
        }

        var groups = _groupRoRepo.GetData(x => ids.Contains(x.Id)).ToList();
        var missing = ids.Where(id => groups.All(g => g.Id != id)).ToList();
        if (missing.Count > 0)
        {
            throw new FasApiErrorException(
                $"Nie znaleziono grupy produktów o identyfikatorze: {string.Join(", ", missing)}.", 400);
        }

        return groups;
    }

    /// <summary>A service is never on stock - goods that were ever received cannot become one.</summary>
    private void ThrowIfEverOnStock(DbModels.Product product)
    {
        if (_stockRoRepo.HasData(x => x.ProductId == product.Id) ||
            _movementRoRepo.HasData(x => x.ProductId == product.Id))
        {
            throw new FasApiErrorException(
                $"„{StockBalances.ProductName(product)}” był już przyjmowany do magazynu - nie może stać się " +
                "usługą. Dodaj do katalogu osobną pozycję.", 400);
        }
    }

    /// <summary>
    /// Checks the sale price and returns the gross price to store: the given one when it agrees
    /// with the net price and the VAT rate to within a grosz, the net price with VAT when none was
    /// given (older clients) - so a shop never shows a gross price the invoice cannot reproduce.
    /// </summary>
    private static decimal ValidatePrices(decimal netPrice, int vatPercentage, decimal? grossPrice)
    {
        if (netPrice < 0)
        {
            throw new FasApiErrorException("Cena netto sprzedaży nie może być ujemna.", 400);
        }

        if (vatPercentage < 0 || vatPercentage > ProductPrices.MaxVatPercentage)
        {
            throw new FasApiErrorException(
                $"Stawka VAT musi mieć od 0 do {ProductPrices.MaxVatPercentage}%.", 400);
        }

        var expected = ProductPrices.GrossFromNet(netPrice, vatPercentage);
        if (grossPrice == null)
        {
            return expected;
        }

        if (grossPrice < 0)
        {
            throw new FasApiErrorException("Cena brutto nie może być ujemna.", 400);
        }

        var gross = ProductPrices.Round(grossPrice.Value);
        if (!ProductPrices.GrossMatchesNet(gross, netPrice, vatPercentage))
        {
            throw new FasApiErrorException(
                $"Cena brutto {Amount(gross)} nie zgadza się z ceną netto {Amount(netPrice)} i stawką VAT " +
                $"{vatPercentage}% - wychodzi z nich {Amount(expected)} brutto.", 400);
        }

        return gross;
    }

    /// <summary>Lead time to store: null for none (missing or 0), otherwise 1 to 365 working days.</summary>
    private static int? ValidateFulfillmentDays(int? days)
    {
        if (days == null || days == 0)
        {
            return null;
        }

        if (days < 0 || days > MaxOrderFulfillmentDays)
        {
            throw new FasApiErrorException(
                $"Termin realizacji zamówienia podaj w dniach roboczych: od 1 do {MaxOrderFulfillmentDays} " +
                "(0 - bez terminu).", 400);
        }

        return days;
    }

    /// <summary>"1625,20" - hosts may run with invariant globalization, so no culture is looked up.</summary>
    private static string Amount(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>
    /// The code as a scanner reads it: without whitespace, an empty text when there is none
    /// (the column is not nullable).
    /// </summary>
    private static string NormalizeEan(string? ean)
    {
        var code = string.Concat((ean ?? string.Empty).Where(c => !char.IsWhiteSpace(c)));
        if (code.Length > MaxEanLength)
        {
            throw new FasApiErrorException($"Kod EAN może mieć najwyżej {MaxEanLength} znaków.", 400);
        }

        return code;
    }

    /// <summary>A scanned EAN has to point at exactly one catalogue entry - that is how pieces get counted.</summary>
    private void ThrowIfEanTaken(string ean, int? ownProductId)
    {
        if (ean.Length == 0)
        {
            return;
        }

        var other = _productRoRepo.GetData(x => x.Ean == ean && x.Id != ownProductId, x => x.Producer)
            .FirstOrDefault();
        if (other != null)
        {
            throw new FasApiErrorException(
                $"Kod EAN {ean} ma już pozycja katalogu „{StockBalances.ProductName(other)}”.", 400);
        }
    }

    /// <summary>
    /// A product is kept either item by item (serial numbers) or as a quantity - never both. Once
    /// stock was received one way, switching would leave that stock unreadable.
    /// </summary>
    private void ThrowIfStockKeptTheOtherWay(DbModels.Product product, bool withoutSerialNumbers)
    {
        var name = StockBalances.ProductName(product);
        if (withoutSerialNumbers && _stockRoRepo.HasData(x => x.ProductId == product.Id))
        {
            throw new FasApiErrorException(
                $"„{name}” ma już w magazynie sztuki z numerami seryjnymi - nie może stać się produktem bez " +
                "numerów seryjnych. Dodaj do katalogu osobną pozycję.", 400);
        }

        if (!withoutSerialNumbers && _movementRoRepo.HasData(x => x.ProductId == product.Id))
        {
            throw new FasApiErrorException(
                $"„{name}” był już przyjmowany ilościowo (bez numerów seryjnych) - nie może teraz wymagać " +
                "numerów seryjnych. Dodaj do katalogu osobną pozycję.", 400);
        }
    }
}