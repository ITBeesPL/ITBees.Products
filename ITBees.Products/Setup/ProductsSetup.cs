using ITBees.Products.Services.Deliveries;
using ITBees.Products.Services.Labels;
using ITBees.Products.Services.Producer;
using ITBees.Products.Services.Product;
using ITBees.Products.Services.Stock;
using ITBees.Products.Services.Warehouses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITBees.Products.Setup;

public class ProductsSetup
{
    /// <summary>
    /// Registers the services behind every controller of this package: producers, products,
    /// warehouses, deliveries, serialized products on stock and warehouse labels. The entities
    /// are mapped separately - see <see cref="DbModelBuilder.Register"/>.
    /// </summary>
    /// <param name="settings">
    /// Pass <see cref="ProductsSettings.DeviceWarehouseUrl"/> to make the label QR codes open
    /// the device details page; without it they carry the bare device guid.
    /// </param>
    public void Register(IServiceCollection services, ProductsSettings? settings = null)
    {
        services.AddSingleton(settings ?? new ProductsSettings());

        // Hosts that registered the two original services themselves keep their registrations.
        services.TryAddTransient<IProducerService, ProducerService>();
        services.TryAddTransient<IProductService, ProductService>();

        services.AddTransient<IWarehouseService, WarehouseService>();
        services.AddTransient<IProductDeliveryService, ProductDeliveryService>();
        services.AddTransient<ISerializedProductService, SerializedProductService>();
        services.AddTransient<IStockLabelService, StockLabelService>();
        services.AddSingleton<IStockLabelPdfGenerator, StockLabelPdfGenerator>();
    }
}
