using System.Threading.Tasks;

namespace KTI.PAL.Teams.Core.Domains.Graph
{
    /// <summary>
    /// Interface definition for Graph Domains
    /// </summary>
    /// <typeparam name="T">Preferably a datatype from Microsoft.Graph</typeparam>
    public interface IGraphDomain<T>
    {
        /// <summary>
        /// Create an item.
        /// </summary>
        /// <param name="item">The item to create.</param>
        /// <returns>An awaitable Task containing the ID of the created item.</returns>
        Task<string> Create(T item);
        /// <summary>
        /// Get an item.
        /// </summary>
        /// <param name="id">The ID of the item to get.</param>
        /// <returns>An awaitable Task containing an item whose ID matches the provided ID.</returns>
        Task<T> Get(string id);
        /// <summary>
        /// Update an item.
        /// </summary>
        /// <param name="item">The item with changed values.</param>
        /// <returns>An awaitable Task containing the item with updated values.</returns>
        Task<T> Update(T item);
        /// <summary>
        /// Delete an item.
        /// </summary>
        /// <param name="id">The ID of the item to delete.</param>
        /// <returns>An awaitable Task containing a boolean whether or not the deletion was successful.</returns>
        Task<bool> Delete(string id);
    }
}
