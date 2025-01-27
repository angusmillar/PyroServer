using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Domain.Cache;

public interface IMetaDataCache
{
    Task<CapabilityStatement> GetCapabilityStatement();
    Task Remove();
}