# Appium Builder Reborn · R9 Responsive UI

## 목표

R8.x까지 반복되던 고정 픽셀 기반 문구/버튼/카드 잘림을 개별 보정이 아니라 레이아웃 구조에서 줄이는 버전입니다.

## 핵심 변경

### 메인 창
- 기본 크기: 1600 × 960 유지
- 최소 크기: 1024 × 680
- 1280 논리 px 미만: 200px 사이드바 → 76px 아이콘 전용 사이드바
- DPI 변경 시 사이드바와 상태바 위치 재계산

### 홈
- 넓은 창: 3개 Device / 4개 KPI / 4개 Quick Action 한 줄
- Compact: KPI 2×2, Quick Action 2×2
- 더 좁은 폭: Device 1열, Quick Action 1열
- Compact 상태는 전체 페이지 Scroll을 사용해 카드 최소 높이를 보존

### 로그 / 미디어
- 넓은 창: 상태 4열, 액션 4열
- Compact: Device/Status/Action을 2×2로 자동 재배치
- Console 자체는 남은 폭을 사용하고, 세로 공간이 부족하면 페이지 Scroll을 사용

### 유틸리티
- 넓은 창: Media 2열 + System 2열
- Compact: 각 카드 1열 Stack
- 버튼과 설명 영역을 압축하지 않음

### Appium 봇
- 넓은 창: AI/Saved + Flow/Log + Builder/Run 다중 패널 구조 유지
- Compact: Flow와 Live Log를 세로 Stack, Builder와 Run Control도 세로 Stack
- 높이가 짧은 창: 기존 2열 구조를 유지하되 전체 페이지 Scroll로 최소 높이 보존
- 수동 빌더 입력 필드는 현재 폭에 따라 1/2/3개 입력 영역을 다시 계산

### 공통 컨트롤
- RoundedButton.GetPreferredSize 구현
- 실제 Font 기준으로 Text + Icon + Gap + Padding 폭 계산
- CreateModernButton은 AutoFitText를 끄고 실제 문구 크기를 MinimumSize에 반영
- 정적 Page Description은 AutoEllipsis 비활성화

## 지원 화면 크기 Smoke Test
- 1024 × 680
- 1152 × 720
- 1366 × 820
- 1600 × 900
- 1920 × 1080

> WinForms 실제 렌더링은 Windows .NET Desktop 환경에서 최종 확인해야 합니다. 이 소스 패키지에서는 C# 구조 검사, 프로젝트 XML, Dashboard 테스트/HTTP Smoke, JavaScript/Python 문법 검사를 수행합니다.
