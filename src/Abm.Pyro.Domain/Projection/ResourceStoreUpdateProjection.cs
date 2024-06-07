namespace Abm.Pyro.Domain.Projection;

public class ResourceStoreUpdateProjection
{
    public ResourceStoreUpdateProjection(
        int? resourceStoreId,
        int versionId,
        bool isCurrent,
        bool isDeleted)
    {
        ResourceStoreId = resourceStoreId;
        VersionId = versionId;
        IsCurrent = isCurrent;
        IsDeleted = isDeleted;
    }

    public int? ResourceStoreId { get; set; }
    public int VersionId { get; set; }
    public bool IsCurrent { get; set; }
    
    public bool IsDeleted { get; set; }
}