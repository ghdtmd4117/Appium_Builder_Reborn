# Testing

Appium Builder Reborn은 UI 자동화 프로그램 특성상 **순수 로직 테스트**와 **Windows/WinForms 환경 검증**을 분리합니다.

## .NET tests

```powershell
dotnet test Tests/AppiumBuilder.Tests.csproj
```

주요 검증 항목:

- ADB `devices -l` parsing 및 selected device routing
- Test History atomic 저장과 backup recovery
- Visual Assertion 설정 serialization
- Log retention 보호 규칙
- Logcat parser
- 주요 WinForms 화면 clipping/layout smoke test

## Python dashboard tests

```powershell
python -m pip install -r requirements.txt
python -m pytest Tests/python -q
```

주요 검증 항목:

- PASS / FAIL / SKIPPED 집계
- history backup fallback
- Step result 보존

## Windows 통합 실행

```bat
Tests\run_all_tests.bat
```

## 검증 범위

이 Repository에서 자동화할 수 있는 로직 테스트와 별개로 다음 항목은 실제 Windows Runtime에서 확인해야 합니다.

- WinForms rendering
- Windows DPI 100% / 125% / 150%
- 실제 Android Device USB 연결
- Appium CLI 설치 환경
- Screen Recording / Screenshot
- 실제 Application 대상 Scenario 실행

따라서 코드의 정적 검증만으로 UI가 모든 환경에서 동일하게 렌더링된다고 가정하지 않습니다.
