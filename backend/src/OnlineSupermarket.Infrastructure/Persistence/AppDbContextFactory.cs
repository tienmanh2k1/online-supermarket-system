using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace OnlineSupermarket.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    internal const string GuardConnectionString =
        "Server=127.0.0.1;Port=1;Database=designtime_guard_only;User=invalid;Password=invalid;";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString(args);
        var isGuard = string.IsNullOrWhiteSpace(connectionString) ||
                      connectionString.Contains("designtime_guard_only");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySQL(isGuard ? GuardConnectionString : connectionString!);

        if (isGuard)
        {
            optionsBuilder.AddInterceptors(new DesignTimeGuardConnectionInterceptor());
            optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator, DesignTimeGuardDatabaseCreator>();
        }

        return new AppDbContext(optionsBuilder.Options);
    }

    internal static string? ResolveConnectionString(string[]? args)
    {
        // 1. Check args (e.g. passed after -- in dotnet ef commands: -- --connection <val>)
        if (args != null && args.Length > 0)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--connection", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return args[i + 1];
                }

                if (args[i].StartsWith("--connection=", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i]["--connection=".Length..];
                }
            }
        }

        // 2. Check direct environment variables
        var envConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(envConnection))
        {
            return envConnection;
        }

        var directEnv = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION");
        if (!string.IsNullOrWhiteSpace(directEnv))
        {
            return directEnv;
        }

        // 3. Check application configuration (appsettings.json / environment-specific)
        var configuration = BuildConfiguration();
        var configConnection = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configConnection))
        {
            return configConnection;
        }

        return null;
    }

    private static IConfiguration BuildConfiguration()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                              ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                              ?? "Production";

        var builder = new ConfigurationBuilder();

        // Allow explicitly bypassing appsettings for testing unconfigured behavior
        if (Environment.GetEnvironmentVariable("IGNORE_APPSETTINGS") != "1")
        {
            var candidates = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
                Path.Combine(Directory.GetCurrentDirectory(), "backend", "src", "OnlineSupermarket.Api"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "OnlineSupermarket.Api")
            };

            foreach (var dir in candidates.Distinct())
            {
                if (Directory.Exists(dir))
                {
                    var mainSettings = Path.Combine(dir, "appsettings.json");
                    if (File.Exists(mainSettings))
                    {
                        builder.AddJsonFile(mainSettings, optional: true);
                        var envSettings = Path.Combine(dir, $"appsettings.{environmentName}.json");
                        if (File.Exists(envSettings))
                        {
                            builder.AddJsonFile(envSettings, optional: true);
                        }
                        break;
                    }
                }
            }
        }

        builder.AddEnvironmentVariables();
        return builder.Build();
    }

    internal sealed class DesignTimeGuardDatabaseCreator : Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator
    {
        private readonly Microsoft.EntityFrameworkCore.Storage.IRelationalConnection _relationalConn;
        private readonly Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator _innerCreator;

        public DesignTimeGuardDatabaseCreator(
            Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreatorDependencies dependencies,
            Microsoft.EntityFrameworkCore.Storage.IRelationalConnection connection,
            Microsoft.EntityFrameworkCore.Storage.IRawSqlCommandBuilder rawSqlCommandBuilder)
            : base(dependencies)
        {
            _relationalConn = connection;

            var mySqlAsm = System.Reflection.Assembly.Load("MySql.EntityFrameworkCore");
            var creatorType = mySqlAsm.GetType("MySql.EntityFrameworkCore.Storage.Internal.MySQLDatabaseCreator", throwOnError: true)!;
            _innerCreator = (Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator)Activator.CreateInstance(
                creatorType,
                dependencies,
                rawSqlCommandBuilder)!;
        }

        public override bool Exists()
        {
            EnsureNotGuard();
            return _innerCreator.Exists();
        }

        public override Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotGuard();
            return _innerCreator.ExistsAsync(cancellationToken);
        }

        public override bool HasTables()
        {
            EnsureNotGuard();
            return _innerCreator.HasTables();
        }

        public override Task<bool> HasTablesAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotGuard();
            return _innerCreator.HasTablesAsync(cancellationToken);
        }

        public override void Create()
        {
            EnsureNotGuard();
            _innerCreator.Create();
        }

        public override Task CreateAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotGuard();
            return _innerCreator.CreateAsync(cancellationToken);
        }

        public override void Delete()
        {
            EnsureNotGuard();
            _innerCreator.Delete();
        }

        public override Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotGuard();
            return _innerCreator.DeleteAsync(cancellationToken);
        }

        private void EnsureNotGuard()
        {
            var connStr = _relationalConn.DbConnection?.ConnectionString ?? _relationalConn.ConnectionString;
            if (!string.IsNullOrEmpty(connStr) && connStr.Contains("designtime_guard_only", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Cannot open database connection: design-time guard configuration is active. " +
                    "Specify target database via EF CLI --connection parameter, or configure ConnectionStrings:DefaultConnection in environment/appsettings.");
            }
        }
    }

    internal sealed class DesignTimeGuardConnectionInterceptor : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result)
        {
            ValidateConnection(connection);
            return base.ConnectionOpening(connection, eventData, result);
        }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            ValidateConnection(connection);
            return base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
        }

        private static void ValidateConnection(DbConnection connection)
        {
            if (connection.ConnectionString.Contains("designtime_guard_only"))
            {
                throw new InvalidOperationException(
                    "Cannot open database connection: design-time guard configuration is active. " +
                    "Specify target database via EF CLI --connection parameter, or configure ConnectionStrings:DefaultConnection in environment/appsettings.");
            }
        }
    }
}
