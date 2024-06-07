using System.Reflection;
using Abm.Pyro.Domain.Attributes;

namespace Abm.Pyro.Domain.Enums;

  public static class EnumCode
  {
    public static string GetDescription(this Enum value)
    {
      Type type = value.GetType();
      string? name = Enum.GetName(type, value);
      if (name is not null)
      {
        FieldInfo? field = type.GetField(name);
        if (field is not null)
        {
          if (Attribute.GetCustomAttribute(field, typeof(EnumInfoAttribute)) is EnumInfoAttribute attr)
          {
            return attr.Description;
          }
          throw new ApplicationException($"No 'Code' attribute found for enum type '{type.Name}'. ");
        }
        throw new ApplicationException($"No 'filed' found for enum type '{type.Name}' with the value '{name}'. ");
      }
      throw new ApplicationException($"No 'Name' found for enum type '{type.Name}' and value '{value.ToString()}'. ");
    }

    public static string GetCode(this Enum value)
    {
      Type type = value.GetType();
      string? name = Enum.GetName(type, value);
      if (name is not null)
      {
        FieldInfo? field = type.GetField(name);
        if (field is not null)
        {
          if (Attribute.GetCustomAttribute(field, typeof(EnumInfoAttribute)) is EnumInfoAttribute attr)
          {
            return attr.Code;
          }
          throw new ApplicationException($"No 'Code' attribute found for enum type '{type.Name}'. ");
        }
        throw new ApplicationException($"No 'filed' found for enum type '{type.Name}' with the value '{name}'. ");
      }
      throw new ApplicationException($"No 'Name' found for enum type '{type.Name}' and value '{value.ToString()}'. ");
    }
  }

