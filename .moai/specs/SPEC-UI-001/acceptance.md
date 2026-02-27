# SPEC-UI-001: Acceptance Criteria

## Metadata

| Field    | Value                                   |
|----------|-----------------------------------------|
| SPEC ID  | SPEC-UI-001                             |
| Title    | HnVue GUI Console User Interface        |
| Format   | Given-When-Then (Gherkin-style)         |

---

## Definition of Done

A requirement is considered complete when:
1. All acceptance scenarios for that requirement pass in the automated test suite
2. The ViewModel under test has zero direct dependencies on `System.Windows.*` types (verified by `MvvmComplianceTests`)
3. The implementation is traceable to this SPEC in the traceability matrix
4. IEC 62304 Class B unit test documentation exists for the affected ViewModel
5. No PHI (patient name, patient ID, birth date) appears in log output at INFO level when the scenario executes

---

## AC-01: Patient Management (FR-UI-01)

### Scenario 1.1 — Patient Search Returns Matching Records Within 200 ms

```
Given the operator is on PatientView
  And IPatientService.SearchAsync returns 3 matching patient records
When the operator enters a search query and submits
Then PatientViewModel.Patients contains exactly 3 items
  And the update is reflected on the UI within 200 milliseconds
  And PatientViewModel.IsLoading transitions from true to false
```

### Scenario 1.2 — Manual Registration Dialog Opens With Required Fields

```
Given the operator is on PatientView
When the operator selects "Manual Registration"
Then PatientRegistrationDialog is displayed
  And the dialog contains fields for: patient ID, name, date of birth, sex, accession number
  And the Submit button is initially disabled until mandatory fields are filled
```

### Scenario 1.3 — Emergency Registration Bypasses Demographics

```
Given the operator is on PatientView
When the operator selects "Emergency Registration"
Then a temporary patient identifier is generated
  And the system navigates to WorklistView without requiring demographic completion
  And no validation error is raised for empty patient name or ID
```

### Scenario 1.4 — Valid Patient Registration Transmits to Core Engine

```
Given the operator has filled all mandatory patient registration fields
When the operator submits the PatientRegistrationDialog
Then IPatientService.RegisterAsync is called with the entered patient data
  And a success confirmation is displayed
  And the new patient appears in the PatientView patient list
```

### Scenario 1.5 — Mandatory Field Validation Blocks Empty Submission

```
Given the operator has opened PatientRegistrationDialog
  And the patient ID field is empty
When the operator attempts to submit the form
Then IPatientService.RegisterAsync is NOT called
  And the patient ID field is highlighted with an error indicator
  And the dialog remains open
```

### Scenario 1.6 — Edit Existing Patient Populates Dialog With Current Data

```
Given the operator has selected an existing patient record in PatientView
When the operator selects "Edit"
Then PatientEditDialog is displayed
  And all dialog fields are pre-populated with the selected patient's current data
  And the operator can modify any field and submit the change
```

---

## AC-02: Worklist Display (FR-UI-02)

### Scenario 2.1 — WorklistView Loads MWL on Activation

```
Given the operator navigates to WorklistView
When the view becomes active
Then WorklistViewModel requests the current MWL from IWorklistService
  And WorklistViewModel.WorklistItems is populated with the returned procedures
  And each item displays: patient name, patient ID, accession number, procedure step description, scheduled date/time
```

### Scenario 2.2 — Loading Indicator During MWL Fetch

```
Given the operator is on WorklistView
  And IWorklistService.GetWorklistAsync has not yet returned
While the MWL fetch is in progress
Then WorklistViewModel.IsLoading is true
  And the refresh command CanExecute is false
  And a loading indicator is visible in the View
```

### Scenario 2.3 — Procedure Selection Navigates to AcquisitionView

```
Given WorklistView displays at least one procedure item
When the operator selects a procedure item
Then WorklistViewModel.SelectedProcedure is set to the selected item
  And the shell navigates to AcquisitionView
  And AcquisitionView loads the selected procedure context
```

### Scenario 2.4 — Manual Refresh Re-fetches Worklist

```
Given WorklistView is displaying a previously loaded MWL
When the operator initiates a manual refresh
Then IWorklistService.GetWorklistAsync is called again
  And the displayed items are replaced with the fresh result
```

