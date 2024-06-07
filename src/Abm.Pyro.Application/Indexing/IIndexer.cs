using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Enums;
namespace Abm.Pyro.Application.Indexing;

public interface IIndexer
{
  Task<IndexerOutcome> Process(Resource fhirResource, FhirResourceTypeId resourceType);
}
