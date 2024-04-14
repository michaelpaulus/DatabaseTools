using Cornerstone.Entities;

namespace Cornerstone.Repository;

public interface IRepository<Entity> : IDisposable where Entity : class, IEntity<Entity>
{
    Task<Entity?> GetSingleAsync(IQuerySpec<Entity> spec);
    Task<IEnumerable<Entity>> GetListAsync(IQuerySpec<Entity> spec);
    Task<int> GetCountAsync(IQuerySpec<Entity> spec);

    Task AddAsync(Entity entity);
    Task AddRangeAsync(IEnumerable<Entity> entities);
    Task UpdateAsync(Entity entity);
    Task UpdateRangeAsync(IEnumerable<Entity> entities);
    Task DeleteAsync(Entity entity);
    Task DeleteRangeAsync(IEnumerable<Entity> entities);
}
