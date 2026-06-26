namespace Abm.Pyro.Domain.Dispatcher;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();
