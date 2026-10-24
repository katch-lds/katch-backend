// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Katch.Backend.UnitTests.Repositories.Configuration;

/// <summary>
/// Modelo com as configurações aplicadas como no SQLite dos testes de integração (<c>usesPostgres</c> falso), ou seja,
/// sem os <c>CHECK</c>, os filtros dos índices e a identidade específicos do PostgreSQL. Os testes unitários não
/// referenciam o SQLite, por isso o modelo é construído sobre o Npgsql, sem abrir ligação.
/// </summary>
internal static class ModelWithoutPostgresFeatures
{
    /// <summary>
    /// Obtém o tipo de entidade do modelo sem as funcionalidades do PostgreSQL.
    /// </summary>
    /// <typeparam name="TEntity">Tipo da entidade.</typeparam>
    /// <returns>O tipo de entidade.</returns>
    public static IEntityType GetEntity<TEntity>()
        where TEntity : class
    {
        var builder = new DbContextOptionsBuilder<Context>();
        builder.UseKatchPostgres("Host=localhost;Database=katch");

        using var context = new Context(builder.Options);
        return context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TEntity))!;
    }

    private sealed class Context : DbContext
    {
        public Context(DbContextOptions<Context> options)
            : base(options)
        {
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            const bool usesPostgres = false;

            modelBuilder.ApplyConfiguration(new AppUserConfiguration(usesPostgres));
            modelBuilder.ApplyConfiguration(new AdminConfiguration());
            modelBuilder.ApplyConfiguration(new RecruiterConfiguration());
            modelBuilder.ApplyConfiguration(new LocationConfiguration(usesPostgres));
            modelBuilder.ApplyConfiguration(new CandidateConfiguration(usesPostgres));
            modelBuilder.ApplyConfiguration(new CandidateLinkConfiguration(usesPostgres));
            modelBuilder.ApplyConfiguration(new CompanyConfiguration(usesPostgres));
            modelBuilder.ApplyConfiguration(new JobConfiguration(usesPostgres));
            modelBuilder.ApplyConfiguration(new MatchConfiguration(usesPostgres));
            modelBuilder.ApplyConfiguration(new MessageConfiguration(usesPostgres));
            modelBuilder.ApplyConfiguration(new OperationLogConfiguration(usesPostgres));
        }
    }
}
