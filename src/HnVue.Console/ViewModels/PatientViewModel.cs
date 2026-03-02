using System.Collections.ObjectModel;
using System.Diagnostics;
using HnVue.Console.Commands;
using HnVue.Console.Models;
using HnVue.Console.Services;

namespace HnVue.Console.ViewModels;

/// <summary>
/// Patient management ViewModel.
/// SPEC-UI-001: FR-UI-01 Patient Management.
/// </summary>
public class PatientViewModel : ViewModelBase
{
    /// <summary>
    /// Raised when navigation to another view is requested.
    /// The string argument is the view name (e.g., "Worklist").
    /// </summary>
    public event EventHandler<string>? NavigationRequested;

    /// <summary>
    /// Raised when an error occurs that should be displayed to the user.
    /// </summary>
    public event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// Raised when the patient registration dialog should be opened.
    /// </summary>
    public event EventHandler? RegistrationRequested;

    /// <summary>
    /// Raised when the patient edit dialog should be opened.
    /// The Patient argument is the patient to edit.
    /// </summary>
    public event EventHandler<Patient>? EditPatientRequested;
    private readonly IPatientService _patientService;
    private string _searchQuery = string.Empty;
    private Patient? _selectedPatient;
    private bool _isLoading;

    /// <summary>
    /// Initializes a new instance of <see cref="PatientViewModel"/>.
    /// </summary>
    public PatientViewModel(IPatientService patientService)
    {
        _patientService = patientService ?? throw new ArgumentNullException(nameof(patientService));
        Patients = new ObservableCollection<Patient>();

        SearchCommand = new AsyncRelayCommand(
            ct => ExecuteSearchAsync(ct),
            () => !string.IsNullOrWhiteSpace(SearchQuery) && !IsLoading);

        RegisterCommand = new RelayCommand(() => ExecuteRegister(null));
        EmergencyRegisterCommand = new RelayCommand(() => ExecuteEmergencyRegister(null));
        EditPatientCommand = new RelayCommand<object?>(
            p => ExecuteEditPatient(p),
            p => SelectedPatient != null && !IsLoading);

        SelectPatientCommand = new RelayCommand<object?>(
            p => ExecuteSelectPatient(p));
    }

    /// <summary>
    /// Gets the patients collection.
    /// </summary>
    public ObservableCollection<Patient> Patients { get; }

    /// <summary>
    /// Gets or sets the search query.
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected patient.
    /// </summary>
    public Patient? SelectedPatient
    {
        get => _selectedPatient;
        set => SetProperty(ref _selectedPatient, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a search is in progress.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                SearchCommand.RaiseCanExecuteChanged();
                EditPatientCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the search command.
    /// </summary>
    public AsyncRelayCommand SearchCommand { get; }

    /// <summary>
    /// Gets the register command.
    /// </summary>
    public RelayCommand RegisterCommand { get; }

    /// <summary>
    /// Gets the emergency register command.
    /// </summary>
    public RelayCommand EmergencyRegisterCommand { get; }

    /// <summary>
    /// Gets the edit patient command.
    /// </summary>
    public RelayCommand<object?> EditPatientCommand { get; }

    /// <summary>
    /// Gets the select patient command (navigates to worklist).
    /// </summary>
    public RelayCommand<object?> SelectPatientCommand { get; }

    /// <summary>
    /// Executes patient search.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Event handler")]
    private async Task ExecuteSearchAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;

        IsLoading = true;
        Patients.Clear();

        try
        {
            var request = new PatientSearchRequest
            {
                Query = SearchQuery,
                MaxResults = 50
            };

            var result = await _patientService.SearchPatientsAsync(request, ct);

            foreach (var patient in result.Patients)
            {
                Patients.Add(patient);
            }

            Debug.WriteLine($"Patient search completed: {result.TotalCount} results found");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Patient search failed: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Opens the patient registration dialog.
    /// </summary>
    private void ExecuteRegister(object? parameter)
    {
        Debug.WriteLine("Opening patient registration dialog");
        RegistrationRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Opens emergency patient registration (skips demographics).
    /// Creates a temporary patient and navigates to WorklistView.
    /// </summary>
    private void ExecuteEmergencyRegister(object? parameter)
    {
        Debug.WriteLine("Opening emergency patient registration");
        var emergencyPatient = new Patient
        {
            PatientId = $"EMRG-{DateTime.UtcNow:yyyyMMddHHmmss}",
            PatientName = "EMERGENCY",
            DateOfBirth = DateOnly.MinValue,
            Sex = Sex.Unknown
        };
        SelectedPatient = emergencyPatient;
        Patients.Add(emergencyPatient);
        NavigationRequested?.Invoke(this, "Worklist");
    }

    /// <summary>
    /// Opens the patient edit dialog.
    /// </summary>
    private void ExecuteEditPatient(object? parameter)
    {
        if (parameter is not Patient patient)
            return;

        Debug.WriteLine($"Editing patient: {patient.PatientId}");
        SelectedPatient = patient;
        EditPatientRequested?.Invoke(this, patient);
    }

    /// <summary>
    /// Selects a patient and navigates to worklist.
    /// </summary>
    private void ExecuteSelectPatient(object? parameter)
    {
        if (parameter is not Patient patient)
            return;

        SelectedPatient = patient;
        Debug.WriteLine($"Selected patient {patient.PatientName}, navigating to worklist");
        NavigationRequested?.Invoke(this, "Worklist");
    }
}
