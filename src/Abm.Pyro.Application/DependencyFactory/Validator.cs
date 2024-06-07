using Microsoft.Extensions.DependencyInjection;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.DependencyFactory;

public class Validator(IServiceProvider serviceProvider) : IValidator
{
    public ValidatorResult Validate<T>(T item) where T : IValidatable
    {
        var validator = serviceProvider.GetRequiredService<IValidatorBase<T>>();
        return validator.Validate(item);
    }
}