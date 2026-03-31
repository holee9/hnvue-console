using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Holds the current authenticated user session accessible throughout the application.
/// SPEC-UI-003: Login/Auth UI - session state for RBAC and navigation.
/// </summary>
public interface ISessionContext
{
    /// <summary>Gets the current active session, or null if not authenticated.</summary>
    UserSession? CurrentSession { get; }

    /// <summary>Gets the current user's role, or Unspecified if not authenticated.</summary>
    UserRole CurrentRole { get; }

    /// <summary>Sets the active session after successful authentication.</summary>
    void SetSession(UserSession session);

    /// <summary>Clears the session on logout.</summary>
    void ClearSession();
}
