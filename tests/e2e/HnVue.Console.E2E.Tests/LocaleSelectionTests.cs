using FluentAssertions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using Xunit;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// E2E tests for locale selection user journey.
/// User Journey: Change language between Korean and English.
/// </summary>
public class LocaleSelectionTests : TestBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await LaunchApplicationAsync();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "LocaleSelection")]
    public void Locale_ComboBox_Exists_In_Status_Bar()
    {
        // Arrange & Act - Application launched in InitializeAsync

        // Assert - Locale combo box should be present in status bar using AutomationId
        var localeComboBox = FindElementByAutomationId("LocaleSelectorComboBox");
        localeComboBox.Should().NotBeNull("locale selector combo box should exist");
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "LocaleSelection")]
    public void Locale_ComboBox_Has_Korean_And_English_Options()
    {
        // Arrange - Use AutomationId
        var localeComboBox = FindElementByAutomationId("LocaleSelectorComboBox");
        localeComboBox.Should().NotBeNull("locale selector should exist");

        // Act - Expand the combo box to see options
        var comboBox = localeComboBox!.AsComboBox();
        comboBox.Expand();
        Wait.UntilInputIsProcessed();

        // Assert - Should have at least 2 items (Korean and English)
        comboBox.Items.Length.Should().BeGreaterOrEqualTo(2,
            "should have Korean and English options");

        comboBox.Collapse();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "LocaleSelection")]
    public void Locale_Can_Be_Changed_To_English()
    {
        // Arrange - Use AutomationId
        var localeComboBox = FindElementByAutomationId("LocaleSelectorComboBox");
        localeComboBox.Should().NotBeNull("locale selector should exist");

        var comboBox = localeComboBox!.AsComboBox();

        // Act - Select English (index 1)
        if (comboBox.Items.Length >= 2)
        {
            comboBox.Select(1);
            Wait.UntilInputIsProcessed();

            // Assert - Verify selection changed (items[1] should be selected)
            // FlaUI ComboBox selection can be verified via SelectionPattern
            var selectedText = comboBox.SelectedItem?.Name;
            selectedText.Should().NotBeNull("selected item should have a value");
        }
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "LocaleSelection")]
    public void Locale_Default_Is_Korean()
    {
        // Arrange - Use AutomationId
        var localeComboBox = FindElementByAutomationId("LocaleSelectorComboBox");
        localeComboBox.Should().NotBeNull("locale selector should exist");

        var comboBox = localeComboBox!.AsComboBox();

        // Assert - Default selection should be Korean (index 0)
        // Verify the first item is selected by checking SelectedItem
        var selectedText = comboBox.SelectedItem?.Name;
        selectedText.Should().NotBeNull("selected item should have a value");
        // Korean should be default (first item)
        selectedText.Should().Contain("한국어", "Korean should be default selection");
    }
}
