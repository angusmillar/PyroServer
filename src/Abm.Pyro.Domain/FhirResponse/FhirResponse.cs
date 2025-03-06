using System.Net;
using Abm.Pyro.Domain.Notification;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirResponse;

public abstract record FhirResponse(
    HttpStatusCode HttpStatusCode,
    Dictionary<string, StringValues> Headers,
    IRepositoryEventCollector RepositoryEventCollector,
    ResourceOutcomeInfo? ResourceOutcomeInfo = null,
    bool CanCommitTransaction = true) 
    : TransactionResponse(RepositoryEventCollector, CanCommitTransaction);

public abstract record TransactionResponse(
    IRepositoryEventCollector RepositoryEventCollector, 
    bool CanCommitTransaction = true);

