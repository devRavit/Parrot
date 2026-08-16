# Parrot 🦜

Windows 커서 통일 도구. 어떤 프로그램을 쓰든 마우스 커서를 **하나의 커스텀 커서로 통일**합니다.
시스템 커서 교체와 게임 프로세스 인젝션 후킹 기법으로 구현했습니다.

## 기능
- **완전 자동**: 포그라운드(커서 밑) 프로세스를 감지해 앱마다 최적 방식을 자동 선택·학습
  - 일반 앱 → `SetSystemCursor` 교체
  - 인젝션 허용 앱(MuMuPlayer, LDPlayer 등) → DLL 후킹으로 그 앱 커서까지 교체
  - 인젝션 차단 앱 → 최상위 오버레이 + OS 커서 숨김
  - 강한 안티치트/커서잠금 게임(WoW 등) → 밴 안전을 위해 **건드리지 않음**
- 커서 **43종**(내장 9종 + CC0 34종) · **색상**(원본 포함 9색) · **크기 5단계** · 실시간 미리보기
- 모던 다크 **대시보드** · 트레이 상주 · **Windows 부팅 자동 시작** · 설정 로컬 저장
- **GitHub 자동 업데이트**(릴리스 버전 체크 → 자동 다운로드/설치)
- 크래시/종료 시 시스템 커서 자동 복구

## 아키텍처 (SOLID)
- **전략 패턴**(OCP): `ICursorStrategy` → `SystemCursorStrategy` / `InjectionStrategy` / `OverlayStrategy`
- **DIP**: `ISystemCursorService`, `IOverlayService`, `IInjectionService`, `ICursorProvider`,
  `IForegroundMonitor`, `ISettingsStore`, `IAutoStartService`, `IUpdateService`
- **OCP 규칙 체인**: `IProcessRule`(Self/Shell/AntiCheat) — 규칙 추가로 확장
- **SRP**: 감지(`ForegroundMonitor`) · 전략 결정(`StrategyResolver`) · 오케스트레이션(`CursorController`) 분리
- 조립 루트는 `App.OnStartup` (수동 DI)

```
app/
  Core/        추상화(인터페이스·값객체)·커서 핸들 유틸
  Services/    Win32/시스템 서비스 구현
  Strategies/  전략·규칙·리졸버
  CursorController.cs  오케스트레이터
  CursorArt.cs / CursorLibrary.cs  커서 렌더/카탈로그
native/        C 후킹 DLL(hookdll.c) + MinHook
installer/     설치 마법사(앱 내장)
```

## 빌드
```powershell
# 요구: .NET 10 SDK, mingw-w64(gcc)  (winget install Microsoft.DotNet.SDK.10 ; winget install BrechtSanders.WinLibs.POSIX.UCRT)
./build.ps1
```
결과: `dist\Parrot.exe`(런타임 내장 단일 exe), `dist\Parrot-Setup.exe`(설치 마법사), `dist\ParrotHook64.dll`.

## 설치
`Parrot-Setup.exe` 실행 → 위치/바로가기/자동시작 선택. 관리자 권한 불필요, 런타임 내장.

## 자동 업데이트
`github.com/devRavit/Parrot`의 최신 릴리스 태그(vX.Y.Z)를 확인해 새 버전이면 Setup 자산을 내려받아 설치합니다.
릴리스 배포: 태그 `vX.Y.Z` + 자산으로 `Parrot-Setup.exe` 업로드.

## 한계
- 진짜 독점 전체화면 게임은 오버레이가 가려질 수 있음.
- WoW 등 서명 강제/안티치트 게임은 안전상 미지원(정식 애드온 권장).

## 참고 (Reference)
- 커서 에셋: Kenney (CC0)
- 후킹 라이브러리: MinHook (BSD-2)
