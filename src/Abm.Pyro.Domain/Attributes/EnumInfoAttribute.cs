namespace Abm.Pyro.Domain.Attributes;

  [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
  public sealed class EnumInfoAttribute(string code, string description) : Attribute
  {

    // This is a positional argument

    public EnumInfoAttribute(string code) : this(code, "Enum description not defined")
    {
    }

    public string Code { get; } = code;

    public string Description { get; } = description;
  }

