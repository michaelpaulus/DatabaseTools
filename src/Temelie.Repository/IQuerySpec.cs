using Temelie.Entities;
namespace Temelie.Repository;
public interface IQuerySpec<Entity> where Entity : class, IEntity<Entity>
{
    IQueryable<Entity> Apply(IQueryable<Entity> query);
}
