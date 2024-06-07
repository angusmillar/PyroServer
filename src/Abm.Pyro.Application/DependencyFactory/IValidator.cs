using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.DependencyFactory;

public interface IValidator
{
    ValidatorResult Validate<T>(T item) where T : IValidatable;
}