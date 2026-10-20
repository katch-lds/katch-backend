// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Katch.Backend.Repositories.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Katch.Backend.Repositories;

/// <summary>
/// Contexto do Entity Framework Core da base de dados <c>katch</c> e unidade de trabalho dos repositórios
/// (modelo de classes do backend, secção 4). As entidades são acrescentadas pelas Issues de cada tabela.
/// </summary>
public sealed class KatchDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KatchDbContext"/> class.
    /// </summary>
    /// <param name="options">Opções do contexto, com o fornecedor e os tipos enumerados.</param>
    public KatchDbContext(DbContextOptions<KatchDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets the accounts of the users (<c>app_user</c>).
    /// </summary>
    public DbSet<AppUser> Users => this.Set<AppUser>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        bool usesPostgres = this.Database.IsNpgsql();

        if (usesPostgres)
        {
            modelBuilder.HasKatchEnums();
        }

        modelBuilder.ApplyConfiguration(new AppUserConfiguration(usesPostgres));
    }
}
