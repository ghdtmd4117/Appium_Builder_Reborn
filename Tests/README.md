# Tests

프로젝트의 핵심 로직과 UI 회귀 위험을 빠르게 확인하기 위한 테스트입니다.

## .NET / WinForms

```powershell
dotnet test Tests/AppiumBuilder.Tests.csproj
```

- Test History atomic 저장 / backup recovery
- Visual Assert config round-trip
- Log retention 보호 규칙
- ADB device parsing / serial routing
- 주요 화면 clipping/layout smoke test
- Logcat severity/filter parser

## Python dashboard

```powershell
python -m pytest Tests/python -q
```

- PASS / FAIL / SKIPPED 통계
- History backup fallback
- Step 결과 보존

Windows에서는 `run_all_tests.bat`으로 두 테스트 묶음을 연속 실행할 수 있습니다.

더 자세한 검증 범위는 [`docs/TESTING.md`](../docs/TESTING.md)를 참고하세요.
