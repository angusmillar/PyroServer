using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

public enum RepositoryEventType
{
    [EnumInfo("create", "Create" )]
    Create = 1,
    [EnumInfo("read", "Read" )]
    Read = 2,
    [EnumInfo("update", "Update" )]
    Update = 3,
    [EnumInfo("delete", "Delete" )]
    Delete = 4,
}