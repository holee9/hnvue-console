# SPEC-UI-003: Login / Auth UI + Role-Based Navigation

**Safety Class**: IEC 62304 Class B  
**Status**: 진행 중 (2/5 완료)  
**Source GAPs**: GAP-11-01, GAP-11-02, GAP-11-03, GAP-11-04, GAP-11-05  
**Created**: 2026-03-31  
**Last Updated**: 2026-03-31

---

## 1. 목적

인증되지 않은 사용자의 시스템 접근을 차단하고, 사용자 역할(Role)에 따라 UI 기능 접근을 제한한다.  
IEC 62304 Class B 의료기기 소프트웨어 보안 요구사항(GAP-11-01~05)을 충족한다.

---

## 2. 범위 및 GAP 현황

| GAP | 설명 | 우선순위 | 구현 상태 | 완료일 |
|-----|------|---------|---------|--------|
| GAP-11-01 | 로그인 화면 없음 — 인증 없이 앱 진입 가능 | Critical | ✅ **완료** | 2026-03-31 |
| GAP-11-04 | 역할 기반 메뉴 가시성 없음 — 모든 역할이 모든 메뉴 접근 | Critical | ✅ **완료** | 2026-03-31 |
| GAP-11-02 | 세션 타임아웃 경고 없음 — 5분 카운트다운 후 자동 로그아웃 미구현 | Medium | ⬜ 미구현 | — |
| GAP-11-03 | 비밀번호 변경 UI 없음 — 최초 로그인 강제 변경 미구현 | Medium | ⬜ 미구현 | — |
| GAP-11-05 | 감사로그 내보내기 없음 — CSV/PDF 내보내기 버튼 미구현 | Medium | ⬜ 미구현 | — |

**전체 진행률**: 2/5 완료 (40%)

---

## 3. 완료된 구현 (GAP-11-01, GAP-11-04)

### 3.1 GAP-11-01: 로그인 화면 (LoginWindow)

#### 요구사항 (EARS 형식)

- **REQ-UI003-01**: 시스템이 시작될 때, LoginWindow가 MainWindow보다 먼저 표시되어야 한다.
- **REQ-UI003-02**: 사용자가 올바른 자격증명을 입력하면, 시스템은 MainWindow를 열고 LoginWindow를 닫아야 한다.
- **REQ-UI003-03**: 사용자가 잘못된 비밀번호를 입력하면, 시스템은 오류 메시지를 표시하고 LoginWindow에 머물러야 한다.
- **REQ-UI003-04**: 인증 실패 5회 후, 계정이 잠기고 로그인 버튼이 비활성화되어야 한다.
- **REQ-UI003-05**: E2E 테스트 모드(HNVUE_E2E_TEST=1)에서는 Mock 서비스로 자동 로그인되어야 한다.
- **REQ-UI003-06**: E2E 로그인 테스트(HNVUE_E2E_SHOW_LOGIN=1)에서는 LoginWindow가 표시되어야 한다.

#### 구현된 컴포넌트

| 컴포넌트 | 파일 | 역할 |
|---------|------|------|
| LoginWindow | `src/HnVue.Console/Shell/LoginWindow.xaml(.cs)` | 로그인 UI, PasswordBox 브리지 |
| LoginViewModel | `src/HnVue.Console/ViewModels/LoginViewModel.cs` | 인증 로직, 잠금 상태 관리 |
| ISessionContext | `src/HnVue.Console/Services/ISessionContext.cs` | 앱 전역 세션 상태 인터페이스 |
| SessionContext | `src/HnVue.Console/Services/SessionContext.cs` | 싱글톤 세션 상태 구현 |

#### AutomationId (E2E 테스트용)

| 요소 | AutomationId |
|------|-------------|
| 창 | `LoginWindow` |
| 사용자명 입력 | `UsernameTextBox` |
| 비밀번호 입력 | `PasswordBox` |
| 로그인 버튼 | `LoginButton` |
| 오류 메시지 | `LoginErrorText` |
| 남은 시도 횟수 | `RemainingAttemptsText` |

#### 인수 기준

- [x] LoginWindow가 앱 시작 시 첫 번째 창으로 표시됨
- [x] 올바른 자격증명으로 MainWindow 전환됨
- [x] 잘못된 비밀번호 시 오류 메시지 표시됨
- [x] 5회 실패 후 계정 잠김 (RemainingAttempts=0 포함)
- [x] E2E 자동 로그인 (HNVUE_E2E_TEST=1)
- [x] E2E 로그인 테스트 모드 (HNVUE_E2E_SHOW_LOGIN=1)

