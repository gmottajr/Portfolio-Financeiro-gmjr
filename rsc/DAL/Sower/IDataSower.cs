namespace DAL.Sower;

/// <summary>
/// Loads the initial persistent data set into the configured data store.
/// </summary>
public interface IDataSower
{
    /// <summary>
    /// Loads the default seed file from the application output directory.
    /// </summary>
    Task SowAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads seed data from the supplied JSON file.
    /// </summary>
    Task SowAsync(string seedFilePath, CancellationToken ct = default);
}
