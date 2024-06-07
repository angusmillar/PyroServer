namespace Abm.Pyro.Application.EndpointPolicy;

public record EndpointPolicy(
    bool AllowCreate,
    bool AllowRead,
    bool AllowUpdate,
    bool AllowDelete,
    bool AllowSearch,
    bool AllowVersionRead,
    bool AllowHistory,
    bool AllowConditionalCreate,
    bool AllowConditionalUpdate,
    bool AllowConditionalDelete,
    bool AllowBaseTransaction,
    bool AllowBaseBatch,
    bool AllowBaseMetadata,
    bool AllowBaseHistory
);