#### 단위 테스트 (9개, 전체 통과)

| 테스트 | 검증 내용 |
|--------|---------|
| `ValidCredentials_SetsSessionAndFiresEvent` | 로그인 성공 시 ISessionContext 설정 + 이벤트 발행 |
| `ValidCredentials_SetsCorrectRole` | 세션에 올바른 역할이 설정됨 |
| `WrongPassword_ShowsError` | 잘못된 비밀번호 시 오류 메시지 |
| `UnknownUser_ShowsError` | 존재하지 않는 사용자 시 오류 메시지 |
| `FiveFailures_SetsIsLocked` | 5회 실패 후 IsLocked=true |
| `HasError_FalseInitially` | 초기 HasError=false |
| `HasError_TrueAfterError` | 오류 발생 후 HasError=true |
| `HasError_FalseAfterClear` | 오류 초기화 후 HasError=false |
| `LoginCommand_CanExecute_WhenNotBusy` | 입력 가능 상태 검증 |

#### E2E 테스트 (4개, 전체 통과)

| 테스트 | 검증 내용 |
|--------|---------|
| `LoginWindow_Is_Displayed_On_Startup` | 시작 시 LoginWindow 표시 |
| `LoginWindow_Has_Username_And_Password_Fields` | UI 요소 존재 확인 |
| `Login_With_Valid_Credentials_Opens_MainWindow` | 유효 로그인 → MainWindow 전환 |
| `Login_With_Wrong_Password_Shows_Error` | 오류 메시지 실증 확인 |

---

### 3.2 GAP-11-04: 역할 기반 메뉴 가시성

#### 요구사항 (EARS 형식)

- **REQ-UI003-07**: 현재 사용자 역할이 Administrator 또는 Service일 때, Config 메뉴 버튼이 표시되어야 한다.
- **REQ-UI003-08**: 현재 사용자 역할이 Administrator 또는 Service일 때, AuditLog 메뉴 버튼이 표시되어야 한다.
- **REQ-UI003-09**: 현재 사용자 역할이 Technologist, Viewer, Radiologist, Physicist, Operator일 때, Config 메뉴 버튼이 숨겨져야 한다.
- **REQ-UI003-10**: 현재 사용자 역할이 Technologist, Viewer, Radiologist, Physicist, Operator일 때, AuditLog 메뉴 버튼이 숨겨져야 한다.
- **REQ-UI003-11**: 모든 역할은 Patient, Worklist, Acquisition, ImageReview 메뉴에 접근할 수 있어야 한다.

#### 구현된 컴포넌트

| 컴포넌트 | 파일 | 역할 |
|---------|------|------|
| ShellViewModel.CanAccessAdminSections | `src/HnVue.Console/ViewModels/ShellViewModel.cs` | Admin/Service 역할 판별 bool |
| MainWindow.xaml | `src/HnVue.Console/Shell/MainWindow.xaml` | BoolToVisibilityConverter 바인딩 |

#### 역할별 메뉴 접근 매트릭스

| 역할 | Patient | Worklist | Acquisition | ImageReview | Config | AuditLog |
|------|---------|---------|------------|------------|--------|---------|
| Administrator | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Service | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Radiologist | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| Technologist | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| Physicist | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| Operator | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| Viewer | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |

#### 인수 기준

- [x] Administrator — Config 버튼 표시됨
- [x] Administrator — AuditLog 버튼 표시됨
- [x] Technologist — Config 버튼 숨겨짐
- [x] Technologist — AuditLog 버튼 숨겨짐
- [x] Viewer — Config 버튼 숨겨짐
- [x] Viewer — AuditLog 버튼 숨겨짐
- [x] Viewer — Patient/Worklist/Acquisition 버튼 표시됨

#### E2E 테스트 (7개, 전체 통과)

| 테스트 | 사용자 | 검증 내용 |
|--------|--------|---------|
| `Admin_Can_See_Config_Button` | System Administrator | Config 버튼 존재 확인 |
| `Admin_Can_See_AuditLog_Button` | System Administrator | AuditLog 버튼 존재 확인 |
| `Technologist_Cannot_See_Config_Button` | Technician Johnson | Config 버튼 null 확인 |
| `Technologist_Cannot_See_AuditLog_Button` | Technician Johnson | AuditLog 버튼 null 확인 |
| `Viewer_Cannot_See_Config_Button` | Viewer Jane | Config 버튼 null 확인 |
| `Viewer_Cannot_See_AuditLog_Button` | Viewer Jane | AuditLog 버튼 null 확인 |
| `Viewer_Can_See_Patient_And_Worklist_Buttons` | Viewer Jane | 공통 버튼 3개 존재 확인 |

