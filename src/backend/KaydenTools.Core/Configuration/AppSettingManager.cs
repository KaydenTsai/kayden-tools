using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace KaydenTools.Core.Configuration;

/// <summary>
/// 應用程式設定管理器
/// </summary>
public class AppSettingManager
{
    private readonly IConfiguration _configuration;

    public AppSettingManager(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public T GetSettings<T>() where T : class, new()
    {
        var settings = new T();
        var properties = typeof(T).GetProperties();

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<SettingPropertyAttribute>();
            if (attribute == null)
            {
                continue;
            }

            var value = _configuration[attribute.Key];

            if (string.IsNullOrEmpty(value))
            {
                if (attribute.Required && attribute.DefaultValue == null)
                {
                    throw new InvalidOperationException(
                        $"Required configuration key '{attribute.Key}' is missing.");
                }

                if (attribute.DefaultValue != null)
                {
                    property.SetValue(settings, Convert.ChangeType(attribute.DefaultValue, property.PropertyType));
                }
                continue;
            }

            var convertedValue = ConvertValue(value, property.PropertyType);
            property.SetValue(settings, convertedValue);
        }

        ValidateSettings(settings);
        return settings;
    }

    private static object? ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string))
            return value;

        if (targetType == typeof(int))
            return int.Parse(value);

        if (targetType == typeof(long))
            return long.Parse(value);

        if (targetType == typeof(bool))
            return bool.Parse(value);

        if (targetType == typeof(double))
            return double.Parse(value);

        if (targetType == typeof(decimal))
            return decimal.Parse(value);

        if (targetType == typeof(TimeSpan))
            return TimeSpan.Parse(value);

        if (targetType.IsEnum)
            return Enum.Parse(targetType, value, ignoreCase: true);

        throw new NotSupportedException($"Type '{targetType}' is not supported for configuration binding.");
    }

    private static void ValidateSettings<T>(T settings) where T : class
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(settings);

        if (!Validator.TryValidateObject(settings, validationContext, validationResults, validateAllProperties: true))
        {
            var errors = string.Join(Environment.NewLine, validationResults.Select(r => r.ErrorMessage));
            throw new ValidationException($"Configuration validation failed:{Environment.NewLine}{errors}");
        }
    }
}
