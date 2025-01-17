using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class ComparatorEntityConfig : IEntityTypeConfiguration<Comparator>
{
    public void Configure(EntityTypeBuilder<Comparator> builder)
    {
        // Comparator ---------------------------------------------------------------
        builder.HasKey(x => x.SearchComparatorId);
      
        builder.Property(x => x.SearchComparatorId).HasConversion<int>();
        builder.Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);

        builder.HasData(
            Enum.GetValues(typeof(SearchComparatorId))
                .Cast<SearchComparatorId>()
                .Select(e => new Comparator(e, e.ToString())));
    }
}