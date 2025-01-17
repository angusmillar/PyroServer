using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class SearchModifierCodeEntityConfig : IEntityTypeConfiguration<SearchModifierCode>
{
    public void Configure(EntityTypeBuilder<SearchModifierCode> builder)
    {
        // SearchModifierCode ---------------------------------------------------------------
        builder.HasKey(x => x.SearchModifierCodeId);

        builder.Property(x => x.SearchModifierCodeId).HasConversion<int>();
        builder.Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);

        builder.HasData(
            Enum.GetValues(typeof(SearchModifierCodeId))
                .Cast<SearchModifierCodeId>()
                .Select(e => new SearchModifierCode(e, e.ToString())));
    }
}