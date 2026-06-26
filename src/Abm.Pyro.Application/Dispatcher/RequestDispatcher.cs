using Abm.Pyro.Domain.Dispatcher;
using Microsoft.Extensions.DependencyInjection;

namespace Abm.Pyro.Application.Dispatcher;

public class RequestDispatcher(IServiceProvider serviceProvider) : IRequestDispatcher
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        var wrapper = (IRequestHandlerWrapper<TResponse>)Activator.CreateInstance(wrapperType)!;
        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }
}

internal interface IRequestHandlerWrapper<TResponse>
{
    Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

internal class RequestHandlerWrapper<TRequest, TResponse> : IRequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse();

        RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle((TRequest)request, cancellationToken);

        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            var b = behavior;
            pipeline = () => b.Handle((TRequest)request, next, cancellationToken);
        }

        return await pipeline();
    }
}