---

## AC-03: Image Viewer (FR-UI-03)

### Scenario 3.1 — 16-bit Grayscale Image Renders Without Downsampling

```
Given ImageReviewView receives a 16-bit grayscale image from IImageService
When the image is displayed on ImageViewerPanel
Then the WriteableBitmap pixel format is Gray16
  And no bit-depth reduction is applied prior to display
  And the full 16-bit dynamic range is preserved in the buffer
```

### Scenario 3.2 — Window/Level Adjustment Updates Display Within 200 ms

```
Given an image is displayed on ImageViewerPanel
When the operator adjusts the Window/Level control (window center or width)
Then the image display updates using the new windowing parameters
  And the update completes within 200 milliseconds of the operator interaction
```

### Scenario 3.3 — Zoom Scales Proportionally and Maintains Center

```
Given an image is displayed on ImageViewerPanel
When the operator applies a zoom gesture or zoom control
Then the image scales proportionally to the zoom factor
  And the center of the zoom gesture remains stationary in the viewport
```

### Scenario 3.4 — Pan Translates Image Within Viewport

```
Given an image is displayed on ImageViewerPanel with a zoom level above 100%
When the operator performs a pan gesture on the image
Then the image position translates in the direction of the pan gesture
  And the image does not shift outside its zoom boundaries
```

### Scenario 3.5 — Rotation Applies Correct Angular Transform

```
Given an image is displayed on ImageViewerPanel
When the operator selects "Rotate 90 CW"
Then the displayed image rotates 90 degrees clockwise
When the operator selects "Rotate 90 CCW"
Then the displayed image rotates 90 degrees counter-clockwise
When the operator selects "Rotate 180"
Then the displayed image rotates 180 degrees from the original orientation
```

### Scenario 3.6 — Flip Mirrors Image on Selected Axis

```
Given an image is displayed on ImageViewerPanel
When the operator selects "Flip Horizontal"
Then the displayed image is mirrored on the vertical axis
When the operator selects "Flip Vertical"
Then the displayed image is mirrored on the horizontal axis
```

### Scenario 3.7 — Reset View Restores Default State

```
Given the operator has applied Window/Level adjustments, zoom, pan, rotation, and flip
When the operator selects "Reset View"
Then ImageReviewViewModel resets: window center and width to defaults, zoom to 100%, pan to origin, rotation to 0°, flip to none
  And the image display updates to reflect the default state
```

---

## AC-04: Measurement Tools (FR-UI-04)

### Scenario 4.1 — Distance Measurement Calculates and Displays mm Value

```
Given the operator activates the Distance measurement tool on MeasurementToolPanel
  And pixel spacing metadata (mm per pixel) is available from the image
When the operator defines a line between two points on the image
Then the measured distance is calculated in millimeters using the pixel spacing
  And the distance value is displayed as an overlay at the measurement line
```

### Scenario 4.2 — Angle Measurement Calculates and Displays Degree Value

```
Given the operator activates the Angle measurement tool
When the operator defines three points forming an angle on the image
Then the measured angle is calculated in degrees
  And the angle value is displayed as an overlay at the vertex point
```

### Scenario 4.3 — Cobb Angle Measurement Calculates From Two Lines

```
Given the operator activates the Cobb Angle measurement tool
When the operator draws two lines on vertebral endplates
Then the Cobb angle is calculated as the angle between perpendiculars to each line
  And the Cobb angle value in degrees is displayed as an overlay
```

### Scenario 4.4 — Annotation Places Free-Text Overlay

```
Given the operator activates the Annotation tool
When the operator selects a position on the image and enters text
Then the annotation text is displayed at the selected image location as an overlay
```

### Scenario 4.5 — Selected Overlay Can Be Deleted

```
Given one or more measurement overlays are present on the image
When the operator selects an overlay
Then the overlay is highlighted
  And a delete action is available
When the operator deletes the overlay
Then the overlay is removed from the image and from MeasurementOverlayService
```

### Scenario 4.6 — Overlays Persist During Active Session

```
Given the operator has added distance, angle, and annotation overlays to an image
When the operator pans, zooms, or rotates the image
Then all overlays remain visible and correctly positioned relative to image coordinates
  And overlays are not lost when switching between images in the same session
```

