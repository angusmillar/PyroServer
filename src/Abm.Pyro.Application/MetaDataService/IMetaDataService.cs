using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.MetaDataService;

public interface IMetaDataService
{
    System.Threading.Tasks.Task<CapabilityStatement> GetCapabilityStatement();
}