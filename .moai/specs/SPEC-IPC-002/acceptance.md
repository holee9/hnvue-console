# SPEC-IPC-002: Acceptance Criteria

<!-- TAG: SPEC-IPC-002 -->

---

## 1. Quality Gate Criteria

| Criteria | Target | Measurement |
|---|---|---|
| Test Coverage | >= 85% | DoseServiceAdapter, ImageServiceAdapter 개별 측정 |
| Build Status | Green | `dotnet build` 성공 |
| Existing Tests | All Pass | `dotnet test` 기존 614+ 테스트 전체 통과 |
| New Tests | All Pass | 모든 신규 테스트 통과 |
| Safety Tests | All Pass | Class C fail-safe 시나리오 전체 통과 |

---

## 2. Acceptance Scenarios

### Scenario 1: GetImageAsync - gRPC 이미지 조회

<!-- REQ: REQ-IMG-002 -->

```gherkin
Feature: ImageServiceAdapter - GetImageAsync

  Scenario: 정상적인 이미지 조회
    Given ImageService gRPC 서버가 정상 동작 중이다
    And image_id "IMG-001"에 대한 이미지 데이터가 존재한다
    When GetImageAsync("IMG-001")를 호출한다
    Then ImageData 객체가 반환된다
    And ImageData.Width > 0 이다
    And ImageData.Height > 0 이다
    And ImageData.PixelData는 비어있지 않다

  Scenario: 존재하지 않는 이미지 조회
    Given ImageService gRPC 서버가 정상 동작 중이다
    And image_id "IMG-NONE"에 대한 이미지가 존재하지 않는다
    When GetImageAsync("IMG-NONE")를 호출한다
    Then null이 반환된다
    And 빈 ImageData(width=0, height=0)는 반환되지 않는다

  Scenario: gRPC 통신 실패 시 이미지 조회
    Given ImageService gRPC 서버에 연결할 수 없다
    When GetImageAsync("IMG-001")를 호출한다
    Then 예외가 발생하거나 null이 반환된다
    And 빈 ImageData(기본값)는 반환되지 않는다
```

### Scenario 2: DoseDisplay 안전 Fail-Safe (Class C)

<!-- REQ: REQ-DOSE-002, REQ-DOSE-007 -->

```gherkin
Feature: DoseServiceAdapter - GetCurrentDoseDisplayAsync (Safety Critical)

  Scenario: 정상적인 dose summary 조회
    Given DoseService gRPC 서버가 정상 동작 중이다
    And 최근 30일 dose summary가 {totalDose: 150.5, examCount: 12}이다
    When GetCurrentDoseDisplayAsync()를 호출한다
    Then DoseDisplay.TotalDose == 150.5 이다
    And DoseDisplay.ExamCount == 12 이다

  Scenario: [SAFETY] gRPC 실패 시 0 반환 금지
    Given DoseService gRPC 서버에 연결할 수 없다
    When GetCurrentDoseDisplayAsync()를 호출한다
    Then DoseDisplay(0, 0)은 반환되지 않는다
    And 예외가 호출자에게 전파된다
    And 경고 로그가 기록된다

  Scenario: [SAFETY] gRPC timeout 시 0 반환 금지
    Given DoseService gRPC 서버가 5초 이상 응답하지 않는다
    When GetCurrentDoseDisplayAsync()를 호출한다
    Then DeadlineExceeded 예외가 발생한다
    And DoseDisplay(0, 0)은 반환되지 않는다
```

### Scenario 3: AlertThreshold 보수적 기본값 (Class C)

<!-- REQ: REQ-DOSE-003, REQ-DOSE-008 -->

