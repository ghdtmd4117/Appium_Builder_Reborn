# Appium Builder Professional R8 Tests

R8은 앱 실행 전 회귀를 잡기 위해 .NET/WinForms와 Python dashboard 두 층의 테스트를 둡니다.

- `.NET / WinForms`: `dotnet test Tests\AppiumBuilder.Tests.csproj`
  - 테스트 이력 atomic 저장/백업 복구
  - Visual Assert 설정 round-trip
  - Log retention이 시나리오 원본을 삭제하지 않는지
  - ADB serial 주입
  - 주요 화면의 단일행 라벨 clipping smoke (1280×760 / 1440×880 / 1600×1000)
  - Logcat severity/filter parser
  - 오래된 Step run artifact 정리 + baseline 보호
- `Python dashboard`: `python -m pytest Tests\python -q`
  - SKIPPED 제외 통계
  - history backup fallback
  - Step 결과 보존

Windows에서는 `Tests\run_all_tests.bat`으로 둘을 연속 실행할 수 있습니다.
