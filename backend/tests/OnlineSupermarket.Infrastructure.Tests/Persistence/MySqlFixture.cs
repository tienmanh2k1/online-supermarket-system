using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OnlineSupermarket.Infrastructure.Persistence;
using Testcontainers.MySql;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Persistence;

public class MySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _mySqlContainer = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("online_supermarket_test")
        .WithUsername("root")
        .WithPassword("password")
        .Build();

    public string ConnectionString => _mySqlContainer.GetConnectionString();

    public string MasterConnectionString =>
        $"{_mySqlContainer.GetConnectionString()};AllowUserVariables=true;";

    public string Host => _mySqlContainer.Hostname;
    public ushort Port => _mySqlContainer.GetMappedPublicPort(3306);
    public string User => "root";
    public string Password => "password";

    public string CreateDatabaseConnectionString(string dbName)
    {
        var builder = new MySqlConnectionStringBuilder(_mySqlContainer.GetConnectionString())
        {
            Database = dbName,
            AllowUserVariables = true
        };
        return builder.ConnectionString;
    }

    public async Task<ExecResult> ExecuteCliAsync(string dbName, string sqlContent)
    {
        var tempFileName = $"/tmp/script_{Guid.NewGuid():N}.sql";
        var bytes = System.Text.Encoding.UTF8.GetBytes(sqlContent);
        await _mySqlContainer.CopyAsync(bytes, tempFileName);

        var result = await _mySqlContainer.ExecAsync(new[]
        {
            "sh",
            "-c",
            $"mysql -u{User} -p{Password} {dbName} < {tempFileName}"
        });

        await _mySqlContainer.ExecAsync(new[] { "rm", "-f", tempFileName });
        return result;
    }

    public async Task<ExecResult> DumpDatabaseAsync(string sourceDb, string dumpFilePath)
    {
        return await _mySqlContainer.ExecAsync(new[]
        {
            "sh",
            "-c",
            $"mysqldump -u{User} -p{Password} --routines --triggers {sourceDb} > {dumpFilePath}"
        });
    }

    public async Task<ExecResult> RestoreDatabaseAsync(string targetDb, string dumpFilePath)
    {
        return await _mySqlContainer.ExecAsync(new[]
        {
            "sh",
            "-c",
            $"mysql -u{User} -p{Password} {targetDb} < {dumpFilePath}"
        });
    }

    public async Task<long> GetFileSizeAsync(string filePath)
    {
        var result = await _mySqlContainer.ExecAsync(new[]
        {
            "sh",
            "-c",
            $"stat -c %s {filePath} 2>/dev/null || wc -c < {filePath}"
        });

        if (result.ExitCode == 0 && long.TryParse(result.Stdout.Trim(), out var size))
        {
            return size;
        }

        return 0;
    }

    public async Task<ExecResult> DeleteFileAsync(string filePath)
    {
        return await _mySqlContainer.ExecAsync(new[] { "rm", "-f", filePath });
    }

    public async Task InitializeAsync()
    {
        await _mySqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(ConnectionString)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _mySqlContainer.DisposeAsync();
    }
}
