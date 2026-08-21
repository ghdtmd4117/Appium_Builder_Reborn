# Local TC Studio server-mode UI

The distributable ZIP produced for this experimental branch contains the Local TC Studio UI wiring for `LocalPc` / `IntranetServer` execution mode.

The UI behavior is:

- `AI 설정` opens execution mode settings.
- Local mode keeps the existing Qwen 2B/4B model selection flow.
- Intranet mode accepts a private-LAN Local TC Server endpoint and access token.
- Selecting TC/planning files does not parse them on the client; parsing is deferred to the selected execution location.
- Switching to intranet mode stops an Ollama server owned by Appium Builder to release client RAM.
- Profile learning and TC generation use `LocalTcRemoteClient` in intranet mode.

The core server/client projects and endpoint tests in this branch are intended to be compiled by CI before the UI wiring is promoted to main.
