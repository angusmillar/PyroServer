using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class ServiceSettingTypeEntityConfig : IEntityTypeConfiguration<ServiceSettingType>
{
    public void Configure(EntityTypeBuilder<ServiceSettingType> builder)
    {
        // ResourceType ---------------------------------------------------------------      
        builder.HasKey(x => x.ServiceSettingTypeId);
      
        builder.Property(x => x.ServiceSettingTypeId).HasConversion<int>();
        builder.Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);

        builder.HasData(
            Enum.GetValues(typeof(ServiceSettingTypeId))
                .Cast<ServiceSettingTypeId>()
                .Select(e => new ServiceSettingType(e, e.ToString())));
    }
}