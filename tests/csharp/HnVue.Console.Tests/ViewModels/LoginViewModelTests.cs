using HnVue.Console.Models;
using HnVue.Console.Services;
using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Moq;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for LoginViewModel.
/// SPEC-UI-003: GAP-11-01 Login screen UI — authentication business logic.
/// </summary>
public class LoginViewModelTests : ViewModelTestBase
{
    private static LoginViewModel CreateSut(
        IUserService? userService = null,
        ISessionContext? sessionContext = null)
    {
        userService ??= new MockUserService();
        sessionContext ??= new SessionContext();
        return new LoginViewModel(userService, sessionContext);
    }

    [Fact]
    public void Can_Be_Constructed()
    {
        var vm = CreateSut();
        Assert.NotNull(vm);
        Assert.Equal("", vm.Username);
        Assert.Equal("", vm.Password);
        Assert.Equal("", vm.ErrorMessage);
        Assert.False(vm.HasError);
        Assert.False(vm.IsLocked);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void Login_With_Valid_Credentials_Raises_LoginSucceeded()
    {
        // Arrange
        var vm = CreateSut();
        UserSession? receivedSession = null;
        vm.LoginSucceeded += (_, session) => receivedSession = session;
        vm.Username = "System Administrator";
        vm.Password = "password123";

        // Act
        vm.LoginCommand.Execute(null);

        // Assert
        Assert.NotNull(receivedSession);
        Assert.Equal(UserRole.Administrator, receivedSession.User.Role);
    }

    [Fact]
    public void Login_With_Valid_Credentials_Sets_SessionContext()
    {
        // Arrange
        var sessionContext = new SessionContext();
        var vm = CreateSut(sessionContext: sessionContext);
        vm.Username = "System Administrator";
        vm.Password = "password123";

        // Act
        vm.LoginCommand.Execute(null);

        // Assert
        Assert.NotNull(sessionContext.CurrentSession);
        Assert.Equal(UserRole.Administrator, sessionContext.CurrentRole);
    }

    [Fact]
    public void Login_With_Wrong_Password_Sets_ErrorMessage()
    {
        // Arrange
        var vm = CreateSut();
        vm.Username = "System Administrator";
        vm.Password = "wrongpassword";

        // Act
        vm.LoginCommand.Execute(null);

        // Assert
        Assert.True(vm.HasError);
        Assert.Contains("Invalid", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Login_With_Unknown_Username_Sets_ErrorMessage()
    {
        // Arrange
        var vm = CreateSut();
        vm.Username = "nonexistent";
        vm.Password = "password123";

        // Act
        vm.LoginCommand.Execute(null);

        // Assert
        Assert.True(vm.HasError);
    }

    [Fact]
    public void Login_After_5_Failures_Sets_IsLocked()
    {
        // Arrange
        var vm = CreateSut();
        vm.Username = "System Administrator";

        // Exhaust attempts (MockUserService locks after 5 failures)
        for (int i = 0; i < 5; i++)
        {
            vm.Password = "wrong";
            vm.LoginCommand.Execute(null);
        }

        // Assert
        Assert.True(vm.IsLocked);
        Assert.Contains("lock", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HasError_Is_False_Initially()
    {
        var vm = CreateSut();
        Assert.False(vm.HasError);
    }

    [Fact]
    public void HasError_Becomes_True_On_Failure()
    {
        var vm = CreateSut();
        vm.Username = "System Administrator";
        vm.Password = "bad";

        var changedProperties = GetChangedProperties(vm, () =>
            vm.LoginCommand.Execute(null));

        Assert.True(vm.HasError);
        Assert.Contains(nameof(vm.HasError), changedProperties);
    }

    [Fact]
    public void Login_Technologist_Role_Is_Not_Admin()
    {
        // Arrange
        var sessionContext = new SessionContext();
        var vm = CreateSut(sessionContext: sessionContext);
        vm.Username = "Technician Johnson";
        vm.Password = "password123";

        // Act
        vm.LoginCommand.Execute(null);

        // Assert
        Assert.Equal(UserRole.Technologist, sessionContext.CurrentRole);
        Assert.False(sessionContext.CurrentRole is UserRole.Administrator or UserRole.Service);
    }
}
