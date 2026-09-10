using ITBees.Products.Controllers.Models;

namespace ITBees.Products.Services.Producer;

public interface IProducerService
{
    ProducerVm Get(int productId);
    ProducerVm Create(ProducerIm producerIm);
    ProducerVm Update(ProducerUm producerUm);
    List<ProducerVm> GetAll();
}