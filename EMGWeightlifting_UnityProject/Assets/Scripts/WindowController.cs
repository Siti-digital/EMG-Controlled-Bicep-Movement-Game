using UnityEngine;
#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
#endif

public class WindowController : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_MINIMIZE = 6;
#endif

    public void MinimizeWindow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr hWnd = GetActiveWindow();
        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_MINIMIZE);
        }
#else
        Debug.Log("[WindowController] Minimize window requested.");
#endif
    }

    public void ToggleMaximize()
    {
        Screen.fullScreen = !Screen.fullScreen;
        Debug.Log("[WindowController] FullScreen toggled: " + Screen.fullScreen);
    }

    public void QuitGame()
    {
        Debug.Log("[WindowController] Quitting application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
