// Parrot — cursor hook DLL (production).
// Injected into a target process; hooks SetCursor / SetClassLongPtrW so the app's own cursor
// is replaced by our custom cursor. Respects the app hiding the cursor (SetCursor(NULL)) so
// aim/lock modes still hide correctly. Loads the cursor from
//   %LOCALAPPDATA%\Parrot\inject\active.cur  and live-reloads it when that file changes.
#include <windows.h>
#include <stdio.h>
#include <wchar.h>
#include "MinHook.h"

static HCURSOR g_cur = NULL;

typedef HCURSOR (WINAPI *SetCursor_t)(HCURSOR);
static SetCursor_t oSetCursor = NULL;
static HCURSOR WINAPI hkSetCursor(HCURSOR h)
{
    // Respect the app hiding the cursor: if it sets NULL, let it hide (don't force ours).
    if (h == NULL) return oSetCursor(NULL);
    return oSetCursor(g_cur ? g_cur : h);
}

typedef ULONG_PTR (WINAPI *SetClassLongPtrW_t)(HWND, int, LONG_PTR);
static SetClassLongPtrW_t oSetClassLongPtrW = NULL;
static ULONG_PTR WINAPI hkSetClassLongPtrW(HWND hWnd, int idx, LONG_PTR v)
{
    if (idx == GCLP_HCURSOR && g_cur && v != 0) v = (LONG_PTR)g_cur;
    return oSetClassLongPtrW(hWnd, idx, v);
}

static void BuildDir(wchar_t* out, int cap)
{
    wchar_t base[MAX_PATH]; base[0] = 0;
    GetEnvironmentVariableW(L"LOCALAPPDATA", base, MAX_PATH);
    _snwprintf(out, cap, L"%s\\Parrot\\inject", base);
}
static void BuildPath(wchar_t* out, int cap)
{
    wchar_t dir[MAX_PATH]; BuildDir(dir, MAX_PATH);
    _snwprintf(out, cap, L"%s\\active.cur", dir);
}

static void LoadCur(void)
{
    wchar_t p[MAX_PATH]; BuildPath(p, MAX_PATH);
    HCURSOR c = (HCURSOR)LoadImageW(NULL, p, IMAGE_CURSOR, 0, 0, LR_LOADFROMFILE);
    if (c) g_cur = c;
}

static DWORD WINAPI Worker(LPVOID param)
{
    (void)param;
    LoadCur();

    if (MH_Initialize() != MH_OK) return 0;
    MH_CreateHook((LPVOID)&SetCursor, (LPVOID)&hkSetCursor, (LPVOID*)&oSetCursor);
    MH_CreateHook((LPVOID)&SetClassLongPtrW, (LPVOID)&hkSetClassLongPtrW, (LPVOID*)&oSetClassLongPtrW);
    MH_EnableHook(MH_ALL_HOOKS);

    // Live-reload active.cur when it changes (design/size/color updates).
    wchar_t dir[MAX_PATH]; BuildDir(dir, MAX_PATH);
    HANDLE hn = FindFirstChangeNotificationW(dir, FALSE, FILE_NOTIFY_CHANGE_LAST_WRITE | FILE_NOTIFY_CHANGE_FILE_NAME);
    for (;;)
    {
        if (hn == INVALID_HANDLE_VALUE || hn == NULL) { Sleep(1500); LoadCur(); continue; }
        DWORD w = WaitForSingleObject(hn, INFINITE);
        if (w == WAIT_OBJECT_0) { Sleep(80); LoadCur(); FindNextChangeNotification(hn); }
    }
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE hinst, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hinst);
        CreateThread(NULL, 0, Worker, NULL, 0, NULL);
    }
    return TRUE;
}
