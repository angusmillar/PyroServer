using Abm.Pyro.Domain.Projection;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirBundleService;

public record ResourceUpdateInfo(string ResourceName, string NewResourceId, int NewVersionId, CommittedResourceInfo? CommittedResourceInfo, ResourceStoreUpdateProjection? ResourceStoreUpdateProjection);

public record CommittedResourceInfo(Resource Resource, Dictionary<string, StringValues> Headers);