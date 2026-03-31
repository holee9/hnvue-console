using System.Windows;
using System.Windows.Input;
using HnVue.Console.Models;
using HnVue.Console.ViewModels;

namespace HnVue.Console.Shell;

/// <summary>
/// Login window — gated entry point for HnVue Console.
/// SPEC-UI-003: GAP-11-01 Login screen UI.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.LoginSucceeded += OnLoginSucceeded;
        PasswordBox.KeyDown += OnPasswordKeyDown;
    }

    /// <summary>
    /// Opens MainWindow and closes this window on successful authentication.
    /// </summary>
    private void OnLoginSucceeded(object? sender, UserSession session)
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
        Close();
    }

    /// <summary>
    /// Bridges WPF PasswordBox (no two-way binding) to ViewModel.Password on click.
    /// </summary>
    private void OnLoginButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;
        if (_viewModel.LoginCommand.CanExecute(null))
        {
            _viewModel.LoginCommand.Execute(null);
        }
    }

    /// <summary>Triggers login on Enter key press in the password field.</summary>
    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            _viewModel.Password = PasswordBox.Password;
            if (_viewModel.LoginCommand.CanExecute(null))
            {
                _viewModel.LoginCommand.Execute(null);
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.LoginSucceeded -= OnLoginSucceeded;
        PasswordBox.KeyDown -= OnPasswordKeyDown;
        base.OnClosed(e);
    }
}
