using System.ComponentModel.DataAnnotations;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Patient registration dialog ViewModel.
/// SPEC-UI-001: FR-UI-01 Patient Management.
/// </summary>
public class PatientRegistrationViewModel : ViewModelBase
{
    private readonly IPatientService _patientService;
    private string _patientId = string.Empty;
    private string _patientName = string.Empty;
    private DateTime? _dateOfBirth;
    private int _selectedSexIndex = 0;
    private string _accessionNumber = string.Empty;
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Event raised when registration is completed.
    /// </summary>
    public event EventHandler? RegistrationCompleted;

    /// <summary>
    /// Initializes a new instance of <see cref="PatientRegistrationViewModel"/>.
    /// </summary>
    public PatientRegistrationViewModel(IPatientService patientService)
    {
        _patientService = patientService ?? throw new ArgumentNullException(nameof(patientService));

        RegisterCommand = new AsyncRelayCommand(
            ct => ExecuteRegisterAsync(ct),
            () => CanRegister(null));
        CancelCommand = new RelayCommand(() => ExecuteCancel(null));
    }

    /// <summary>
    /// Gets or sets the patient ID.
    /// </summary>
    public string PatientId
    {
        get => _patientId;
        set
        {
            if (SetProperty(ref _patientId, value))
            {
                RegisterCommand.RaiseCanExecuteChanged();
                ClearError();
            }
        }
    }

    /// <summary>
    /// Gets or sets the patient name.
    /// </summary>
    public string PatientName
    {
        get => _patientName;
        set
        {
            if (SetProperty(ref _patientName, value))
            {
                RegisterCommand.RaiseCanExecuteChanged();
                ClearError();
            }
        }
    }

    /// <summary>
    /// Gets or sets the date of birth.
    /// </summary>
    public DateTime? DateOfBirth
    {
        get => _dateOfBirth;
        set
        {
            if (SetProperty(ref _dateOfBirth, value))
            {
                RegisterCommand.RaiseCanExecuteChanged();
                ClearError();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected sex index.
    /// </summary>
    public int SelectedSexIndex
    {
        get => _selectedSexIndex;
        set => SetProperty(ref _selectedSexIndex, value);
    }

    /// <summary>
    /// Gets or sets the accession number.
    /// </summary>
    public string AccessionNumber
    {
        get => _accessionNumber;
        set => SetProperty(ref _accessionNumber, value);
    }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Gets a value indicating whether there is an error.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Gets the register command.
    /// </summary>
    public AsyncRelayCommand RegisterCommand { get; }

    /// <summary>
    /// Gets the cancel command.
    /// </summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>
    /// Determines whether registration can be executed.
    /// </summary>
    private bool CanRegister(object? parameter)
    {
        return !string.IsNullOrWhiteSpace(PatientId)
            && !string.IsNullOrWhiteSpace(PatientName)
            && DateOfBirth.HasValue;
    }

    /// <summary>
    /// Executes patient registration.
    /// </summary>
    private async Task ExecuteRegisterAsync(CancellationToken ct)
    {
        if (!ValidateInput())
            return;

        try
        {
            var registration = new PatientRegistration
            {
                PatientId = PatientId,
                PatientName = PatientName,
                DateOfBirth = DateOfBirth.HasValue ? DateOnly.FromDateTime(DateOfBirth.Value) : default,
                Sex = (Sex)SelectedSexIndex,
                AccessionNumber = string.IsNullOrWhiteSpace(AccessionNumber) ? null : AccessionNumber,
                IsEmergency = false
            };

            await _patientService.RegisterPatientAsync(registration, ct);

            // Success
            RegistrationCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Registration failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates input fields.
    /// </summary>
    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(PatientId))
        {
            ErrorMessage = "Patient ID is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(PatientName))
        {
            ErrorMessage = "Patient Name is required";
            return false;
        }

        if (!DateOfBirth.HasValue)
        {
            ErrorMessage = "Date of Birth is required";
            return false;
        }

        if (DateOfBirth.Value > DateTime.Today)
        {
            ErrorMessage = "Date of Birth cannot be in the future";
            return false;
        }

        if (DateOfBirth.Value < DateTime.Today.AddYears(-150))
        {
            ErrorMessage = "Date of Birth is invalid";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Clears the error message.
    /// </summary>
    private void ClearError()
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ErrorMessage = string.Empty;
        }
    }

    /// <summary>
    /// Executes cancel (closes dialog).
    /// </summary>
    private void ExecuteCancel(object? parameter)
    {
        RegistrationCompleted?.Invoke(this, EventArgs.Empty);
    }
}
