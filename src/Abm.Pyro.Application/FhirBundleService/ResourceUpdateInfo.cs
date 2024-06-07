using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Application.FhirBundleService;

public record ResourceUpdateInfo(string ResourceName, string NewResourceId, int NewVersionId, CommittedResourceInfo? CommittedResourceInfo, ResourceStoreUpdateProjection? ResourceStoreUpdateProjection);

public record CommittedResourceInfo(Resource Resource, Dictionary<string, StringValues> Headers);