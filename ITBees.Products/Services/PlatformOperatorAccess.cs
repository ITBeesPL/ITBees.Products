using ITBees.RestfulApiControllers.Exceptions;
using ITBees.RestfulApiControllers.Models;
using ITBees.UserManager.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ITBees.Products.Services;

internal static class PlatformOperatorAccess
{
    /// <summary>The warehouse module is an internal back-office tool - platform operators only.</summary>
    public static void ThrowIfNotPlatformOperator(this IAspCurrentUserService currentUserService)
    {
        if (!currentUserService.CurrentUserIsPlatformOperator())
        {
            throw new FasApiErrorException(new FasApiErrorVm(
                "You don't have enough rights to access this data.", StatusCodes.Status403Forbidden));
        }
    }
}
