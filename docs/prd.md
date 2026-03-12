# PRD (Product Requirements Document)
# HnVue — 진단 의료용 X-ray Console Software

---

> **문서 버전**: 1.0
> **작성일**: 2026-03-12
> **프로젝트**: HnVue Console
> **문서 상태**: 초안
> **Safety Class**: IEC 62304 Class B (영상 표시, 측정) / Class C (Generator 제어, 노출 제어)

---

## 1. Product Overview

### 1.1 제품 정의

HnVue Console은 **하이브리드 아키텍처 기반 진단 의료용 X-ray Console Software**로, C++ Core Engine(실시간 성능)과 C# WPF GUI(생산성)를 gRPC IPC로 통합합니다. FPGA 기반 Flat Panel Detector와 X-ray Generator를 제어하며, 영상 획득/처리/판독/전송의 전체 임상 워크플로우를 지원합니다.

#### 제품 식별 정보

| 항목 | 값 |
|------|-----|
| **제품명** | Digital X-ray Imaging System |
| **모델명** | HnX-R1 |
| **모델 변종** | G1417CW/CWP/FW/FWP (G 시리즈/2/3G), A1717MCW, A1417MCW, F1417MCW |
| **Console Software** | HnVUE |
| **버전** | 1.0.2 (현재), 1.0.0.37 (Performance Test 기준) |
| **제조사** | H&abyz Co., Ltd. |
| **의료기기 등급** | IEC 62304 Class B (영상 표시, 측정) / Class C (Generator 제어, 노출 제어) |

#### UI 화면 구성 (User Manual 기준)

| 화면 | 설명 |
|------|------|
| **Login** | 사용자 인증 (ID/PW) |
| **Worklist / Studylist** | HIS/RIS Worklist 동기화, 검사 목록 |
| **Acquisition** | 환자 정보, 촬영 파라미터, 프리뷰, Crop & Marker, Manipulation, Annotation, Tool |
| **Stitching** | Full Image, Frame Image, Edit Stitching Image (파노라마 촬영) |
| **Dicom Print** | 영상 DICOM 프린트 |
| **Report** | 촬영 리포트 작성/관리 |
| **Viewer** | 영상 뷰어 (Zoom, Pan, Window/Level, 측정 도구) |
| **Setting** | 시스템, DICOM, 네트워크, 로그, 백업 설정 |

### 1.2 제품 목표

| 목표 | 설명 | 성공 기준 |
|------|------|-----------|
| **환자 안전** | IEC 62304 Class C SAFETY-CRITICAL 설계 | 다중 인터록, Dose 관리, Audit Trail |
| **임상 효율** | 빠르고 직관적인 촬영 워크플로우 | 10-state FSM, One-Touch 촬영, <3초 프리뷰 |
| **표준 준수** | DICOM, IHE, 규제 인허가 준비 | 100% SCU, MFDS/FDA 제출 준비 |
| **품질 보증** | 85%+ 테스트 커버리지, TRUST 5 | 1,048개 테스트, 0 LSP errors |

### 1.3 타겟 하드웨어

| 하드웨어 | 인터페이스 | 지원 상태 |
|----------|-----------|-----------|
| **Flat Panel Detector** | USB 3.x / PCIe (DMA) | HAL 인터페이스 완료, 실제 드라이버 연동 필요 |
| **High Voltage Generator** | RS-232 / RS-485 / Ethernet | HAL 인터페이스 완료, 실제 드라이버 연동 필요 |
| **AEC (Automatic Exposure Control)** | 프로토콜 의존 | HAL 인터페이스 완료 |
| **Collimator** | 디지털 I/O / Serial | HAL 인터페이스 완료 |
| **X-ray Table** | 모터 제어 / Position Sensor | HAL 인터페이스 완료 |
| **Safety Interlocks** | 디지털/아날로그 입력 | 9개 인터록 체인 완료 |

### 1.4 타겟 OS

| OS | 지원 상태 | 비고 |
|----|-----------|------|
| **Windows 10/11 IoT Enterprise LTSC** | 1차 타겟 | WPF GUI 실행, C++ Core Engine 빌드 |
| **Ubuntu 20.04/22.04** | 개발 환경 | 비즈니스 로직 크로스 플랫폼, WPF 미실행 |

---

## 2. Functional Requirements

### 2.1 영상 획득 및 처리 (Image Acquisition & Processing)

> **SPEC 참조**: SPEC-IMAGING-001 (완료 100%)

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-IMG-01** | Raw 데이터 획득 | Detector로부터 16-bit Raw 픽셀 데이터 DMA 전송 | Class B | ✅ 완료 |
| **FR-IMG-02** | Offset 보정 | Dark Frame 감산, 픽셀별 Offset 보정 | Class B | ✅ 완료 |
| **FR-IMG-03** | Gain 보정 | Gain Map 적용, 센서 불균형 보정 | Class B | ✅ 완료 |
| **FR-IMG-04** | Defect Pixel 보정 | Dead/Hot Pixel 보간, 실시간 맵 업데이트 | Class B | ✅ 완료 |
| **FR-IMG-05** | Scatter 보정 | Virtual Grid (GLI) 적용, 선택적 기능 | Class B | ✅ 완료 |
| **FR-IMG-06** | Noise Reduction | Gaussian/ Median 필터, 사용자 선택 | Class A | ✅ 완료 |
| **FR-IMG-07** | Flatten/Edge Mask | 이미지 외곽 마스킹 | Class A | ✅ 완료 |
| **FR-IMG-08** | Window/Level | 디스플레이용 LUT 적용, 실시간 조정 | Class B | 🟡 UI 연동 필요 |
| **FR-IMG-09** | 디스플레이 렌더러 | 16-bit 그레이스케일, GSDF/DICOM Part 14 | Class B | ⬜ 미구현 |

#### 이미지 파이프라인 데이터 플로우

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Image Acquisition Pipeline                       │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Detector ASIC ──USB 3.x/PCIe──▶ DMA Ring Buffer (C++)             │
│       │                                                             │
│       ▼                                                             │
│  [Offset Correction] ──Dark Frame 감산                             │
│       │                                                             │
│       ▼                                                             │
│  [Gain Correction] ──Gain Map 적용                                  │
│       │                                                             │
│       ▼                                                             │
│  [Defect Pixel Map] ──Dead/Hot Pixel 보간                          │
│       │                                                             │
│       ▼                                                             │
│  [Scatter Correction] ──Virtual Grid (선택적)                      │
│       │                                                             │
│       ▼                                                             │
│  [Window/Level] ──Display LUT 적용                                  │
│       │                                                             │
│       ▼                                                             │
│  [GUI Display] ──16-bit 그레이스케일 렌더링                        │
│       │                                                             │
│       ▼                                                             │
│  [DICOM Export] ──Patient Info + Image → PACS (C-STORE)           │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 Generator 제어 (X-ray Generator Control)

