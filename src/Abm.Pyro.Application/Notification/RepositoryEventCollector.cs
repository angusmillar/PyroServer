using Abm.Pyro.Application.Tenant;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Application.Notification;

public class RepositoryEventCollector(
    ITenantService tenantService,
    IDateTimeProvider dateTimeProvider) : IRepositoryEventCollector
{
    private readonly List<RepositoryEvent> _repositoryEventList = [];
    private DateTime? _eventTimestampUtc;
    
    public IReadOnlyCollection<RepositoryEvent> RepositoryEventList => _repositoryEventList.AsReadOnly();
    
    public void Add(RepositoryEvent repositoryEvent)
    {
        _repositoryEventList.Add(repositoryEvent);
    }
   
    public void Add(FhirResourceTypeId resourceType, string requestId, RepositoryEventType repositoryEventType, string resourceId)
    {
        if (_eventTimestampUtc is null)
        {
            _eventTimestampUtc = dateTimeProvider.Now.UtcDateTime;
        }
        _repositoryEventList.Add(new RepositoryEvent(
            ResourceType: resourceType,
            RequestId: requestId,
            RepositoryEventType: repositoryEventType, 
            ResourceId: resourceId, 
            Tenant: tenantService.GetScopedTenant(), 
            EventTimestampUtc: _eventTimestampUtc.Value));
    }
    
    public void Clear()
    {
        _repositoryEventList.Clear();
        _eventTimestampUtc = null;
    }
}