---

## AC-05: Image Quality Control (FR-UI-05)

### Scenario 5.1 — Accept Transmits Acceptance Command With Confirmation

```
Given an image is displayed on ImageReviewView
When the operator selects "Accept" on QCActionPanel
Then ConfirmationDialog is displayed requesting explicit acknowledgment
When the operator confirms
Then IQCService.AcceptImageAsync is called with the current image reference
  And the QC status in ImageReviewViewModel reflects "Accepted"
```

### Scenario 5.2 — Reject Requires Reason Before Transmitting

```
Given an image is displayed on ImageReviewView
When the operator selects "Reject" on QCActionPanel
Then a rejection reason prompt is displayed
When the operator enters a reason and confirms
Then IQCService.RejectImageAsync is called with the image reference and the entered reason
  And the QC status reflects "Rejected"
```

### Scenario 5.3 — Reprocess Sends Request and Updates Image on Response

```
Given an image is displayed on ImageReviewView
When the operator selects "Reprocess"
Then IQCService.ReprocessImageAsync is called
  And a processing indicator is shown
  And when the reprocessed image is received, ImageReviewViewModel.CurrentImage is updated
```

### Scenario 5.4 — QC Buttons Disabled During In-Flight Operation

```
Given an image QC action (accept, reject, or reprocess) has been initiated
While the gRPC call is pending
Then all QC action commands report CanExecute as false
  And a processing indicator is visible on QCActionPanel
```

---

## AC-06: Protocol Selection (FR-UI-06)

### Scenario 6.1 — Protocol Panel Auto-Populates on Procedure Load

```
Given AcquisitionView loads a procedure context with a scheduled body part
When the AcquisitionView becomes active
Then ProtocolViewModel.BodyParts is populated with protocols applicable to the scheduled body part
  And the applicable body part is pre-selected
```

### Scenario 6.2 — Body Part Selection Filters Projection Presets

```
Given the operator is on AcquisitionView
When the operator selects a body part from the protocol list
Then ProtocolViewModel.Projections is updated to show only projections applicable to the selected body part
```

### Scenario 6.3 — Projection Selection Sends Protocol and Updates Parameters

```
Given the operator has selected a body part on ProtocolSelectionPanel
When the operator selects a projection preset
Then IProtocolService.SelectProtocolAsync is called with the selected protocol
  And ExposureParameterPanel updates with the preset kVp, mA, time, SID, and FSS values
```

### Scenario 6.4 — Protocol Labels Render in Selected Locale

```
Given the application locale is set to Korean (ko-KR)
When the operator views ProtocolSelectionPanel
Then body part names and projection labels are displayed in Korean
```

---

## AC-07: Exposure Parameter Display and Control (FR-UI-07)

### Scenario 7.1 — Required Parameters Are Displayed

```
Given AcquisitionView is active
Then ExposureParameterPanel displays all of: kVp, mA (or mAs), Exposure Time (ms), SID (cm), Focal Spot Size
```

### Scenario 7.2 — In-Range Parameter Change Is Transmitted

```
Given the operator is on ExposureParameterPanel
  And AEC mode is inactive
When the operator enters a kVp value within the allowable range received from Core Engine
Then IExposureService.SetParameterAsync is called with the new kVp value
  And no validation error is displayed
```

### Scenario 7.3 — Out-of-Range Value Is Rejected

```
Given the operator enters a kVp value outside the Core Engine's permitted range
When the operator attempts to confirm the entry
Then IExposureService.SetParameterAsync is NOT called
  And ExposureParameterViewModel displays the permissible range as an error hint
  And the previous valid value is restored in the input field
```

### Scenario 7.4 — AEC Mode Makes mA and Time Fields Read-Only

```
Given AEC mode is active
Then ExposureParameterViewModel.IsMaEditable is false
  And ExposureParameterViewModel.IsExposureTimeEditable is false
  And the mA and Exposure Time fields in the View appear as read-only with an "AEC" label
```

### Scenario 7.5 — Core Engine Stream Updates Parameters in Real Time

