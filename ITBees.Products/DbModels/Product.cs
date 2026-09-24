using ITBees.Models.Users;
using ITBees.Products.Entities;

namespace ITBees.Products.DbModels;

public class Product
{
    public int Id { get; set; }
    public Producer Producer { get; set; }
    public int ProducerId { get; set; }
    public List<ProductImage> ProductImages { get; set; }
    public string ThumbnailUrl { get; set; }
    public string ShortDescription { get; set; }
    public string LongDescription { get; set; }
    public DateTime Created { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Net sale price of one piece ("cena netto sprzedaży").</summary>
    public decimal NetPriceSell { get; set; }

    /// <summary>VAT rate of the sale in percent, e.g. 23 ("stawka VAT").</summary>
    public int VatPercentageSell { get; set; }

    /// <summary>
    /// Gross sale price of one piece ("cena brutto") - what a consumer pays and what a public shop
    /// shows. Kept together with <see cref="NetPriceSell"/> and <see cref="VatPercentageSell"/>: it
    /// may differ from the net price with VAT by at most one grosz (a "nice" price such as 1999.00),
    /// see <see cref="ProductPrices"/>. Zero in rows stored before the column existed - read it
    /// through <see cref="ProductPrices.EffectiveGross"/>.
    /// </summary>
    public decimal GrossPriceSell { get; set; }

    /// <summary>
    /// "Publicznie dostępny" - the host may show the product to customers and let them order it
    /// (e.g. a public shop page). False by default, so products of the back office stay hidden.
    /// </summary>
    public bool IsPubliclyAvailable { get; set; }

    /// <summary>
    /// "Termin realizacji zamówienia" - working days needed to ship an order when the product is
    /// not on stock. Null when no lead time is set.
    /// </summary>
    public int? OrderFulfillmentDays { get; set; }

    public decimal NetPriceBuy { get; set; }
    public int VatPercentageBuy { get; set; }
    public string Ean { get; set; }

    /// <summary>
    /// Kept on stock as a quantity per warehouse instead of one row per serial number - e.g.
    /// cables, paper rolls or spare parts. Such pieces are counted by scanning the <see cref="Ean"/>
    /// code on them and their stock is a sum of <see cref="Entities.ProductStockMovement"/> rows.
    /// False (the default) keeps the product serialized, as every product was before this flag.
    /// </summary>
    public bool WithoutSerialNumbers { get; set; }

    public UserAccount AddedBy { get; set; }
    public Guid AddedByGuid { get; set; }
}