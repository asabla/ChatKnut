using FluentValidation;

using Microsoft.Extensions.Options;

namespace Backend.Api.Extensions;

internal static class SettingsValidationExtensions
{
    public static OptionsBuilder<TOptions> ValidateWithFluent<TOptions>(
        this OptionsBuilder<TOptions> optionsBuilder)
        where TOptions : class
    {
        optionsBuilder.Services.AddSingleton<IValidateOptions<TOptions>>(
            provider => new FluentValidationOptions<TOptions>(
                name: optionsBuilder.Name,
                serviceProvider: provider));

        return optionsBuilder;
    }
}

internal class FluentValidationOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string? _name;

    public FluentValidationOptions(string? name, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _name = name;
    }

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        if (!string.IsNullOrWhiteSpace(_name) && !_name.Equals(name))
            return ValidateOptionsResult.Skip;

        ArgumentNullException.ThrowIfNull(options);

        using var scope = _serviceProvider.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<TOptions>>();

        var results = validator.Validate(options);

        if (results.IsValid)
            return ValidateOptionsResult.Success;

        var typeName = options.GetType().Name;
        var errors = new List<string>();

        foreach (var result in results.Errors)
        {
            errors.Add($"Validation error: '{typeName}.{result.PropertyName}' " +
                $"with errors: '{result.ErrorMessage}");
        }

        return ValidateOptionsResult.Fail(errors);
    }
}