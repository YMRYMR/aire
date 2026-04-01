namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Opens the database connection, runs schema migrations, and seeds default data.
    /// </summary>
    public interface IDatabaseInitializer
    {
        Task InitializeAsync();
    }
}
