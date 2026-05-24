using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MeetingSim.Tests.Integration;

public sealed class MeetingSimAppFactory : WebApplicationFactory<Program>
{
    public string ArchiveRoot { get; } = Path.Combine(Path.GetTempPath(), $"meetingsim-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(ArchiveRoot);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "test-key-not-used",
                ["SessionArchive:Root"] = ArchiveRoot,
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(ArchiveRoot))
        {
            try
            {
                Directory.Delete(ArchiveRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
