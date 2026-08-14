# Design Decisions

프로젝트를 실제로 사용하면서 반복적으로 발생한 문제를 기준으로 구조를 변경한 주요 결정들을 기록합니다.

## 1. Appium Server의 소유권을 구분한다

### 문제
사용자가 별도 Terminal에서 실행한 Appium과 Application이 실행한 Appium을 구분하지 않으면 종료 버튼이 다른 작업의 Server까지 종료할 수 있습니다.

### 결정
`AppiumServerManager`가 Application이 시작한 process handle을 보관하고, **owned process만 종료**합니다. 외부 Server는 health check로 감지만 합니다.

### 결과
- 중복 Server 실행 감소
- 외부 개발 환경 보호
- Bot 실행 전 상태 확인 가능

---

## 2. ADB command는 선택된 Device에 명시적으로 routing한다

### 문제
Device가 두 대 이상 연결된 환경에서 일반 `adb shell ...` 호출은 대상이 모호해집니다.

### 결정
Device 선택 후 대부분의 command를 `adb -s <serial> ...` 형태로 구성합니다. `devices`, `connect`, `kill-server` 같은 global command는 예외 처리합니다.

### 결과
Multi-device 환경에서도 command 대상이 명확해졌습니다.

---

## 3. Test History는 임시 파일 검증 후 교체한다

### 문제
테스트 종료 시점이나 저장 도중 Application이 비정상 종료되면 JSON 이력이 손상될 수 있습니다.

### 결정

```text
Temporary write
  → Flush to disk
  → JSON validation
  → Backup
  → Atomic replace
```

`File.Replace`를 사용할 수 없는 환경에서는 backup + move 방식으로 fallback합니다.

### 결과
이전 정상 이력 복구 가능성을 높이고 partial write를 주 파일에 바로 반영하지 않습니다.

---

## 4. Visual Assertion 설정과 실행 Artifact를 분리한다

Baseline/threshold/mask는 Scenario 설정으로 유지하고, 실제 run screenshot과 결과 metadata는 실행 Artifact로 분리합니다.

이렇게 하면 동일한 Scenario 기준을 유지하면서 실행별 결과를 독립적으로 비교할 수 있습니다.

---

## 5. WinForms UI는 고정 픽셀 보정보다 reflow를 우선한다

### 문제
특정 해상도에서 Width/Height를 직접 늘리는 방식은 다른 DPI와 창 크기에서 다시 clipping을 만들었습니다.

### 결정
- DPI 기준 logical pixel helper
- `TableLayoutPanel` / `FlowLayoutPanel`
- `AutoSize`, `MinimumSize`, `AutoScroll`
- 좁은 폭에서 Sidebar compact mode
- 카드 column 수 변경 및 vertical reflow

### 결과
특정 화면 크기 하나를 맞추는 대신, 공간이 부족하면 UI 구조 자체가 재배치되도록 변경했습니다.
