using Abm.Pyro.Domain.Dispatcher;

namespace Abm.Pyro.Application.Dispatcher;

public interface IRequestDispatcher
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
