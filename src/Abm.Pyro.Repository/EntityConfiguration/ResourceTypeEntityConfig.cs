using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class ResourceTypeEntityConfig : IEntityTypeConfiguration<ResourceType>
{
    public void Configure(EntityTypeBuilder<ResourceType> builder)
    {
        // ResourceType ---------------------------------------------------------------      
        builder.HasKey(x => x.FhirResourceType);
      
        builder.Property(x => x.FhirResourceType).HasConversion<int>();
        builder.Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.FhirResourceNameMaxLength);

        builder.HasData(
            Enum.GetValues(typeof(FhirResourceTypeId))
                .Cast<FhirResourceTypeId>()
                .Select(e => new ResourceType(e, e.ToString())));
    }
}