> **SPEC 참조**: SPEC-HAL-001 (완료 100%)

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-GEN-01** | kVp 제어 | Tube 전압 설정 (40-150 kVp, 0.1 kVp 단위) | Class C | ✅ HAL 인터페이스 완료 |
| **FR-GEN-02** | mA 제어 | Tube 전류 설정 (10-640 mA) | Class C | ✅ HAL 인터페이스 완료 |
| **FR-GEN-03** | 노출 시간 제어 | Exposure time 설정 (1-6300 ms) | Class C | ✅ HAL 인터페이스 완료 |
| **FR-GEN-04** | 촬영 트리거 | X-ray ON/OFF 제어, 하드웨어 인터록 체크 | Class C | ✅ FSM에서 구현 완료 |
| **FR-GEN-05** | 상태 모니터링 | Generator 상태 실시간 표시 (Ready/Warmup/Fault) | Class B | ✅ HAL 인터페이스 완료 |
| **FR-GEN-06** | AEC 모드 | 자동 노출 제어 모드 설정/해제 | Class C | ✅ HAL 인터페이스 완료 |
| **FR-GEN-07** | 파라미터 검증 | 안전 범위 초과 시 노출 거부 | Class C | ✅ DeviceSafetyLimits 완료 |
| **FR-GEN-08** | 프로토콜 관리 | Body Part/Projection별 Preset | Class B | ✅ ProtocolRepository 완료 |

### 2.3 DICOM 통신 (DICOM Communication Services)

> **SPEC 참조**: SPEC-DICOM-001 (완료 100%, 256 테스트)

| FR-ID | 요구사항 | 설명 | SOP Class | 구현 상태 |
|-------|----------|------|-----------|-----------|
| **FR-DICOM-01** | Storage SCU | 촬영 영상을 PACS로 전송 (C-STORE) | DX/CR Image Storage | ✅ 완료 |
| **FR-DICOM-02** | Worklist SCU | HIS/RIS에서 환자/검사 정보 조회 (C-FIND) | Modality Worklist | ✅ 완료 |
| **FR-DICOM-03** | MPPS SCU | 검사 수행 상태 보고 (N-CREATE/N-SET) | MPPS | ✅ 완료 |
| **FR-DICOM-04** | Storage Commitment | PACS 저장 확인 (N-ACTION) | Storage Commitment | ✅ 완료 |
| **FR-DICOM-05** | Query/Retrieve SCU | 이전 영상 조회/이동 (C-FIND/C-MOVE) | Q/R | ✅ 완료 |
| **FR-DICOM-06** | Dose SR | 방사선량 구조화 리포트 생성/전송 | X-Ray Dose SR | ✅ 완료 (TID 10001/10003) |
| **FR-DICOM-07** | GSPS | Window/Level 표시 상태 저장 | GSPS | 🟡 계획 |
| **FR-DICOM-08** | TLS 지원 | 보안 DICOM 통신 | - | ✅ DicomTlsFactory 완료 |
| **FR-DICOM-09** | 연결 풀링 | PACS 연결 재사용, 성능 최적화 | - | ✅ AssociationPool 완료 |
| **FR-DICOM-10** | 전송 큐 | 네트워크 단절 시 영구 큐잉 | - | ✅ TransmissionQueue 완료 |

#### DICOM IOD (Information Object Definition) 지원

| IOD | SOP Class UID | Transfer Syntax |
|-----|---------------|-----------------|
| Digital X-Ray Image | 1.2.840.10008.5.1.4.1.1.1.1 | Implicit/Explicit VR, JPEG 2000 |
| Computed Radiography | 1.2.840.10008.5.1.4.1.1.1.1.2 | Implicit/Explicit VR |
| X-Ray Dose SR | 1.2.840.10008.5.1.4.1.1.88.22 | Implicit VR |
| Basic Text SR | 1.2.840.10008.5.1.4.1.1.88.11 | Implicit VR |

### 2.4 Workflow 엔진 (Clinical Workflow Engine)

> **SPEC 참조**: SPEC-WORKFLOW-001 (95% 완료, 351 테스트)

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-WF-01** | State Machine | 10-state FSM 제어 | Class C | ✅ 완료 |
| **FR-WF-02** | 상태 전환 가드 | 안전 조건 검증 후 전환 | Class C | ✅ TransitionGuardMatrix 완료 |
| **FR-WF-03** | Worklist 동기화 | HIS/RIS에서 검사 목록 가져오기 | Class B | ✅ WorklistSyncHandler 완료 |
| **FR-WF-04** | 환자 선택 | Worklist에서 환자/검사 선택 | Class B | ✅ PatientSelectHandler 완료 |
| **FR-WF-05** | 프로토콜 선택 | Body Part/Projection별 촬영 프로토콜 선택 | Class B | ✅ ProtocolSelectHandler 완료 |
| **FR-WF-06** | 프리뷰 모드 | 실시간 Detector 스트리밍, 포지셔닝 지원 | Class B | ✅ PreviewHandler 완료 |
| **FR-WF-07** | 촬영 트리거 | X-ray 노출 제어, 인터록 체크 | Class C | ✅ ExposureTriggerHandler 완료 |
| **FR-WF-08** | QC 리뷰 | 촬영 영상 품질 확인, Reject/Accept | Class B | ✅ QcReviewHandler 완료 |
| **FR-WF-09** | MPPS 완료 | 검사 상태 PACS로 보고 | Class B | ✅ MppsCompleteHandler 완료 |
| **FR-WF-10** | PACS 전송 | 영상 PACS로 저장 (C-STORE) | Class B | ✅ PacsExportHandler 완료 |
| **FR-WF-11** | Reject/재촬영 | 불량 영상 Reject, 재촬영 워크플로우 | Class B | ✅ RejectRetakeHandler 완료 |
| **FR-WF-12** | 충돌 복구 | 크래시 시 상태 복구 (Crash Recovery) | Class C | ✅ CrashRecoveryService 완료 |
| **FR-WF-13** | 저널링 | SQLite WAL 패턴 영구 저널 | Class C | ✅ SqliteWorkflowJournal 완료 |
| **FR-WF-14** | 인터록 체크 | 9개 하드웨어 인터록 체인 | Class C | ✅ InterlockChecker 완료 |

#### Workflow State Machine

