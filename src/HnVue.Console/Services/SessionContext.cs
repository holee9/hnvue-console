using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Singleton session context holding the current authenticated user session.
/// SPEC-UI-003: Login/Auth UI - session state for RBAC and navigation.
/// </summary>
public class SessionContext : ISessionContext
{
    private UserSession? _currentSession;

    /// <inheritdoc/>
    public UserSession? CurrentSession => _currentSession;

    /// <inheritdoc/>
    public UserRole CurrentRole => _currentSession?.User?.Role ?? UserRole.Unspecified;

    /// <inheritdoc/>
    public void SetSession(UserSession session) => _currentSession = session;

    /// <inheritdoc/>
    public void ClearSession() => _currentSession = null;
}
