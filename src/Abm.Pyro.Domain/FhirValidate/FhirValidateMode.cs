namespace Abm.Pyro.Domain.FhirValidate;

public enum FhirValidateMode
{
    /// <summary>
    /// The server checks the content, and then checks that the content would be acceptable as a create (e.g. that the
    /// content would not violate any uniqueness constraints).
    /// </summary>
    Create,
    /// <summary>
    /// The server checks the content, and then checks that it would accept it as an update against the nominated
    /// specific resource (e.g. that there are no changes to immutable fields the server does not allow to change and checking version integrity if appropriate).
    /// </summary>
    Update,
    /// <summary>
    /// The server ignores the content and checks that the nominated resource is allowed to be deleted (e.g. checking
    /// referential integrity rules).
    /// </summary>
    Delete,
    /// <summary>
    /// The server checks an existing resource (must be nominated by id, not provided as a parameter) as valid against
    /// the nominated profile.
    /// </summary>
    Profile
}