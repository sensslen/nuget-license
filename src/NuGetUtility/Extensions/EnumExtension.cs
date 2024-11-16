// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.ComponentModel;
using System.Reflection;

namespace NuGetUtility.Extensions
{
    public static class EnumExtension
    {
        public static string? GetDescription(this Enum value)
        {
            Type type = value.GetType();
            if (Enum.GetName(type, value) is not string name)
            {
                return null;
            }

            if (type.GetField(name) is not FieldInfo field)
            {
                return null;
            }

            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attr)
            {
                return attr.Description;
            }
            return null;
        }

    }
}
