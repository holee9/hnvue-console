using System.Windows.Threading;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// ViewModel for the login screen.
/// SPEC-UI-003: GAP-11-01 Login screen UI — authenticates user via IUserService.AuthenticateAsync.
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private readonly IUserService _userService;
    private readonly ISessionContext _sessionContext;

    private string _username = "";
    private string _password = "";
    private string _errorMessage = "";
    private int _remainingAttempts = 5;
    private bool _isLocked;
    private bool _isBusy;

    /// <summary>Gets or sets the username input (matched against User.UserName).</summary>
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    /// <summary>Gets or sets the password input.</summary>
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    /// <summary>Gets or sets the authentication error message.</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    /// <summary>Gets or sets the remaining login attempts before lockout.</summary>
    public int RemainingAttempts
    {
        get => _remainingAttempts;
        set => SetProperty(ref _remainingAttempts, value);
    }

    /// <summary>Gets or sets whether the account is locked out.</summary>
    public bool IsLocked
    {
        get => _isLocked;
        set => SetProperty(ref _isLocked, value);
    }

    /// <summary>Gets or sets whether a login attempt is in progress.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Gets whether there is an error message to display.</summary>
    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    /// <summary>Gets the login command.</summary>
    public AsyncRelayCommand LoginCommand { get; }

    /// <summary>Raised when authentication succeeds, carrying the new session.</summary>
    public event EventHandler<UserSession>? LoginSucceeded;

    /// <summary>
    /// Initializes a new instance of <see cref="LoginViewModel"/>.
    /// </summary>
    public LoginViewModel(IUserService userService, ISessionContext sessionContext)
    {
        _userService = userService;
        _sessionContext = sessionContext;

        LoginCommand = new AsyncRelayCommand(
            ExecuteLoginAsync,
            () => !IsBusy && !IsLocked && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password),
            onError: ex => ErrorMessage = $"Login error: {ex.Message}",
            dispatcher: null);
    }

    private async Task ExecuteLoginAsync(CancellationToken ct)
    {
        IsBusy = true;
        ErrorMessage = "";

        try
        {
            var result = await _userService.AuthenticateAsync(Username, Password, "CONSOLE-WS", ct);

            if (result.Success && result.Session != null)
            {
                _sessionContext.SetSession(result.Session);
                LoginSucceeded?.Invoke(this, result.Session);
            }
            else
            {
                RemainingAttempts = result.RemainingAttempts;
                // Account is locked if explicitly told so, or if attempts are exhausted
                IsLocked = result.FailureReason == AuthenticationFailureReason.AccountLocked
                           || result.RemainingAttempts == 0;
                ErrorMessage = result.FailureReason switch
                {
                    AuthenticationFailureReason.AccountLocked =>
                        "Account locked. Please try again in 15 minutes.",
                    AuthenticationFailureReason.InvalidCredentials when result.RemainingAttempts > 0 =>
                        $"Invalid username or password. {result.RemainingAttempts} attempt(s) remaining.",
                    _ => "Account locked due to too many failed attempts."
                };
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