```
Given AcquisitionView is active
  And the Core Engine sends an updated parameter state via gRPC stream
When the stream frame arrives
Then ExposureParameterViewModel properties update to reflect the received values
  And the update is applied within 200 milliseconds of stream frame receipt
```

---

## AC-08: System Configuration (FR-UI-08)

### Scenario 8.1 — Configuration Loads on Navigation

```
Given the operator navigates to ConfigurationView
When the view becomes active
Then ISystemConfigService.GetConfigAsync is called
  And ConfigurationViewModel populates with current values in sections: Calibration, Network, Users, Logging
```

### Scenario 8.2 — Network Configuration Change Is Validated and Transmitted

```
Given the operator has navigated to the Network configuration section
When the operator modifies the DICOM AE title to a valid string and confirms
Then INetworkService.UpdateNetworkConfigAsync is called with the updated values
When the operator enters an invalid IP address format
Then the change is not transmitted
  And a format validation error is displayed
```

### Scenario 8.3 — User Management Actions Are Transmitted

```
Given an administrator is logged in and viewing the Users section
When the administrator creates a new user account with valid credentials
Then IUserService.CreateUserAsync is called with the new user data
  And the updated user list is displayed
When the administrator deactivates a user account
Then IUserService.DeactivateUserAsync is called
  And the deactivated account is reflected in the list
```

### Scenario 8.4 — Non-Admin Cannot Access Users or Calibration Sections

```
Given a non-administrator user is logged in
When the user navigates to ConfigurationView
Then the Users section tab is not visible in ConfigurationViewModel.ConfigSections
  And the Calibration section tab is not visible
  And an access-denied message is displayed if access is attempted via direct navigation
```

---

## AC-09: Real-time Acquisition Preview (FR-UI-09)

### Scenario 9.1 — Preview Stream Displays in Acquisition-Ready State

```
Given the system is in acquisition-ready state (Core Engine confirms readiness)
While acquisition-ready state is maintained
Then AcquisitionViewModel.IsAcquiring is false
  And the ImageViewerPanel in AcquisitionView displays the live preview frame stream
```

### Scenario 9.2 — Preview Frame Updates Within 200 ms

```
Given AcquisitionView is displaying a preview stream
When a new preview frame is received from the Core Engine via gRPC stream
Then the preview display updates with the new frame within 200 milliseconds
```

### Scenario 9.3 — Exposure Transitions View State and Shows Acquired Image

```
Given the system is in acquisition-ready state
When the operator initiates an exposure (with required safety confirmation)
Then AcquisitionViewModel.IsAcquiring transitions to true
  And a processing indicator is shown
  And when the acquired image is received, AcquisitionViewModel.PreviewImage is updated with the acquired result
```

### Scenario 9.4 — Stream Interruption Shows Connection-Lost Indicator

```
Given AcquisitionView is displaying a preview stream
When the Core Engine connection is lost or the preview stream errors
Then the stale preview frame is cleared from the display
  And AcquisitionViewModel.IsStreamConnected transitions to false
  And a connection-lost indicator is displayed in place of the preview
```

---

## AC-10: Dose Display (FR-UI-10)

### Scenario 10.1 — Current and Cumulative Dose Are Always Displayed

```
Given AcquisitionView is active
Then DoseDisplayPanel shows: current dose value with unit, cumulative dose for the current examination with unit
  And dose units match those specified by the Core Engine's dose data contract (e.g., mGy, mGy·cm²)
```

### Scenario 10.2 — Dose Update Reflected Within 200 ms

```
Given the Core Engine sends an updated dose value via gRPC notification
When the notification arrives
Then DoseViewModel.CurrentDose and DoseViewModel.CumulativeDose update to reflect the new values
  And the update is applied within 200 milliseconds
```

### Scenario 10.3 — Dose Alert Threshold Breach Displays Visual Alert

```
Given the Core Engine has provided a configurable dose alert threshold
When the current dose exceeds the alert threshold
Then DoseViewModel.IsDoseAlertActive transitions to true
  And a visual dose alert indicator (color change, icon, or banner) is displayed on DoseDisplayPanel
```

---

## AC-11: AEC Mode Toggle (FR-UI-11)

### Scenario 11.1 — AEC Activation Sends Command and Updates Exposure Panel

