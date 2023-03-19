using FluentValidation;

namespace Backend.Api.Settings;

internal class ApiSettingsValidator : AbstractValidator<ApiSettings>
{
    public ApiSettingsValidator()
    {
        // TODO: add rules for settings here
    }

    private static bool StringMustBeValidUrl(string arg)
    {
        return Uri.TryCreate(arg, UriKind.Absolute, out var resultUri)
            && resultUri.Scheme == Uri.UriSchemeHttps;
    }
}
