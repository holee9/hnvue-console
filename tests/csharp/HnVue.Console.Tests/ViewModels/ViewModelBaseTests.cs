using HnVue.Console.Tests.TestHelpers;
using HnVue.Console.ViewModels;
using Xunit;

namespace HnVue.Console.Tests.ViewModels;

/// <summary>
/// Unit tests for ViewModelBase.
/// SPEC-UI-001: MVVM infrastructure tests.
/// </summary>
public class ViewModelBaseTests
{
    public class TheSetPropertyMethod
    {
        [Fact]
        public void Returns_False_When_Value_Is_Same()
        {
            // Arrange
            var viewModel = new TestViewModel();
            var initialCallCount = 0;
            viewModel.PropertyChanged += (s, e) => initialCallCount++;

            // Act
            var result = viewModel.SetTestValue("Initial");

            // Assert
            Assert.True(result);
            Assert.Equal(1, initialCallCount);

            // Act again with same value
            result = viewModel.SetTestValue("Initial");

            // Assert
            Assert.False(result);
            Assert.Equal(1, initialCallCount); // No additional notification
        }

        [Fact]
        public void Returns_True_And_Raises_PropertyChanged_When_Value_Changes()
        {
            // Arrange
            var viewModel = new TestViewModel();
            var callCount = 0;
            string? changedProperty = null;
            viewModel.PropertyChanged += (s, e) =>
            {
                callCount++;
                changedProperty = e.PropertyName;
            };

            // Act
            var result = viewModel.SetTestValue("NewValue");

            // Assert
            Assert.True(result);
            Assert.Equal(1, callCount);
            Assert.Equal("TestValue", changedProperty);
        }

        [Fact]
        public void Supports_Default_Values()
        {
            // Arrange & Act
            var viewModel = new TestViewModel();

            // Assert
            Assert.Null(viewModel.TestValue);
        }

        [Fact]
        public void Supports_Value_Types()
        {
            // Arrange
            var viewModel = new TestViewModel();
            var callCount = 0;
            viewModel.PropertyChanged += (s, e) => callCount++;

            // Act
            var result = viewModel.SetIntValue(42);

            // Assert
            Assert.True(result);
            Assert.Equal(1, callCount);
            Assert.Equal(42, viewModel.IntValue);
        }

        [Fact]
        public void RaisePropertiesChanged_Raises_Multiple_Events()
        {
            // Arrange
            var viewModel = new TestViewModel();
            var changedProperties = new List<string>();
            viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName ?? "");

            // Act
            viewModel.RaiseMultipleProperties();

            // Assert
            Assert.Equal(2, changedProperties.Count);
            Assert.Contains("TestValue", changedProperties);
            Assert.Contains("IntValue", changedProperties);
        }
    }

    /// <summary>
    /// Test ViewModel implementation for testing ViewModelBase.
    /// </summary>
    private class TestViewModel : ViewModelBase
    {
        private string? _testValue;
        private int _intValue;

        public string? TestValue
        {
            get => _testValue;
            set => SetProperty(ref _testValue, value);
        }

        public int IntValue
        {
            get => _intValue;
            set => SetProperty(ref _intValue, value);
        }

        public bool SetTestValue(string value) => SetProperty(ref _testValue, value);

        public bool SetIntValue(int value) => SetProperty(ref _intValue, value);

        public void RaiseMultipleProperties()
        {
            RaisePropertiesChanged(nameof(TestValue), nameof(IntValue));
        }
    }
}