```
Given AEC mode is inactive on AECTogglePanel
When the operator activates AEC mode
Then IAECService.EnableAECAsync is called
  And ExposureParameterViewModel.IsMaEditable and IsExposureTimeEditable transition to false
  And AECTogglePanel displays a visual indicator confirming AEC is active
```

### Scenario 11.2 — AEC Deactivation Sends Command and Restores Editability

```
Given AEC mode is active
When the operator deactivates AEC mode
Then IAECService.DisableAECAsync is called
  And ExposureParameterViewModel.IsMaEditable and IsExposureTimeEditable transition to true
  And AECTogglePanel removes the AEC active indicator
```

### Scenario 11.3 — AEC Active State Is Clearly Indicated

```
Given AEC mode is active
Then AECTogglePanel displays a persistent visual indicator (label, icon, or color highlight) that is unambiguous to the operator
  And this indicator remains visible without any operator action to seek it
```

---

## AC-12: System Status Dashboard (FR-UI-12)

### Scenario 12.1 — All Required Components Are Displayed

```
Given the operator navigates to SystemStatusView
Then SystemStatusViewModel.ComponentStatuses includes entries for each of: X-ray Generator, Detector, Collimator, Network, DICOM Service, Core Engine
  And the status of each component is visible without scrolling at 1920 × 1080
```

### Scenario 12.2 — Status Update Reflected Within 200 ms

```
Given SystemStatusView is active
  And the Core Engine sends a component status update via gRPC stream
When the status update arrives
Then SystemStatusViewModel updates the affected component's status
  And the View reflects the change within 200 milliseconds
```

### Scenario 12.3 — Component Error Prevents Exposure Initiation

```
Given the X-ray Generator component reports an error state
Then SystemStatusViewModel.CanInitiateExposure is false
  And a persistent error indicator is displayed for the X-ray Generator on SystemStatusView
  And the exposure initiation command in AcquisitionViewModel reports CanExecute as false
```

### Scenario 12.4 — Status Badge Visible on NavigationBar From All Views

```
Given the system is operating
When the operator navigates to any primary view (Patient, Worklist, Acquisition, ImageReview, Config, AuditLog)
Then the condensed system status badge on the NavigationBar is visible
  And its color reflects the overall system health (green = all OK, amber = warning, red = error)
```

---

## AC-13: Audit Log Viewer (FR-UI-13)

### Scenario 13.1 — Audit Log Loads on Navigation

```
Given the operator navigates to AuditLogView
When the view becomes active
Then IAuditLogService.GetAuditLogsAsync is called (first page)
  And AuditLogViewModel.LogEntries is populated
  And each entry displays: timestamp, event type, user ID, event description
```

### Scenario 13.2 — Filter Request Fetches Filtered Results

```
Given the operator is on AuditLogView
When the operator sets a filter (date range: last 7 days, event type: Exposure)
Then IAuditLogService.GetAuditLogsAsync is called with the filter parameters
  And AuditLogViewModel.LogEntries is replaced with the filtered result set
```

---

## AC-14: MVVM Architecture Compliance (NFR-UI-01)

### Scenario 14.1 — ViewModel Assembly Contains Zero System.Windows References

```
Given the HnVue.Console assembly is built
When MvvmComplianceTests uses reflection to enumerate all types whose names end in "ViewModel"
Then no field, property, constructor parameter, or method parameter on any ViewModel type
  resolves to a type from the System.Windows namespace
  And no method return type on any ViewModel type is from System.Windows
```

### Scenario 14.2 — All ViewModel State Is Observable

```
Given any ViewModel in the assembly
Then the ViewModel implements INotifyPropertyChanged
  And all public state properties that affect View display raise PropertyChanged notifications when their value changes
```

### Scenario 14.3 — No Direct Method Calls from View Code-Behind to ViewModel

```
Given any View's code-behind (.xaml.cs) in the assembly
When the file is inspected
Then no direct property assignments or method calls on the DataContext (ViewModel) are present
  And all View-to-ViewModel interaction occurs exclusively through WPF data binding and command binding
```

---

## AC-15: Response Time (NFR-UI-02)

### Scenario 15.1 — UI Feedback Within 200 ms of Operator Interaction

