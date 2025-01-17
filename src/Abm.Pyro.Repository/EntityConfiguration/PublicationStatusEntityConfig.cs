using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abm.Pyro.Repository.EntityConfiguration;

public class PublicationStatusEntityConfig : IEntityTypeConfiguration<PublicationStatus>
{
    public void Configure(EntityTypeBuilder<PublicationStatus> builder)
    {
        // PublicationStatus --------------------------------------------------------
        builder.HasKey(x => x.PublicationStatusId);
      
        builder.Property(x => x.PublicationStatusId).HasConversion<int>();
        builder.Property(x => x.Name).HasMaxLength(RepositoryModelConstraints.CodeMaxLength);

        builder.HasData(
            Enum.GetValues(typeof(PublicationStatusId))
                .Cast<PublicationStatusId>()
                .Select(e => new PublicationStatus(e, e.ToString())));
    }
}