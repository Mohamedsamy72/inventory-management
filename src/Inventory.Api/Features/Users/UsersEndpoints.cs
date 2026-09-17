using Inventory.Api.Errors;
using Inventory.Application.Users;
using Inventory.Domain.Enums;

namespace Inventory.Api.Features.Users;

public sealed record CreateUserRequest(string FullName, string MobileNumber, string Password, RoleName Role);
public sealed record ChangeUserRoleRequest(RoleName Role);
public sealed record SetUserScopeRequest(IReadOnlyList<Guid> WarehouseIds, IReadOnlyList<Guid> RestaurantIds);
public sealed record PermissionGrantRequest(string Code, bool IsGranted);
public sealed record SetUserPermissionsRequest(IReadOnlyList<PermissionGrantRequest> Permissions);

/// <summary>Task 4.14 - user provisioning, role assignment, scope assignment, and permission
/// overrides. Every command DTO above deliberately has no <c>companyId</c>, <c>userId</c>, or
/// <c>createdAt</c> property (task 4.9): the tenant is always <see cref="Application.Common.ICurrentUserService"/>'s,
/// and the target user is always the route id, never a body field a client could redirect.
/// </summary>
public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder users = app.MapGroup("/api/v1/users");

        users.MapPost("/", CreateUserAsync).RequireAuthorization("users:manage");
        users.MapGet("/", ListUsersAsync).RequireAuthorization("users:view");
        users.MapGet("/{id:guid}", GetUserAsync).RequireAuthorization("users:view");
        users.MapPut("/{id:guid}/role", ChangeRoleAsync).RequireAuthorization("users:manage");
        users.MapPut("/{id:guid}/scope", SetScopeAsync).RequireAuthorization("users:scope");
        users.MapPut("/{id:guid}/permissions", SetPermissionsAsync).RequireAuthorization("users:manage");

        return app;
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request, HttpContext httpContext, IUserManagementService service)
    {
        var command = new CreateUserCommand(request.FullName, request.MobileNumber, request.Password, request.Role);
        UserManagementResult<UserSummary> result = await service.CreateUserAsync(command, httpContext.RequestAborted);

        if (result.Succeeded)
        {
            return Results.Created($"/api/v1/users/{result.Value!.Id}", result.Value);
        }

        return await WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ListUsersAsync(HttpContext httpContext, IUserManagementService service) =>
        Results.Ok(await service.ListUsersAsync(httpContext.RequestAborted));

    private static async Task<IResult> GetUserAsync(Guid id, HttpContext httpContext, IUserManagementService service)
    {
        UserSummary? user = await service.GetUserAsync(id, httpContext.RequestAborted);
        return user is null ? Results.NotFound() : Results.Ok(user);
    }

    private static async Task<IResult> ChangeRoleAsync(
        Guid id, ChangeUserRoleRequest request, HttpContext httpContext, IUserManagementService service)
    {
        UserManagementResult<UserSummary> result = await service.ChangeRoleAsync(id, request.Role, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> SetScopeAsync(
        Guid id, SetUserScopeRequest request, HttpContext httpContext, IUserManagementService service)
    {
        var scope = new UserScope(request.WarehouseIds, request.RestaurantIds);
        UserManagementResult<UserScope> result = await service.SetScopeAsync(id, scope, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> SetPermissionsAsync(
        Guid id, SetUserPermissionsRequest request, HttpContext httpContext, IUserManagementService service)
    {
        List<PermissionGrant> grants = request.Permissions
            .Select(p => new PermissionGrant(p.Code, p.IsGranted))
            .ToList();

        UserManagementResult<IReadOnlyList<string>> result = await service.SetPermissionsAsync(id, grants, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> WriteErrorAsync(HttpContext httpContext, UserManagementError error)
    {
        (int statusCode, string code) = error switch
        {
            UserManagementError.NotFound => (StatusCodes.Status404NotFound, string.Empty),
            UserManagementError.NonGrantablePermission => (StatusCodes.Status400BadRequest, ErrorCodes.NonGrantablePermission),
            UserManagementError.PrivilegeEscalation => (StatusCodes.Status403Forbidden, ErrorCodes.PrivilegeEscalationDenied),
            UserManagementError.IdentityCreationFailed => (StatusCodes.Status400BadRequest, ErrorCodes.InvalidPassword),
            _ => (StatusCodes.Status400BadRequest, ErrorCodes.InvalidPassword),
        };

        if (statusCode == StatusCodes.Status404NotFound)
        {
            return Results.NotFound();
        }

        await ProblemResponseWriter.WriteAsync(httpContext, statusCode, code);
        return Results.Empty;
    }
}
