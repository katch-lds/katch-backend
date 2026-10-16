// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Katch.Backend.Repositories.Configuration;

/// <summary>
/// Mapeamento da entidade <see cref="Recruiter"/> para a tabela <c>recruiter</c> (modelo de dados v02, secções 5.2 e 6.3).
/// A chave primária é também chave estrangeira para <c>app_user</c>, na relação 1 : 0..1.
/// </summary>
public sealed class RecruiterConfiguration : IEntityTypeConfiguration<Recruiter>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Recruiter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("recruiter");

        builder.HasKey(recruiter => recruiter.Id);

        builder.Property(recruiter => recruiter.Id)
            .ValueGeneratedNever();

        builder.HasOne(recruiter => recruiter.User)
            .WithOne()
            .HasForeignKey<Recruiter>(recruiter => recruiter.Id)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
