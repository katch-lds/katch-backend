// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="Admin"/> para a tabela <c>admin</c> (modelo de dados v02, secções 5.2 e 6.2).
/// A chave primária é também chave estrangeira para <c>app_user</c>, na relação 1 : 0..1.
/// </summary>
public sealed class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("admin");

        builder.HasKey(admin => admin.Id);

        builder.Property(admin => admin.Id)
            .ValueGeneratedNever();

        builder.HasOne(admin => admin.User)
            .WithOne()
            .HasForeignKey<Admin>(admin => admin.Id)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