```
┌─────────────────────────────────────────────────────────────────────┐
│                     Workflow State Machine                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌───────┐    ┌───────────┐    ┌────────────┐    ┌───────────┐   │
│  │ Idle  │───▶│Worklist   │───▶│ Patient    │───▶│ Protocol  │   │
│  └───────┘    │ Sync      │    │ Select     │    │ Select    │   │
│     ▲        └───────────┘    └────────────┘    └───────────┘   │
│     │                                                             │
│     │                    ┌───────────┐                           │
│     └────────────────────│ Reject/   │◀───────────────────┐      │
│  (Retake/Completion)     │ Retake    │                    │      │
│                          └───────────┘                    │      │
│                                                             │      │
│  ┌─────────┐    ┌──────────┐    ┌───────────┐    ┌───────▼──────┐│
│  │Preview  │───▶│ Exposure │───▶│ QC Review │───▶│ MPPS        ││
│  └─────────┘    │ Trigger  │    │ (Accept)  │    │ Complete    ││
│                 └──────────┘    └───────────┘    └─────────────┘│
│                       │                                    │     │
│                   ┌───▼────┐                            ┌───▼───┐│
│                   │ Interlock                             │ PACS  ││
│                   │ Check      ◀─────────────────────────│ Export││
│                   │ (9-chain)                            └───────┘│
│                   └────────┐                                       │
│                            │                                       │
│                            ▼                                       │
│                      [SAFETY-CRITICAL]                              │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.5 방사선량 관리 (Radiation Dose Management)

> **SPEC 참조**: SPEC-DOSE-001 (완료 100%, 222 테스트)

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-DOSE-01** | DAP 계산 | Dose Area Product = K_air × A_field | Class B | ✅ DapCalculator 완료 |
| **FR-DOSE-02** | Air Kerma | K_air = k_factor × (kVp^n) × mAs / SID² × C_cal | Class B | ✅ DapCalculator 완료 |
| **FR-DOSE-03** | 조사야 면적 | Detector geometry 기반 계산 | Class B | ✅ DetectorGeometryProvider 완료 |
| **FR-DOSE-04** | 보정 관리 | Calibration 데이터 영구 저장 | Class B | ✅ CalibrationManager 완료 |
| **FR-DOSE-05** | 파라미터 수신 | Generator로부터 실제 노출 파라미터 수신 | Class C | ✅ ExposureParameterReceiver 완료 |
| **FR-DOSE-06** | 선량 기록 | DoseRecordRepository 영구 저장 (Atomic) | Class B | ✅ 완료 |
| **FR-DOSE-07** | Study 누적 | 단일 검사 내 선량 누적 | Class B | ✅ StudyDoseAccumulator 완료 |
| **FR-DOSE-08** | Audit Trail | SHA-256 해시체인, 블록체인 스타일 | Class B | ✅ AuditTrailWriter 완료 |
| **FR-DOSE-09** | RDSR 생성 | DICOM Dose SR (TID 10001/10003) | Class B | ✅ RdsrBuilder 완료 |
| **FR-DOSE-10** | DRL 비교 | Diagnostic Reference Level 초과 알림 | Class B | ✅ DrlComparer 완료 |
| **FR-DOSE-11** | 선량 표시 | 실시간 DAP/DLP 표시, 알람 | Class B | ✅ DoseDisplayNotifier 완료 |
| **FR-DOSE-12** | 보고서 생성 | Study/Period별 Dose Report | Class B | ✅ DoseReportGenerator 완료 |

### 2.6 UI 및 Viewer (User Interface & Image Viewer)

> **SPEC 참조**: SPEC-UI-001 (35% 완료)
> **User Manual 참조**: HnVUE Software User Manual v1.0.2

#### 2.6.1 UI 인프라

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-UI-01** | MVVM 아키텍처 | Model-View-ViewModel 패턴 | Class B | ✅ 완료 |
| **FR-UI-02** | DI 컨테이너 | Microsoft.Extensions.DI | Class A | ✅ 완료 |
| **FR-UI-03** | Localization | 다표어 지원 (.resx) | Class A | ✅ 인프라 완료 |
| **FR-UI-04** | Shell 구조 | MainWindow, Region 지원 | Class A | ✅ 완료 |
| **FR-UI-05** | 디자인 시스템 | Colors, Styles, Templates | Class A | ✅ 완료 |
| **FR-UI-15** | 다중 모니터 | 2 모니터 지원 (촬영 + 뷰어) | Class A | ⬜ 미구현 |

#### 2.6.2 Login 화면

> **console 기본 요건_20260310.xlsx** 참조

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-UI-LOGIN-01** | 사용자 인증 | ID/PW 로그인 | Class B | 🟡 계획 |
| **FR-UI-LOGIN-02** | 권한 확인 | Admin/Engineer/Operator 권한 분리 | Class B | 🟡 계획 |
| **FR-UI-LOGIN-03** | 세션 타임아웃 | 설정된 시간 동작 없으면 자동 로그아웃 | Class B | 🟡 계획 |
| **FR-UI-LOGIN-04** | 로그인 실패 처리 | Cyber security 적용 (예: 4회 실패 시 30분 로그인 금지) | Class B | 🟡 계획 |

#### 2.6.3 Worklist/Studylist 화면

> **UI 변경안**: PPT `★HnVUE UI 변경 최종안_251118.pptx` 참조
> **console 기본 요건**: `console 기본 요건_20260310.xlsx` 참조

> **UI 변경안**: PPT `★HnVUE UI 변경 최종안_251118.pptx` 참조

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-UI-WL-01** | Worklist/Studylist 탭 | 검사 목록 탭 전환 | Class B | 🟡 ViewModels 완료, XAML 필요 |
| **FR-UI-WL-02** | 검색 필터 | Accession No, Ref. Physician, Exam Date, PACS | Class B | 🟡 ViewModels 완료, XAML 필요 |
| **FR-UI-WL-03** | 날짜 필터 | Today, 3Days, 1Week, All, 1Month | Class B | 🟡 ViewModels 완료, XAML 필요 |
| **FR-UI-WL-04** | Detector Status | Detector 상태 표시 | Class B | 🟡 ViewModels 완료, XAML 필요 |
| **FR-UI-WL-05** | Before/After 리스트 | 촬영 전/후 목록 표시 | Class B | 🟡 ViewModels 완료, XAML 필요 |
| **FR-UI-WL-06** | 빠른 촬영 접근 | Worklist에서 바로 촬영 창 이동 | Class B | 🟡 계획 |
| **FR-UI-WL-07** | 서버 연결 상태 | PACS/Woklist 서버 연결 상태 표시 | Class B | 🟡 계획 |
| **FR-UI-WL-08** | Patient Register | 수동 환자 등록 | Class B | 🟡 계획 |

#### 2.6.4 Acquisition 화면

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-UI-ACQ-01** | 환자 정보 표시 | ID, 이름, 생년월일, 성별 | Class B | 🟡 ViewModels 완료, XAML 필요 |
| **FR-UI-ACQ-02** | 촬영 리스트 | Body Part, Projection, View 선택 | Class B | 🟡 ViewModels 완료, XAML 필요 |
| **FR-UI-ACQ-03** | 이미지 처리 | 실시간 이미지 처리 표시 | Class B | 🟡 ViewModels 완료, XAML 필요 |
| **FR-UI-ACQ-04** | Crop & Marker | 영상 자르기, 마커 표시 | Class B | ⬜ 미구현 |
| **FR-UI-ACQ-05** | Manipulation | Flip, Rotate, Invert 등 | Class B | ⬜ 미구현 |
| **FR-UI-ACQ-06** | Annotation | 화살표, 텍스트, 도형 주석 | Class B | ⬜ 미구현 |
| **FR-UI-ACQ-07** | Tool | 자동 밝기/대비, Zoom, Pan | Class B | ⬜ 미구현 |
| **FR-UI-ACQ-08** | Save/Send/Exit | 저장, 전송, 종료 | Class B | 🟡 계획 |
| **FR-UI-ACQ-09** | Bucky Selection | Bucky 타입 선택 (Grid/None) | Class B | 🟡 계획 |

#### 2.6.5 Stitching 화면 (파노라마)

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-UI-STITCH-01** | Full Image | 파노라마 합성 전체 영상 | Class B | ⬜ 미구현 |
| **FR-UI-STITCH-02** | Frame Image | 개별 프레임 영상 | Class B | ⬜ 미구현 |
| **FR-UI-STITCH-03** | Edit Stitching | 파노라마 영상 편집 | Class B | ⬜ 미구현 |

#### 2.6.6 Dicom Print 화면

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-UI-PRINT-01** | Viewer | 인쇄 preview | Class A | ⬜ 미구현 |
| **FR-UI-PRINT-02** | Image List | 인쇄할 영상 선택 | Class A | ⬜ 미구현 |
| **FR-UI-PRINT-03** | Print Tool | 인쇄 설정 (film size, layout) | Class A | ⬜ 미구현 |

#### 2.6.7 Report 화면

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-UI-RPT-01** | Viewer | 리포트 뷰어 | Class A | ⬜ 미구현 |
| **FR-UI-RPT-02** | Image List | 리포트에 포함할 영상 선택 | Class A | ⬜ 미구현 |
| **FR-UI-RPT-03** | Report Tool | 리포트 작성 도구 | Class A | ⬜ 미구현 |

#### 2.6.8 Viewer 화면

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-UI-VIEW-01** | 영상 뷰어 | 16-bit 그레이스케일, Pan/Zoom | Class B | ⬜ 미구현 |
| **FR-UI-VIEW-02** | Image List | 영상 목록 (Thumbnail) | Class B | ⬜ 미구현 |
| **FR-UI-VIEW-03** | Window/Level | 실시간 조정, Preset | Class B | ⬜ 미구현 |
| **FR-UI-VIEW-04** | 측정 도구 | 거리, 각도, ROI 밀도, Cobb angle | Class B | ⬜ 미구현 |

#### 2.6.9 Dose 대시보드

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-UI-DOSE-01** | Dose 표시 | 실시간 DAP/DLP 표시 | Class B | ✅ DoseDisplayViewModel 완료 |
| **FR-UI-DOSE-02** | DRL 알람 | 진단 기준 선량 초과 알람 | Class B | ✅ DoseDisplayViewModel 완료 |

### 2.7 시스템 설정 및 관리 (System Configuration & Management)

> **User Manual 참조**: Section 4.8 Setting

| FR-ID | 요구사항 | 설명 | Safety Class | 구현 상태 |
|-------|----------|------|--------------|-----------|
| **FR-SYS-01** | 사용자 관리 | 계정 생성/삭제, 권한 (Admin/Operator/Viewer) | Class B | 🟡 계획 |
| **FR-SYS-02** | 네트워크 설정 | DICOM AE Title, Port, IP | Class B | 🟡 ViewModel 완료 |
| **FR-SYS-03** | 캘리브레이션 | Offset/Gain/Defect Pixel 보정 관리 | Class C | 🟡 ViewModel 완료 |
| **FR-SYS-04** | 프로토콜 관리 | Body Part/Projection별 Preset CRUD | Class B | ✅ ProtocolRepository 완료 |
| **FR-SYS-05** | AEC 설정 | AEC 모드, 속도, 타겟 | Class C | 🟡 ViewModel 완료 |
| **FR-SYS-06** | 로그 관리 | Audit Trail, Event Log, Export | Class B | ✅ AuditTrailWriter 완료 |
| **FR-SYS-07** | 백업/복구 | 설정, 저널, Dose 데이터 백업 | Class B | 🟡 계획 |
| **FR-SYS-08** | 자동 업데이트 | OTA 업데이트 (안전 확인 후) | Class A | ⬜ 미구현 |
| **FR-SYS-09** | Date Format/Language | 날짜 형식, 언어 설정 | Class A | 🟡 ViewModel 완료 |
| **FR-SYS-10** | Session Time | 세션 타임아웃 설정 | Class B | 🟡 ViewModel 완료 |
| **FR-SYS-11** | Automatic Deletion | 자동 영상 삭제 설정 | Class B | 🟡 계획 |
| **FR-SYS-12** | Integrity | 데이터 무결성 체크 | Class B | ✅ AuditTrailWriter 완료 |
| **FR-SYS-13** | Detector 설정 | Detector 모델, 캘리브레이션 | Class C | 🟡 ViewModel 완료 |
| **FR-SYS-14** | Generator 설정 | HVG 파라미터, 제어 모드 | Class C | 🟡 ViewModel 완료 |
| **FR-SYS-15** | PACS 설정 | PACS 서버 연결, AE Title, Port | Class B | 🟡 ViewModel 완료 |
| **FR-SYS-16** | Worklist 설정 | MWL 서버 연결 | Class B | 🟡 ViewModel 완료 |
| **FR-SYS-17** | Print 설정 | Print Server, Print Setting | Class A | 🟡 ViewModel 완료 |
| **FR-SYS-18** | Option 설정 | 기타 옵션 | Class A | 🟡 계획 |
| **FR-SYS-19** | Display 설정 | 모니터 보정, GSDF | Class B | 🟡 계획 |
| **FR-SYS-20** | DicomSet 설정 | DICOM Transfer Syntax, Compression | Class B | 🟡 ViewModel 완료 |
| **FR-SYS-21** | RIS Code 설정 | RIS 코드 매핑 | Class B | 🟡 계획 |

---

## 3. Non-Functional Requirements

### 3.1 성능 요구사항 (Performance Requirements)

| NFR-ID | 요구사항 | 목표 | 측정 방법 |
|--------|----------|------|-----------|
| **NFR-PERF-01** | 촬영 후 프리뷰 | <3초 | End-to-end 타이머 |
| **NFR-PERF-02** | 영상 처리 파이프라인 | <1초 (2K x 2K) | 프로파일링 |
| **NFR-PERF-03** | DICOM C-STORE | <5초/영상 | 네트워크 로그 |
| **NFR-PERF-04** | UI 응답성 | <100ms | UI 렌더링 타이머 |
| **NFR-PERF-05** | DAP 계산 | <200ms | Dose 계산 타이머 |
| **NFR-PERF-06** | Workflow 전환 | <500ms | State 전환 로그 |
| **NFR-PERF-07** | IPC 지연시간 | <50ms | gRPC 통신 로그 |
| **NFR-PERF-08** | 메모리 사용량 | <2GB (GUI) | Profiler |
| **NFR-PERF-09** | 디스크 사용량 | <100GB/년 (영상 제외) | Storage 모니터링 |

### 3.2 보안 요구사항 (Security Requirements)

| NFR-ID | 요구사항 | 설명 | 기준 |
|--------|----------|------|------|
| **NFR-SEC-01** | DICOM TLS | 보안 DICOM 통신 | TLS 1.3+ |
| **NFR-SEC-02** | 감사 로그 | 사용자/시스템 동작 기록 | FDA 21 CFR Part 11 |
| **NFR-SEC-03** | 무결성 | SHA-256 해시체인 | AuditTrailWriter 구현 |
| **NFR-SEC-04** | 인증 | 사용자 로그인 (ID/PW) | 최소 8자, 특수문자 포함 |
| **NFR-SEC-05** | 권한 | 역할 기반 접근 제어 | Admin/Operator/Viewer |
| **NFR-SEC-06** | 데이터 암호화 | Dose/저널 데이터 암호화 | AES-256 |
| **NFR-SEC-07** | 소프트웨어 서명 | 실행파일 디지털 서명 | Code Signing Certificate |
| **NFR-SEC-08** | SOUP 관리 | 오픈소스 취약점 추적 | SOUP Register |

### 3.3 안정성 요구사항 (Reliability Requirements)

| NFR-ID | 요구사항 | 목표 | 완화 방안 |
|--------|----------|------|-----------|
| **NFR-REL-01** | 장비 가동률 | 99%+ (연간) | 예방 정비, 빠른 장애 복구 |
| **NFR-REL-02** | MTBF | >2000시간 | HA 아키텍처 |
| **NFR-REL-03** | MTTR | <4시간 | 원격 진단, 모듈 교체 |
| **NFR-REL-04** | 데이터 보존 | 영구 저장 (Atomic) | FSync, WAL 패턴 |
| **NFR-REL-05** | 충돌 복구 | 자동 복구 | Crash Recovery Service |
| **NFR-REL-06** | 네트워크 단절 | 재전송 큐 | TransmissionQueue |
| **NFR-REL-07** | 전원 장애 | 안전 정지 | UPS, Graceful Shutdown |
| **NFR-REL-08** | 중복 촬영 방지 | Study UID 체크 | DICOM 중복 검사 |

### 3.4 규제 준수 요구사항 (Regulatory Compliance Requirements)

| NFR-ID | 요구사항 | 규격 | 증거 |
|--------|----------|------|------|
| **NFR-REG-01** | IEC 62304 Class C | SW Life Cycle | @MX 태그, Traceability Matrix |
| **NFR-REG-02** | IEC 60601-2-54 | X-ray 성능/안전 | Generator 제어 테스트 |
| **NFR-REG-03** | DICOM Conformance | PS3.x | Conformance Statement |
| **NFR-REG-04** | IHE RAD TF | MWL, MPPS, Dose SR | Integration Tests |
| **NFR-REG-05** | SOUP Register | Clause 8.1.2 | `docs/soup-register.md` |
| **NFR-REG-06** | Risk Management | ISO 14971 | Risk Analysis Report |
| **NFR-REG-07** | Usability | IEC 62366-1 | Usability Test Report |
| **NFR-REG-08** | 사이버보안 | MFDS 가이드라인 | Cybersecurity Assessment |

### 3.5 유지보수성 요구사항 (Maintainability Requirements)

| NFR-ID | 요구사항 | 설명 | 목표 |
|--------|----------|------|------|
| **NFR-MAIN-01** | 코드 커버리지 | 단위 테스트 | 85%+ |
| **NFR-MAIN-02** | 코드 품질 | LSP errors/warnings | 0 errors, <10 warnings |
| **NFR-MAIN-03** | 문서화 | 코드 주석, @MX 태그 | 100% 공개 API |
| **NFR-MAIN-04** | 모듈화 | 낮은 결합도, 높은 응집도 | HAL 인터페이스 분리 |
| **NFR-MAIN-05** | 확장성 | 플러그인 HAL, 다중 벤더 | 인터페이스 기반 설계 |
| **NFR-MAIN-06** | 테스트 자동화 | CI/CD | Gitea Actions |
| **NFR-MAIN-07** | 디버깅 | 로그, 추적 | Diagnostic Mode |
| **NFR-MAIN-08** | 버전 관리 | 시맨틱 버전 | Conventional Commits |

---

## 4. Architecture Design

### 4.1 하이브리드 아키텍처 개요

```
┌─────────────────────────────────────────────────────────────────────┐
│                        HnVue Console Architecture                   │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    GUI Layer (C# / WPF)                     │   │
│  │  ┌───────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐  │   │
│  │  │ Views     │ │ViewModels│ │ Services │ │ DI Container  │  │   │
│  │  │ (XAML)    │ │ (MVVM)   │ │ (B.Logic)│ │ (Microsoft)   │  │   │
│  │  └───────────┘ └──────────┘ └──────────┘ └──────────────┘  │   │
│  └───────────────────────────┬─────────────────────────────────┘   │
│                              │ gRPC IPC (HTTP/2)                   │
│  ┌───────────────────────────▼─────────────────────────────────┐   │
│  │                   IPC Layer (C# + C++)                       │   │
│  │  ┌───────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐  │   │
│  │  │ gRPC      │ │ Proto    │ │ Channel  │ │ Serializer   │  │   │
│  │  │ Client    │ │ Buffers  │ │ Manager  │ │ (Protobuf)    │  │   │
│  │  └───────────┘ └──────────┘ └──────────┘ └──────────────┘  │   │
│  └───────────────────────────┬─────────────────────────────────┘   │
│                              │                                     │
│  ┌───────────────────────────▼─────────────────────────────────┐   │
│  │              Core Engine Layer (C++ / Native)                │   │
│  │  ┌───────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐  │   │
│  │  │ Workflow  │ │ Dose     │ │ Imaging  │ │ DICOM (DCMTK) │  │   │
│  │  │ FSM       │ │ Manager  │ │ Pipeline │ │ (fo-dicom)    │  │   │
│  │  └───────────┘ └──────────┘ └──────────┘ └──────────────┘  │   │
│  └───────────────────────────┬─────────────────────────────────┘   │
│                              │                                     │
│  ┌───────────────────────────▼─────────────────────────────────┐   │
│  │              Hardware Abstraction Layer (HAL)                │   │
│  │  ┌───────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐  │   │
│  │  │ Detector  │ │ Generator│ │ AEC      │ │ Interlock     │  │   │
│  │  │ Driver    │ │ Driver   │ │ Driver   │ │ Checker       │  │   │
│  │  └───────────┘ └──────────┘ └──────────┘ └──────────────┘  │   │
│  └───────────────────────────┬─────────────────────────────────┘   │
│                              │                                     │
│  ┌───────────────────────────▼─────────────────────────────────┐   │
│  │                   Hardware Layer                             │   │
│  │  ┌───────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐  │   │
│  │  │ FPGA      │ │ HVG      │ │ AEC      │ │ Table/        │  │   │
│  │  │ Detector  │ │ (HV Gen) │ │ HW       │ │ Collimator    │  │   │
│  │  └───────────┘ └──────────┘ └──────────┘ └──────────────┘  │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 4.2 계층별 상세 설계

#### 4.2.1 GUI Layer (C# / WPF)

| 모듈 | 설명 | 파일 위치 |
|------|------|-----------|
| **Views** | XAML 화면 정의 | `src/HnVue.Console/Views/**/*.xaml` |
| **ViewModels** | MVVM 패턴 ViewModel | `src/HnVue.Console/ViewModels/*.cs` |
| **Services** | 비즈니스 로직 서비스 | `src/HnVue.Console/Services/*.cs` |
| **Models** | 데이터 모델 | `src/HnVue.Console/Models/*.cs` |
| **Controls** | 사용자 정의 컨트롤 | `src/HnVue.Console/Controls/*.cs` |

#### 4.2.2 IPC Layer (gRPC)

| 채널 | Proto 파일 | 설명 |
|------|-----------|------|
| **Command** | `hnvue_command.proto` | Generator 제어, AEC 설정 |
| **Config** | `hnvue_config.proto` | 프로토콜, 캘리브레이션 |
| **Health** | `hnvue_health.proto` | 하드웨어 상태, 인터록 |
| **Image** | `hnvue_image.proto` | Raw 데이터, 처리된 영상 |
| **Ipc** | `hnvue_ipc.proto` | 공통 IPC 메시지 |

#### 4.2.3 Core Engine Layer (C++)

| 모듈 | 설명 | LOC |
|------|------|-----|
| **Workflow** | 10-state FSM, State Handlers | ~18,000 |
| **Dose** | DAP 계산, RDSR, DRL | 6,788 |
| **Imaging** | 이미지 파이프라인 | ~3,000 |
| **IPC Server** | gRPC 서버 | ~2,000 |
| **HAL** | 하드웨어 추상화 | ~4,000 |

#### 4.2.4 HAL (Hardware Abstraction Layer)

| 인터페이스 | 설명 | 구현 상태 |
|------------|------|-----------|
| **IDetector** | Detector 제어, DMA 전송 | ✅ Mock 완료 |
| **IGenerator** | HVG 제어 (kVp, mA, time) | ✅ Mock 완료 |
| **IAec** | AEC 파라미터 설정 | ✅ Mock 완료 |
| **ISafetyInterlock** | 9개 인터록 체인 | ✅ Mock 완료 |
| **ICollimator** | 빔 크기/형상 제어 | ✅ Mock 완료 |
| **ITable** | 모터/포지션 제어 | ✅ Mock 완료 |

### 4.3 데이터 플로우

#### 4.3.1 촬영 워크플로우 데이터 플로우

```
Patient Study (MWL)
        │
        ▼
┌───────────────┐     ┌───────────────┐     ┌───────────────┐
│ User Select   │────▶│ Protocol Load │────▶│ Prep Exposure │
│ Patient/Exam  │     │ (kVp/mAs/FSS) │     │ (Interlock)   │
└───────────────┘     └───────────────┘     └───────┬───────┘
                                                   │
                                                   ▼
┌───────────────┐     ┌───────────────┐     ┌───────────────┐
│ X-ray ON      │────▶│ Detector      │────▶│ Raw Image     │
│ (Trigger)     │     │ Acquisition   │     │ (16-bit)      │
└───────────────┘     └───────────────┘     └───────┬───────┘
                                                   │
                                                   ▼
┌───────────────┐     ┌───────────────┐     ┌───────────────┐
│ Image Process │────▶│ QC Review     │────▶│ Accept/Reject │
│ (Pipeline)    │     │ (User)        │     │               │
└───────────────┘     └───────────────┘     └───────┬───────┘
                                                   │
                                     ┌─────────────┴─────────────┐
                                     ▼                           ▼
                              ┌─────────────┐             ┌─────────────┐
                              │ MPPS/Export │             │ Retake      │
                              │ (DICOM)     │             │ (Discard)   │
                              └─────────────┘             └─────────────┘
```

---

### 4.4 Performance Test 검증 결과

> **출처**: `docs/ref/3. [HnVUE] Performance Test Report (A-PTR-HNV).docx`
> **테스트 기간**: 2025-07-14 ~ 2025-07-18
> **테스트 버전**: HnVUE 1.0.0.37
> **테스트 결과**: 11개 항목 모두 **PASS**

| 테스트 ID | 테스트 항목 | 테스트 기준 | 결과 | FR-ID 매핑 |
|----------|------------|-------------|------|------------|
| **PT-01** | 영상 획득/저장/전송 | 의료용 영상 획득/저장/전송 기능 확인 | ✅ PASS | FR-IMG-01~09, FR-DICOM-01 |
| **PT-02** | 환자 정보 저장/조회 | 촬영된 환자 신상정보, 촬영 오더 정보, 촬영 영상 저장/조회 | ✅ PASS | FR-WF-04, FR-SYS-06 |
| **PT-03** | 영상 조작 | 영상 확대/축소/회전/이동/밝기 조절 | ✅ PASS | FR-UI-08~09 |
| **PT-04** | 영상 레이아웃 | 영상 레이아웃 표시 선택, Thumbnail 영상 표시, 부분 확대 | ✅ PASS | FR-UI-14 |
| **PT-05** | DICOM 3.0 지원 | DICOM 3.0 규격 지원 영상 포맷 및 전송/저장 기능 | ✅ PASS | FR-DICOM-01~10 |
| **PT-06** | 사용자 인증 | 권한 부여 사용자 ID 로그인 | ✅ PASS | FR-SYS-01 |
| **PT-07** | 영상 삭제 권한 | 영상 삭제 권한 승인 | ✅ PASS | FR-SYS-01 |
| **PT-08** | PACS 오류 처리 | PACS 서버 오류 시 해결 방법 제공 | ✅ PASS | FR-DICOM-09~10 |
| **PT-09** | 오류 메시지 | 하드웨어/소프트웨어 오류 발생 시 안내 메시지 | ✅ PASS | NFR-REL-07 |
| **PT-10** | 환자 정보 확인 | 촬영된 영상의 환자정보 확인 | ✅ PASS | FR-UI-06~07 |
| **PT-11** | PACS 통신 | PACS와 통신 연결 | ✅ PASS | FR-DICOM-01~04 |

---

## 5. Technology Stack

### 5.1 개발 언어 및 프레임워크

| 계층 | 기술 | 버전 | 용도 |
|------|------|------|------|
| **GUI** | C# / .NET 8 | 8.0 | WPF 응용프로그램 |
| **GUI 프레임워크** | WPF | - | Windows Presentation Foundation |
| **MVVM** | Prism.Wpf | 9.x | MVVM 프레임워크 |
| **Core Engine** | C++ | C++20 | 성능 크리티컬 모듈 |
| **IPC** | gRPC | 1.68.x | 프로세스 간 통신 |
| **직렬화** | Protocol Buffers | 25.x | 데이터 직렬화 |
| **DICOM** | fo-dicom | 5.1.x | .NET DICOM 라이브러리 |

### 5.2 빌드 및 패키지 관리

| 도구 | 버전 | 용도 |
|------|------|------|
| **MSBuild** | 17.x (VS 2022) | C# 빌드 |
| **CMake** | 3.25+ | C++ 빌드 |
| **NuGet** | 6.x | C# 패키지 |
| **vcpkg** | 2024+ | C++ 패키지 |
| **Gitea Actions** | - | CI/CD |

### 5.3 테스트 도구

| 도구 | 용도 | 대상 |
|------|------|------|
| **xUnit** | 단위 테스트 | C# |
| **Google Test** | 단위 테스트 | C++ |
| **FlaUI** | UI E2E 테스트 | WPF |
| **Orthanc** | DICOM 테스트 서버 | Docker |
| **DVTK** | DICOM 적합성 검증 | Integration |

### 5.4 SOUP (Software of Unknown Provenance)

> **상세**: `docs/soup-register.md`

| 라이브러리 | 버전 | Risk Class | 용도 |
|-----------|------|------------|------|
| **gRPC** | 1.68.x | Low | IPC |
| **Protobuf** | 25.x | Low | 직렬화 |
| **spdlog** | 1.13.x | Low | 로깅 (C++) |
| **fmt** | 10.x | Low | 포맷 (C++) |
| **fo-dicom** | 5.1.x | Medium | DICOM (C#) |
| **Prism.Wpf** | 9.x | Low | MVVM (C#) |
| **Google Test** | 1.14.x | None | 테스트 (C++) |
| **xUnit** | 2.7.x | None | 테스트 (C#) |

---

## 6. Implementation Status

### 6.1 전체 진행 현황 (2026-03-12 기준)

```
┌─────────────────────────────────────────────────────────────────────┐
│                    HnVue Implementation Status                      │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  SPEC-INFRA-001    │███████████████████████████████████████│ 100% │
│  SPEC-IPC-001      │███████████████████████████████████████│ 100% │
│  SPEC-HAL-001      │███████████████████████████████████████│ 100% │
│  SPEC-IMAGING-001  │███████████████████████████████████████│ 100% │
│  SPEC-DICOM-001    │███████████████████████████████████████│ 100% │
│  SPEC-DOSE-001     │███████████████████████████████████████│ 100% │
│  SPEC-WORKFLOW-001 │████████████████████████████████████░░░░│  95% │
│  SPEC-UI-001       │████████████████░░░░░░░░░░░░░░░░░░░░░░│  35% │
│  SPEC-TEST-001     │████████████████░░░░░░░░░░░░░░░░░░░░░░│  35% │
│                                                                     │
│  OVERALL          │████████████████████████████████████░░░░│  85% │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 6.2 SPEC별 상세 현황

| SPEC | 총 항목 | 완료 | 부분 | 미구현 | 진행률 | 비고 |
|------|--------|------|------|--------|--------|------|
| **INFRA** | 16 | 16 | 0 | 0 | **100%** | 빌드/CI 완료 |
| **IPC** | 12 | 12 | 0 | 0 | **100%** | gRPC 완료 |
| **HAL** | 18 | 18 | 0 | 0 | **100%** | Mock 완료 |
| **IMAGING** | 9 | 9 | 0 | 0 | **100%** | 파이프라인 완료 |
| **DICOM** | 16 | 16 | 0 | 0 | **100%** | 256 테스트 |
| **DOSE** | 16 | 16 | 0 | 0 | **100%** | 222 테스트 |
| **WORKFLOW** | 27 | 26 | 1 | 0 | **95%** | 351 단위 테스트, 통합 7/20 |
| **UI** | 17 | 6 | 6 | 5 | **35%** | ViewModels 완료, XAML/렌더러 필요 |
| **TEST** | 16 | 5 | 1 | 10 | **35%** | Orthanc 완료, Python/CI/RTM 필요 |
| **합계** | **147** | **124** | **8** | **15** | **~85%** | - |

### 6.3 코드베이스 규모

| 영역 | 파일 수 | LOC | 비고 |
|------|--------|-----|------|
| **C++ Core** | ~65 | ~16,000+ | HAL, Imaging, IPC |
| **C# DICOM** | >30 | ~5,000 | 완료 |
| **C# DOSE** | 완비 | 6,788 | 완료 |
| **C# WORKFLOW** | 완비 | ~18,000+ | 95% 완료 |
| **C# Console/UI** | 진행 | 1,106 | 35% 진행 |
| **C# IPC Client** | 8 | ~1,584 | 완료 |
| **C++ Tests** | 15 | ~4,200 | 완료 |
| **C# Tests (DICOM)** | >25 | 8,408 | 완료 |
| **총계** | - | ~60,000+ | - |

### 6.4 테스트 커버리지

| 프로젝트 | 테스트 수 | 상태 |
|----------|-----------|------|
| **HnVue.Console.Tests** | 219 | ✅ Pass |
| **HnVue.Dose.Tests** | 222 | ✅ Pass |
| **HnVue.Workflow.Tests** | 351 | ✅ Pass (단위), 🟡 7/20 (통합) |
| **HnVue.Dicom.Tests** | 256 | ✅ Pass |
| **총계** | **1,048** | **~95% 통과** |

---

## 7. Roadmap

### 7.1 남은 작업 정의

#### 7.1.1 UI 완성 (SPEC-UI-001)

| 작업 | 우선순위 | 예상 시간 | 비고 |
|------|----------|----------|------|
| **XAML Views 작성** | P0 | 10-15시간 | 촬영, QC, Worklist, 설정 화면 |
| **16-bit 렌더러** | P0 | 8-12시간 | GSDF/DICOM Part 14 |
| **Window/Level 도구** | P1 | 4-6시간 | 실시간 조정 |
| **측정 도구** | P1 | 6-8시간 | 거리, 각도, ROI, Cobb angle |
| **다중 모니터** | P2 | 4-6시간 | 2 모니터 지원 |
| **FlaUI E2E 테스트** | P1 | 4-6시간 | UI 자동화 테스트 |

#### 7.1.2 WORKFLOW 통합 테스트 (SPEC-WORKFLOW-001)

| 작업 | 우선순위 | 예상 시간 | 비고 |
|------|----------|----------|------|
| **통합 테스트 작성** | P0 | 6-8시간 | 13개 시나리오 추가 |
| **DICOM 예외 테스트** | P0 | 3-4시간 | PACS 단절, 타임아웃 |
| **HW 예외 테스트** | P0 | 3-4시간 | Detector 실패, Generator 오류 |

#### 7.1.3 Windows 이관 (실제 하드웨어 연동)

| 작업 | 우선순위 | 예상 시간 | 비고 |
|------|----------|----------|------|
| **WPF GUI 검증** | P0 | 2-3시간 | XAML 디자이너, 실행 테스트 |
| **gRPC C++ Core 빌드** | P0 | 3-4시간 | Visual Studio 2022 |
| **HVG 드라이버 연동** | P0 | 4-6시간 | RS-232/485 시리얼 |
| **Detector 드라이버** | P0 | 6-8시간 | USB 3.x DMA |
| **Safety Interlock HW** | P0 | 4-6시간 | 디지털/아날로그 I/O |

#### 7.1.4 TEST 완성 (SPEC-TEST-001)

| 작업 | 우선순위 | 예상 시간 | 비고 |
|------|----------|----------|------|
| **Python 시뮬레이터** | P1 | 8-10시간 | Detector 에뮬레이션 |
| **CI 파이프라인** | P1 | 4-6시간 | Gitea Actions |
| **V&V 문서** | P0 | 16-20시간 | RTM, IEC 62304 증거 |
| **System Tests** | P1 | 6-8시간 | E2E 워크플로우 |

### 7.2 릴리스 로드맵

```
┌─────────────────────────────────────────────────────────────────────┐
│                      HnVue Release Roadmap                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  2026 Q1  ──────▶ 2026 Q2  ──────▶ 2026 Q3  ──────▶ 2026 Q4       │
│                                                                     │
│  ┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐         │
│  │ Alpha   │    │ Beta    │    │ MFDS    │    │ v1.0    │         │
│  │ Release │    │ Release │    │ 제출    │    │ Release │         │
│  └─────────┘    └─────────┘    └─────────┘    └─────────┘         │
│                                                                     │
│  • UI 완성    • HW 연동    • 인허가     • 정식 판매               │
│  • Windows   • 파일럿    • 서류       • OEM 파트너               │
│    이관       병원       작성                                  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 7.3 마일스톤 정의

| 마일스톤 | 날짜 | 기준 (Exit Criteria) |
|----------|------|---------------------|
| **M1: Alpha** | 2026-03-31 | UI 완성, Windows 실행 가능 |
| **M2: Beta** | 2026-06-30 | HW 연동, 파일럿 병원 배포 |
| **M3: MFDS 제출** | 2026-09-30 | 인허가 서류 완료, 제출 |
| **M4: v1.0 출시** | 2026-12-31 | MFDS 허가 획득, 상용 판매 |

---

## 8. Success Metrics

### 8.1 품질 KPI (Quality KPIs)

| 지표 | 목표 | 현재 | 측정 주기 |
|------|------|------|-----------|
| **테스트 커버리지** | 85%+ | ~85% | 분기별 |
| **단위 테스트 통과율** | 100% | 100% (1,048/1,048) | 주간 |
| **LSP Errors** | 0 | 0 | 주간 |
| **LSP Warnings** | <10 | 0 | 주간 |
| **MX 태그覆盖率** | 100% (공개 API) | 100% | 릴리스별 |
| **Critical Bugs** | 0 | 0 | 릴리스별 |
| **CI/CD 성공률** | 95%+ | 100% | 주간 |

### 8.2 성능 KPI (Performance KPIs)

| 지표 | 목표 | 측정 방법 |
|------|------|-----------|
| **촬영 후 프리뷰** | <3초 | End-to-end 타이머 |
| **영상 처리** | <1초 (2Kx2K) | 프로파일러 |
| **DICOM C-STORE** | <5초/영상 | 네트워크 로그 |
| **UI 응답성** | <100ms | UI 렌더링 타이머 |
| **IPC 지연** | <50ms | gRPC 로그 |

### 8.3 규제 KPI (Regulatory KPIs)

| 지표 | 목표 | 기한 |
|------|------|------|
| **MFDS 인허가** | Class II 허가 | 2026 Q4 |
| **DICOM Conformance** | 100% SCU | 2026 Q2 |
| **IEC 62304 문서** | SRS/SAD/SDS/V&V | 2026 Q3 |
| **SOUP 관리** | 100% 추적 | 상시 |
| **Risk Assessment** | ISO 14971 준수 | 2026 Q3 |

### 8.4 비즈니스 KPI (Business KPIs)

| 지표 | 2026 목표 | 2027 목표 |
|------|-----------|-----------|
| **판매 대수** | 10대 | 30대 |
| **매출** | $200K | $600K |
| **고객 만족도** | 4.0/5.0+ | 4.3/5.0+ |
| **장비 가동률** | 99%+ | 99.5%+ |

---

## 9. Appendix

### 9.1 용어 정의

| 용어 | 정의 |
|------|------|
| **FSM** | Finite State Machine (유한 상태 기계) |
| **HAL** | Hardware Abstraction Layer (하드웨어 추상화 계층) |
| **IPC** | Inter-Process Communication (프로세스 간 통신) |
| **AEC** | Automatic Exposure Control (자동 노출 제어) |
| **DAP** | Dose Area Product (선량-면적 곱) |
| **RDSR** | Radiation Dose Structured Report |
| **DRL** | Diagnostic Reference Level (진단 기준 선량) |
| **MWL** | Modality Worklist (모달리티 작업 목록) |
| **MPPS** | Modality Performed Procedure Step (모달리티 수행 절차 단계) |
| **GSDF** | Grayscale Standard Display Function (DICOM Part 14) |
| **MVVM** | Model-View-ViewModel 패턴 |
| **SOUP** | Software of Unknown Provenance (불확실 오픈소스) |
| **LSP** | Language Server Protocol |
| **MX 태그** | MoAI Context Annotation (@MX:NOTE, @MX:WARN, @MX:ANCHOR) |
| **TRUST 5** | Tested, Readable, Unified, Secured, Trackable 품질 프레임워크 |

### 9.2 참고 문서

| 문서 | 위치 |
|------|------|
| **MRD** | `docs/mrd.md` |
| **기술 리서치** | `docs/xray-console-sw-research.md` |
| **마스터 플랜** | `docs/antigravity-plan.md` |
| **SOUP Register** | `docs/soup-register.md` |
| **Windows 이관 보고서** | `docs/windows-tasks-report.md` |
| **SPEC 문서들** | `.moai/specs/SPEC-XXX/spec.md` |
| **코드 리뷰** | `docs/antigravity-review-completed-specs.md` |
| **코드 리뷰 Phase 1-2** | `docs/antigravity-review-phase1-2.md` |

### 9.3 변경 이력

| 버전 | 날짜 | 변경 사항 | 작성자 |
|------|------|-----------|--------|
| 1.0 | 2026-03-12 | 초안 작성 | MoAI |

---

**문서 기록**:

이 문서는 **MoAI Orchestrator**에 의해 작성되었습니다.
작성은 사용자의 대화 언어(`conversation_language: ko`)에 따라 수행되었습니다.

---

*Maintained by: abyz-lab <hnabyz2023@gmail.com>*