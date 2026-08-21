# Local TC Server · 사내 AI 연산 분리

Local TC Studio는 두 가지 실행 방식을 지원합니다.

1. **이 PC에서 실행**: Appium Builder가 현재 PC의 Ollama/Qwen을 사용합니다.
2. **사내 AI 서버에서 실행**: 저사양 클라이언트 PC는 원본 TC/기획서 파일만 사내 Local TC Server로 전송하고, 문서 파싱·이미지 추출·Qwen Vision·TC 생성은 서버 PC가 담당합니다.

## 왜 서버 모드를 사용하는가

서버 모드에서는 클라이언트 PC가 Qwen 모델을 메모리에 올리지 않습니다. 또한 PPTX/PDF/DOCX/이미지 파싱도 서버에서 수행하므로 저사양 QA PC의 RAM/CPU 부담을 크게 줄일 수 있습니다.

클라이언트에는 생성 결과만 돌아오며, 프로젝트 학습 프로필은 기존처럼 각 클라이언트 PC의 `%LOCALAPPDATA%/AppiumBuilderReborn/TC`에 저장됩니다.

## 서버 PC 준비

서버 PC에는 .NET 8 SDK가 필요합니다.

```bat
LocalTcServer\start_local_tc_server.bat
```

기본 설정:

- Listen: `http://0.0.0.0:7788`
- AI model: `qwen3-vl:4b`
- Ollama endpoint: 서버 PC 내부 `127.0.0.1:11434`
- 동시 AI 작업: 1개씩 순차 처리

2B 모델을 사용하려면 서버 시작 전에 환경 변수를 지정합니다.

```bat
set LOCAL_TC_MODEL=qwen3-vl:2b
dotnet run --project LocalTcServer\LocalTcServer.csproj
```

첫 실행 시 서버가 접속 Token을 생성하고 콘솔에 표시합니다. Token은 서버 PC의 `%LOCALAPPDATA%/AppiumBuilderReborn/LocalTcServer/server-token.txt`에도 저장됩니다.

## 클라이언트 연결

Local TC Studio에서 `AI 설정`을 열고:

- `사내 AI 서버에서 실행` 선택
- 서버 주소 입력 (예: `http://192.168.0.30:7788`)
- 서버 콘솔의 Token 입력
- `연결 테스트`
- `적용`

클라이언트는 보안상 localhost 또는 사설/사내 IP 주소에만 연결할 수 있습니다. 공개 인터넷 IP로의 Local TC 자료 전송은 차단합니다.

## 보안 주의

기본 서버는 HTTP이므로 전송 내용 자체가 암호화되지는 않습니다. 신뢰된 사내 LAN 또는 격리된 테스트 네트워크에서 사용하세요. 민감도가 높은 환경에서는 서버 앞단에 사내 HTTPS reverse proxy를 두는 방식을 권장합니다.

접속 Token은 클라이언트에서 Windows DPAPI(CurrentUser)로 보호해 저장합니다. 서버는 요청마다 `X-Local-TC-Token`을 검사합니다.

## 처리 흐름

```text
저사양 QA PC
  Appium Builder / Local TC Studio
       │
       │ 원본 TC / PPTX / PDF / DOCX / 이미지
       ▼
사내 Local TC Server :7788
  ├─ 파일 임시 저장
  ├─ TC CSV/XLSX 분석
  ├─ 문서/이미지 파싱
  ├─ Ollama + Qwen Vision
  ├─ 프로젝트 규칙 학습 / TC 생성
  └─ 요청 종료 후 임시 원본 삭제
       │
       ▼
QA PC에 JSON 결과 반환 → Grid 편집 / CSV Export
```

서버는 한 번에 하나의 AI 요청을 처리해 여러 QA PC가 동시에 요청하더라도 모델 메모리가 중복 폭증하지 않도록 합니다.
