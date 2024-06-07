using System.Net;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirResponse;

public abstract record FhirResponse(HttpStatusCode HttpStatusCode, Dictionary<string, StringValues> Headers, ResourceOutcomeInfo? ResourceOutcomeInfo = null, bool CanCommitTransaction = true) 
    : TransactionResponse(CanCommitTransaction);

public abstract record TransactionResponse(bool CanCommitTransaction = true);

