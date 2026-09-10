using ITBees.Products.Entities;

namespace ITBees.Products.Controllers.Models;

public class ProducerVm
{
    public ProducerVm()
    {
    }
    public ProducerVm(Producer x)
    {
        Id = x.Id;
        Name = x.Name;
        IsActive = x.IsActive;
        Url = x.Url;
    }
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public string Url { get; set; }

    public static ProducerVm GetSampleProducer()
    {
        return new ProducerVm()
        {
            Id = 1,
            Name = "Teltonika",
            Url = "https://www.teltonika.com",
            IsActive = true,
        };
    }

    public static ProducerVm GetSampleProducer2()
    {
        return new ProducerVm()
        {
            Id = 2,
            Name = "China",
            Url = "https://www.china.com",
            IsActive = true,
        };
    }

    public static ProducerVm GetSampleProducer3()
    {
        return new ProducerVm()
        {
            Id = 3,
            Name = "Garmin",
            Url = "https://www.garmin.com",
            IsActive = true,
        };
    }
}