```
Given the operator performs any interaction (button click, navigation, text entry)
Then visible UI feedback (button state change, navigation transition, input acknowledgment) is produced within 200 milliseconds
```

### Scenario 15.2 — UI Thread Not Blocked During gRPC Calls

```
Given a ViewModel issues a gRPC call via AsyncRelayCommand
While the gRPC call is in flight
Then the UI thread remains responsive to input events
  And the application does not freeze, stutter, or become unresponsive
```

---

## AC-16: 16-bit Grayscale Rendering (NFR-UI-03)

### Scenario 16.1 — WriteableBitmap Uses Gray16 Pixel Format

```
Given GrayscaleRenderer creates a WriteableBitmap for a received image
Then the WriteableBitmap's Format property equals PixelFormats.Gray16
  And no bit-depth conversion is applied between the source pixel data and the bitmap buffer
```

### Scenario 16.2 — W/L Transform Preserves DICOM GSDF Requirements

```
Given WindowLevelTransform is configured with window center and width values
When a 65536-entry LUT is computed
Then the mapping from input pixel value to display value follows the DICOM PS 3.14 GSDF formula
  And lossless 16-bit input values produce correct perceptual grayscale output values
```

---

## AC-17: Localization (NFR-UI-06)

### Scenario 17.1 — All User-Facing Strings Are in Resource Files

```
Given the HnVue.Console source code
When all View (.xaml) and ViewModel files are inspected
Then no hard-coded Korean or English user-facing string literals appear outside of .resx resource files
  And all UI text is referenced via resource keys
```

### Scenario 17.2 — Runtime Locale Switch Applies Without Restart

```
Given the application is running with Korean locale (ko-KR)
When the operator changes the display language to English in ConfigurationView
Then all user-facing string elements in the currently visible views update to English
  And no application restart is required
```

### Scenario 17.3 — Korean Hangul Characters Render Correctly

```
Given the application locale is Korean (ko-KR)
When all primary views are displayed
Then all Korean Hangul text renders without corruption, substitution characters, or truncation
  And minimum font size is 12pt (16px at 96 DPI)
```

---

## AC-18: IEC 62366-1 Safety Confirmations (REG-UI-01)

### Scenario 18.1 — Exposure Initiation Requires Explicit Confirmation

```
Given AcquisitionView is in acquisition-ready state
When the operator activates the exposure initiation control
Then ConfirmationDialog is displayed with unambiguous labeling indicating radiation will be emitted
  And the exposure gRPC command is NOT dispatched until the operator explicitly confirms
  And dismissing the dialog cancels the exposure without any radiation command being sent
```

### Scenario 18.2 — Dose Alert Is Displayed Prominently Without Operator Action

```
Given the dose exceeds the configured alert threshold during an examination
Then DoseViewModel.IsDoseAlertActive is true
  And the dose alert indicator on DoseDisplayPanel is visually prominent (color, icon, or size) without the operator scrolling or seeking it
```

### Scenario 18.3 — Exposure Controls Are Unambiguously Labeled

```
Given AcquisitionView is displayed
Then all controls that initiate radiation exposure are labeled with text or iconography that unambiguously communicates their radiation-initiating function
  And the labeling is consistent across all clinical workflow states
```

---

## Quality Gates

| Gate                           | Criterion                                                                      | Blocking |
|--------------------------------|--------------------------------------------------------------------------------|----------|
| ViewModel Unit Test Coverage   | >= 85% statement coverage for all ViewModel and Rendering types                | Yes      |
| MVVM Compliance                | Zero System.Windows references in ViewModel assembly (reflection test)         | Yes      |
| Response Time                  | All 200 ms interaction targets met under profiled test conditions               | Yes      |
| Localization Resource Coverage | Zero hard-coded user-facing strings in source; all strings in .resx            | Yes      |
| Safety Confirmation Coverage   | All safety-critical commands verified to require ConfirmationDialog            | Yes      |
| PHI Log Audit                  | Zero PHI in INFO-level log output across all scenario executions               | Yes      |
| IEC 62304 Traceability         | All FR, NFR, and REG requirements traceable to test cases in traceability matrix | Yes    |
| Layout Verification            | No scrollbar required at 1920 × 1080 at 100% DPI on all primary views         | Yes      |