```gherkin
Feature: DoseServiceAdapter - GetAlertThresholdAsync (Safety Critical)

  Scenario: 정상적인 threshold 조회
    Given ConfigService gRPC 서버가 정상 동작 중이다
    And dose.warning_threshold_mgy = 200 이다
    And dose.error_threshold_mgy = 500 이다
    When GetAlertThresholdAsync()를 호출한다
    Then DoseAlertThreshold.WarningThreshold == 200 이다
    And DoseAlertThreshold.ErrorThreshold == 500 이다

  Scenario: [SAFETY] ConfigService 실패 시 보수적 기본값
    Given ConfigService gRPC 서버에 연결할 수 없다
    When GetAlertThresholdAsync()를 호출한다
    Then DoseAlertThreshold.WarningThreshold == 50 이다 (보수적 기본값)
    And DoseAlertThreshold.ErrorThreshold == 100 이다 (보수적 기본값)
    And 예외는 발생하지 않는다
    And 경고 로그가 기록된다

  Scenario: [SAFETY] 설정값이 비정상적으로 높은 경우
    Given ConfigService에서 dose.warning_threshold_mgy = 99999 를 반환한다
    When GetAlertThresholdAsync()를 호출한다
    Then 반환된 값이 합리적 범위 내인지 검증한다
```

### Scenario 4: SubscribeDoseUpdates 스트리밍

<!-- REQ: REQ-DOSE-005 -->

```gherkin
Feature: DoseServiceAdapter - SubscribeDoseUpdatesAsync

  Scenario: 실시간 dose 업데이트 수신
    Given DoseService gRPC 서버가 SubscribeDoseAlerts 스트림을 제공한다
    And 서버가 3개의 DoseAlert 메시지를 순차적으로 전송한다
    When SubscribeDoseUpdatesAsync()를 호출한다
    Then IAsyncEnumerable<DoseUpdate>로 3개 항목을 수신한다
    And 각 DoseUpdate의 필드가 정확히 매핑된다

  Scenario: 스트리밍 중 연결 끊김
    Given DoseService gRPC 스트림이 활성 상태이다
    When 서버 연결이 끊어진다
    Then RpcException이 IAsyncEnumerable에서 발생한다
    And 리소스가 정상 해제된다

  Scenario: CancellationToken으로 스트림 종료
    Given DoseService gRPC 스트림이 활성 상태이다
    When CancellationToken이 취소된다
    Then 스트림 구독이 즉시 종료된다
    And 리소스가 정상 해제된다
```

### Scenario 5: gRPC Deadline Timeout

<!-- REQ: REQ-INFRA-001, REQ-INFRA-002, REQ-INFRA-003, REQ-INFRA-004 -->

```gherkin
Feature: GrpcAdapterBase - Deadline Policy

  Scenario: Command RPC에 5초 deadline 적용
    Given GrpcAdapterBase.CreateCallOptions(TimeSpan.FromSeconds(5))를 호출한다
    When 반환된 CallOptions를 검사한다
    Then Deadline이 현재 시각 + 5초 이내이다

  Scenario: Image streaming RPC에 30초 deadline 적용
    Given GrpcAdapterBase.CreateCallOptions(TimeSpan.FromSeconds(30))를 호출한다
    When 반환된 CallOptions를 검사한다
    Then Deadline이 현재 시각 + 30초 이내이다

  Scenario: Long-lived streaming에 deadline 없음
    Given GrpcAdapterBase.CreateStreamingCallOptions()를 호출한다
    When 반환된 CallOptions를 검사한다
    Then Deadline이 설정되지 않는다 (null 또는 MaxValue)

  Scenario: Deadline 초과 시 예외 발생
    Given gRPC 서버가 10초 이상 응답하지 않는다
    And 5초 deadline이 설정되어 있다
    When gRPC 호출을 수행한다
    Then RpcException(StatusCode.DeadlineExceeded)가 발생한다
    And 경고 로그가 기록된다

  Scenario: 기존 어댑터에 deadline 적용 후 회귀 없음
    Given PatientServiceAdapter, WorklistServiceAdapter에 deadline이 적용되었다
    When 기존 단위 테스트를 전체 실행한다
    Then 모든 기존 테스트가 통과한다
```

