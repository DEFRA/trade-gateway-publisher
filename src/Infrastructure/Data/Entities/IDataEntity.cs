namespace Infrastructure.Data.Entities;

public interface IDataEntity
{
    string Id { get; set; }

    DateTime Created { get; set; }

    DateTime Updated { get; set; }
}
