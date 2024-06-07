using Abm.Pyro.Domain.SearchQuery;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Domain.Support;

public interface IPaginationSupport
{
  int CalculatePageRequired(int? requiredPageNumber, int? countOfRecordsRequested, int totalRecordCount);
  int CalculateTotalPages(int? countOfRecordsRequested, int totalRecordCount);
  Task SetBundlePagination(Bundle bundle, SearchQueryServiceOutcome searchQueryServiceOutcome, string requestSchema, string requestPath,
    int pagesTotal, int pageCurrentlyRequired);
  int SetNumberOfRecordsPerPage(int? countOfRecordsRequested);
}
