using BusinessOS.Application.Configurations.Exceptions;
using FluentValidation;

namespace BusinessOS.WebApi.Extensions;

public static class ValidationExtensions
{
    public static async Task ValidateAndThrowAppAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (!result.IsValid)
        {
            throw new ValidationAppException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
