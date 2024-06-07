namespace Abm.Pyro.Repository.Extensions;

public static class QueryableExtensions
{
  public static IQueryable<T> Paging<T>(this IQueryable<T> query, int pagRequested, int numberOfRecordsPerPage)
  {
    if (pagRequested > 0)
    {
      pagRequested = pagRequested - 1;
    }
    return query.Skip(pagRequested * numberOfRecordsPerPage).Take(numberOfRecordsPerPage);
  }
}
