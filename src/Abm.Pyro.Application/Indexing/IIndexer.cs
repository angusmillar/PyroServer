using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Indexing;

namespace Abm.Pyro.Application.Indexing;

public interface IIndexer
{
  Task<IndexerOutcome> Process(Resource fhirResource, FhirResourceTypeId resourceType);
}