---

## 4. 미구현 항목

### 4.1 GAP-11-02: 세션 타임아웃 경고

**목표**: 비활성 5분 후 카운트다운 다이얼로그 표시, 응답 없으면 자동 로그아웃

**설계 방향**:
- `ISessionContext`에 LastActivityTime 추적 추가
- `SessionTimeoutService`: 백그라운드 타이머, 5분 비활성 감지
- `SessionTimeoutDialog`: 카운트다운 UI (60초), "계속 사용" / "로그아웃" 버튼
- 로그아웃 시 `SessionContext.ClearSession()` + LoginWindow 재표시

**예상 컴포넌트**:
- `src/HnVue.Console/Services/SessionTimeoutService.cs`
- `src/HnVue.Console/Dialogs/SessionTimeoutDialog.xaml(.cs)`

---

### 4.2 GAP-11-03: 비밀번호 변경 UI

**목표**: 최초 로그인 시 강제 변경, 설정에서 수시 변경 가능

**설계 방향**:
- `IUserService.ChangePasswordAsync(username, oldPw, newPw)` 구현
- `ChangePasswordDialog`: 현재/새 비밀번호 입력, 확인 필드
- 최초 로그인 플래그: `User.RequiresPasswordChange` 속성
- LoginViewModel에 최초 로그인 감지 후 다이얼로그 강제 표시

**예상 컴포넌트**:
- `src/HnVue.Console/Dialogs/ChangePasswordDialog.xaml(.cs)`
- `src/HnVue.Console/ViewModels/ChangePasswordViewModel.cs`

---

### 4.3 GAP-11-05: 감사로그 내보내기

**목표**: AuditLogView에서 조회된 로그를 CSV/PDF로 내보내기

**설계 방향**:
- `AuditLogViewModel`에 `ExportCommand` 추가
- CSV: 표준 .NET StreamWriter
- PDF: iTextSharp 또는 QuestPDF 라이브러리
- 내보내기 완료 후 파일 저장 경로 알림 다이얼로그

**예상 컴포넌트**:
- `src/HnVue.Console/Services/AuditLogExportService.cs`
- AuditLogView.xaml에 내보내기 버튼 추가

---

## 5. 기술 아키텍처 결정

### 세션 상태 싱글톤 패턴

```
LoginViewModel → ISessionContext.SetSession()
ShellViewModel ← ISessionContext.CurrentRole
```

- `ISessionContext`는 DI 컨테이너에 `Singleton`으로 등록
- `LoginViewModel`이 인증 성공 시 세션을 설정
- `ShellViewModel`이 세션에서 역할을 읽어 메뉴 가시성 결정
- 로그아웃 시 `ClearSession()` 호출 → 역할이 `Unspecified`로 초기화

### E2E 환경 변수 전략

| 환경변수 | 값 | 효과 |
|---------|-----|------|
| `HNVUE_E2E_TEST` | `1` | MockUserService 주입, gRPC 비활성화 |
| `HNVUE_E2E_SHOW_LOGIN` | `1` | E2E 모드에서도 LoginWindow 표시 |

---

## 6. 의존성

- **SPEC-SECURITY-001**: `IUserService`, `UserSession`, `UserRole`, `MockUserService` (인증 인프라)
- **SPEC-UI-001**: `ShellViewModel`, `MainWindow.xaml` (기존 MVVM 구조)

---

## 7. 변경 이력

| 날짜 | 항목 | 상태 |
|------|------|------|
| 2026-03-31 | GAP-11-01 LoginWindow 구현 + 단위 테스트 9개 + E2E 4개 | ✅ 완료 |
| 2026-03-31 | GAP-11-04 역할 기반 메뉴 가시성 + E2E 7개 | ✅ 완료 |
| — | GAP-11-02 세션 타임아웃 경고 | ⬜ 미구현 |
| — | GAP-11-03 비밀번호 변경 UI | ⬜ 미구현 |
| — | GAP-11-05 감사로그 내보내기 | ⬜ 미구현 |
