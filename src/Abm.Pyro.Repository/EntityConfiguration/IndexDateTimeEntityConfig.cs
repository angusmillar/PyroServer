using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class IndexDateTimeEntityConfig : IEntityTypeConfiguration<IndexDateTime>
{
    public void Configure(EntityTypeBuilder<IndexDateTime> builder)
    {
        // IndexDateTime ---------------------------------------------------------------      
        builder.HasKey(x => x.IndexDateTimeId);
        builder.HasIndex(x => x.LowUtc);
        builder.HasIndex(x => x.HighUtc);
        
    }
}