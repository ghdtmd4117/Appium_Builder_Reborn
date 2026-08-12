# 포트폴리오용 핵심 소스

이 폴더는 Appium Builder Reborn의 구현 방식과 코드 구조를 빠르게 확인할 수 있도록 **대표 코드만 선별해 공개한 공간**입니다.

- `AppiumBuilder.slnx / AppiumBuilder.csproj` : Visual Studio / .NET 8 프로젝트 구성
- `Core/AppiumServerManager.cs` : Appium Server 상태 확인, 시작·종료, Process ownership 관리
- `Core/AdbEngine.cs` : ADB Device 탐지, Multi Device 및 명령 실행
- `Core/TestHistoryStore.cs` : Test History의 temp → validation → backup → atomic replace 저장
- `MainForm/MainFormResponsive.cs` : DPI-aware / Breakpoint 기반 Responsive WinForms Layout
- `Tests/LayoutSmokeTests.cs` : 해상도별 UI clipping 및 레이아웃 회귀 검사

전체 기능과 화면, 프로젝트 발전 과정은 저장소 루트 `README.md`에서 확인할 수 있습니다.
