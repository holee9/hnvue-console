using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Patient edit dialog ViewModel.
/// SPEC-UI-001: FR-UI-01 Patient Management.
/// </summary>
public class PatientEditViewModel : ViewModelBase
{
    private readonly IPatientService _patientService;
    private string _patientId = string.Empty;
    private string _patientName = string.Empty;
    private DateTime? _dateOfBirth;
    private int _selectedSexIndex;
    private string _accessionNumber = string.Empty;
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Event raised when edit is completed.
    /// </summary>
    public event EventHandler? EditCompleted;

    /// <summary>
    /// Initializes a new instance of <see cref="PatientEditViewModel"/>.
    /// </summary>
    public PatientEditViewModel(IPatientService patientService)
    {
        _patientService = patientService ?? throw new ArgumentNullException(nameof(patientService));

        SaveCommand = new AsyncRelayCommand(
            ct => ExecuteSaveAsync(ct),
            () => CanSave(null));
        CancelCommand = new RelayCommand(() => ExecuteCancel(null));
    }

    /// <summary>
    /// Gets or sets the patient ID (read-only).
    /// </summary>
    public string PatientId
    {
        get => _patientId;
        set => SetProperty(ref _patientId, value);
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
                SaveCommand.RaiseCanExecuteChanged();
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
                SaveCommand.RaiseCanExecuteChanged();
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
    /// Gets the save command.
    /// </summary>
    public AsyncRelayCommand SaveCommand { get; }

    /// <summary>
    /// Gets the cancel command.
    /// </summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>
    /// Loads a patient for editing.
    /// </summary>
    public void LoadPatient(Patient patient)
    {
        PatientId = patient.PatientId;
        PatientName = patient.PatientName;
        DateOfBirth = patient.DateOfBirth.ToDateTime(TimeOnly.MinValue);
        SelectedSexIndex = (int)patient.Sex;
        AccessionNumber = patient.AccessionNumber ?? string.Empty;
    }

    /// <summary>
    /// Determines whether save can be executed.
    /// </summary>
    private bool CanSave(object? parameter)
    {
        return !string.IsNullOrWhiteSpace(PatientName)
            && DateOfBirth.HasValue;
    }

    /// <summary>
    /// Executes patient update.
    /// </summary>
    private async Task ExecuteSaveAsync(CancellationToken ct)
    {
        if (!ValidateInput())
            return;

        try
        {
            var request = new PatientEditRequest
            {
                PatientId = PatientId,
                PatientName = PatientName,
                DateOfBirth = DateOfBirth.HasValue ? DateOnly.FromDateTime(DateOfBirth.Value) : null,
                Sex = (Sex)SelectedSexIndex,
                AccessionNumber = string.IsNullOrWhiteSpace(AccessionNumber) ? null : AccessionNumber
            };

            await _patientService.UpdatePatientAsync(request, ct);

            // Success
            EditCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Update failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates input fields.
    /// </summary>
    private bool ValidateInput()
    {
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
        EditCompleted?.Invoke(this, EventArgs.Empty);
    }
}
