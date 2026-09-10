using ITBees.Interfaces.Repository;
using ITBees.Products.Controllers.Models;
using ITBees.RestfulApiControllers.Exceptions;
using ITBees.RestfulApiControllers.Models;
using ITBees.UserManager.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ITBees.Products.Services.Producer;

public class ProducerService : IProducerService
{
    private readonly IAspCurrentUserService _aspCurrentUserService;
    private readonly IWriteOnlyRepository<Entities.Producer> _producerWoRepo;
    private readonly IReadOnlyRepository<Entities.Producer> _producerRoRepo;

    public ProducerService(
        IAspCurrentUserService aspCurrentUserService,
        IWriteOnlyRepository<Entities.Producer> producerWoRepo,
        IReadOnlyRepository<Entities.Producer> producerRoRepo)
    {
        _aspCurrentUserService = aspCurrentUserService;
        _producerWoRepo = producerWoRepo;
        _producerRoRepo = producerRoRepo;
    }
    
    
    public ProducerVm Get(int productId)
    {
        if (!_aspCurrentUserService.CurrentUserIsPlatformOperator())
        {
            var message = "You don't have enought rights to get this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        
        var producer = _producerRoRepo.GetData(x => x.Id == productId).FirstOrDefault();
        
        if (producer == null)
        {
            throw new FasApiErrorException("Producer not found", 404);
        }
        
        return new ProducerVm(producer); 
    }

    public ProducerVm Create(ProducerIm producerIm)
    {
        if (!_aspCurrentUserService.CurrentUserIsPlatformOperator())
        {
            var message = "You don't have enought rights to edit this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        
        var newProducer = _producerWoRepo.InsertData(new Entities.Producer()
        {
            Name = producerIm.Name,
            IsActive = producerIm.IsActive,
            Url = producerIm.Url
        });
        
        return new ProducerVm(newProducer);
    }

    public ProducerVm Update(ProducerUm producerUm)
    {
        var cu = _aspCurrentUserService.GetCurrentSessionUser();
        if (!cu.IsAuthorized)
        {
            var message = "You don't have enought rights to edit this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        
        var producer = _producerRoRepo.GetData(x => x.Id == producerUm.Id).FirstOrDefault();
        if (producer == null)
        {
            throw new FasApiErrorException("Producer not found", 404);
        }
        
        var updatedProducer = _producerWoRepo.UpdateData(x => x.Id ==producerUm.Id , x =>
        {
            x.IsActive = producerUm.IsActive;
            x.Name = producerUm.Name;
            x.Url = producerUm.Url;
            
        }).FirstOrDefault();
        
        return new ProducerVm(updatedProducer);
    }

    public List<ProducerVm> GetAll()
    {
        if (!_aspCurrentUserService.CurrentUserIsPlatformOperator())
        {
            var message = "You don't have enought rights to get this data.";
            throw new FasApiErrorException(new FasApiErrorVm(message, StatusCodes.Status403Forbidden));  
        }
        var producers = _producerRoRepo.GetData(x => x.IsActive).ToList();
        return new List<ProducerVm>(producers.Select(x => new ProducerVm(x)));
    }
}