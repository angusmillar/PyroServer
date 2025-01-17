using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class HttpVerbEntityConfig : IEntityTypeConfiguration<HttpVerb>
{
    public void Configure(EntityTypeBuilder<HttpVerb> builder)
    {
        // HttpVerb -----------------------------------------------------------------      
        builder.HasKey(x => x.HttpVerbId);
      
        builder.Property(x => x.HttpVerbId).HasConversion<int>();
        builder.Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);

        builder.HasData(
            Enum.GetValues(typeof(HttpVerbId))
                .Cast<HttpVerbId>()
                .Select(e => new HttpVerb(e, e.ToString())));
    }
}