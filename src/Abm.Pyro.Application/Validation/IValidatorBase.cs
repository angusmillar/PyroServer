namespace Abm.Pyro.Domain.Validation;

public interface IValidatorBase<in T> where T : IValidatable
{
     ValidatorResult Validate(T item); 
}