### Scenario 6: AEC Enable/Disable 감사 로그

<!-- REQ: REQ-AUDIT-001, REQ-AUDIT-003 -->

```gherkin
Feature: AECServiceAdapter - Audit Logging

  Scenario: AEC enable 시 감사 로그 기록
    Given AECServiceAdapter에 IAuditLogService가 주입되어 있다
    When AEC enable 작업을 수행한다
    Then IAuditLogService.LogAsync가 호출된다
    And 로그에 "AEC_ENABLE" 작업 유형이 포함된다
    And 로그에 타임스탬프가 포함된다

  Scenario: AEC disable 시 감사 로그 기록
    Given AECServiceAdapter에 IAuditLogService가 주입되어 있다
    When AEC disable 작업을 수행한다
    Then IAuditLogService.LogAsync가 호출된다
    And 로그에 "AEC_DISABLE" 작업 유형이 포함된다

  Scenario: 감사 로그 실패가 원래 작업을 중단하지 않음
    Given IAuditLogService.LogAsync가 예외를 발생시킨다
    When AEC enable 작업을 수행한다
    Then AEC enable 작업은 정상 완료된다
    And 감사 실패 경고 로그가 별도로 기록된다
```

### Scenario 7: Dose Threshold 변경 감사 로그

<!-- REQ: REQ-AUDIT-002, REQ-DOSE-004 -->

```gherkin
Feature: DoseServiceAdapter - SetAlertThreshold Audit

  Scenario: Dose threshold 변경 시 감사 로그 기록
    Given DoseServiceAdapter에 IAuditLogService가 주입되어 있다
    And 현재 warning threshold가 200 mGy이다
    When SetAlertThresholdAsync(warning: 300, error: 600)를 호출한다
    Then ConfigService.SetConfiguration이 호출된다
    And IAuditLogService.LogAsync가 호출된다
    And 로그에 변경 전 값(200)과 변경 후 값(300)이 포함된다
    And 로그에 타임스탬프가 포함된다

  Scenario: Dose threshold 변경 중 감사 로그 실패
    Given IAuditLogService.LogAsync가 예외를 발생시킨다
    When SetAlertThresholdAsync(warning: 300, error: 600)를 호출한다
    Then ConfigService.SetConfiguration은 정상 호출된다
    And threshold 변경은 정상 완료된다
    And 감사 실패 경고 로그가 별도로 기록된다

  Scenario: ResetCumulativeDose 시 감사 로그 기록
    Given DoseServiceAdapter에 IAuditLogService가 주입되어 있다
    When ResetCumulativeDoseAsync("STUDY-001")를 호출한다
    Then ResetStudyDose RPC가 호출된다
    And IAuditLogService.LogAsync가 호출된다
    And 로그에 study_id "STUDY-001"이 포함된다
```

---

## 3. Definition of Done

- [ ] Proto 파일 변경 후 `dotnet build` 성공
- [ ] DoseServiceAdapter 모든 메서드 구현 완료 (stub 제거)
- [ ] DoseServiceAdapter Class C 안전 fail-safe 테스트 전체 통과
- [ ] ImageServiceAdapter 모든 메서드 구현 완료 (stub 제거)
- [ ] ImageServiceAdapter gRPC 실패 시 빈 ImageData 반환하지 않음 검증
- [ ] GrpcAdapterBase에 deadline 메서드 추가
- [ ] 모든 어댑터(기존 4개 + 신규 2개)에 deadline 적용
- [ ] AECServiceAdapter에 IAuditLogService injection 완료
- [ ] DoseServiceAdapter의 SetAlertThreshold, ResetCumulativeDose에 감사 로그 추가
- [ ] 감사 로그 실패 격리 검증
- [ ] 기존 테스트 614+ 전체 통과 (회귀 없음)
- [ ] 신규 테스트 커버리지 >= 85%
