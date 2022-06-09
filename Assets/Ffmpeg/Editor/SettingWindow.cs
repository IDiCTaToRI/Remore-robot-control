using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FfmpegUnity
{
    public class SettingWindow : EditorWindow
    {
        enum BinaryToUse
        {
            BuiltInBinary = 1,
            InstalledBinary = 2,
        }

        enum BinaryOrLibraryToUse
        {
            Library = 0,
            BuiltInBinary = 1,
            InstalledBinary = 2,
        }

        enum IPCForMoblie
        {
            LibraryMemory,
            Pipe,
        }

        BinaryOrLibraryToUse windowsUse_;
        BinaryToUse macUse_;
        BinaryToUse linuxUse_;
        IPCForMoblie androidIPC_;
        IPCForMoblie iosIPC_;

        [MenuItem("Tools/Ffmpeg for Unity/Open Setting Window")]
        static void openDialogWindow()
        {
            bool isOpenWindow = false;

            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window.GetType() == typeof(SettingWindow))
                {
                    window.Focus();
                    isOpenWindow = true;
                }
            }

            if (!isOpenWindow)
            {
                GetWindow<SettingWindow>(true, "Ffmpeg for Unity", true);
            }
        }

        static void setScriptingDefineSymbol(BuildTargetGroup target, string symbol)
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            if (!symbols.Contains(symbol))
            {
                if (symbols == null || symbols == "")
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(target, symbol);
                }
                else
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(target, symbols + ";" + symbol);
                }
            }
        }

        static string deleteSymbol(string symbols, string symbol)
        {
            if (symbols.Contains(";" + symbol))
            {
                symbols = symbols.Replace(";" + symbol, "");
            }
            else if (symbols.Contains(symbol + ";"))
            {
                symbols = symbols.Replace(symbol + ";", "");
            }
            else
            {
                symbols = symbols.Replace(symbol, "");
            }

            return symbols;
        }

        static void deleteScriptingDefineSymbol(BuildTargetGroup target, string symbol)
        {
            string symbols;

            symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            symbols = deleteSymbol(symbols, symbol);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(target, symbols);
        }

        void Awake()
        {
            if (PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone).Contains("FFMPEG_UNITY_USE_BINARY_WIN"))
            {
                if (PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone).Contains("FFMPEG_UNITY_USE_OUTER_WIN"))
                {
                    windowsUse_ = BinaryOrLibraryToUse.InstalledBinary;
                }
                else
                {
                    windowsUse_ = BinaryOrLibraryToUse.BuiltInBinary;
                }
            }
            else
            {
                windowsUse_ = BinaryOrLibraryToUse.Library;
            }

            if (PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone).Contains("FFMPEG_UNITY_USE_OUTER_MAC"))
            {
                macUse_ = BinaryToUse.InstalledBinary;
            }
            else
            {
                macUse_ = BinaryToUse.BuiltInBinary;
            }

            if (PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone).Contains("FFMPEG_UNITY_USE_OUTER_LINUX"))
            {
                linuxUse_ = BinaryToUse.InstalledBinary;
            }
            else
            {
                linuxUse_ = BinaryToUse.BuiltInBinary;
            }

            if (PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android).Contains("FFMPEG_UNITY_USE_PIPE"))
            {
                androidIPC_ = IPCForMoblie.Pipe;
            }
            else
            {
                androidIPC_ = IPCForMoblie.LibraryMemory;
            }

            if (PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS).Contains("FFMPEG_UNITY_USE_PIPE"))
            {
                iosIPC_ = IPCForMoblie.Pipe;
            }
            else
            {
                iosIPC_ = IPCForMoblie.LibraryMemory;
            }
        }

        void OnGUI()
        {
            var currentPosition = position;
            currentPosition.height = 200f;
            position = currentPosition;

            GUILayout.Label("Binary / Library to Use (for Editor and Standalone)");

            var newWindowsUse = (BinaryOrLibraryToUse)EditorGUILayout.EnumPopup("Windows", windowsUse_);
            if (newWindowsUse != windowsUse_)
            {
                switch (newWindowsUse)
                {
                    case BinaryOrLibraryToUse.Library:
                        deleteScriptingDefineSymbol(BuildTargetGroup.Standalone, "FFMPEG_UNITY_USE_BINARY_WIN");
                        deleteScriptingDefineSymbol(BuildTargetGroup.Standalone, "FFMPEG_UNITY_USE_OUTER_WIN");
                        deleteScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_BINARY_WIN");
                        deleteScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_OUTER_WIN");
                        deleteScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_BINARY_WIN");
                        deleteScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_OUTER_WIN");
                        break;
                    case BinaryOrLibraryToUse.BuiltInBinary:
                        setScriptingDefineSymbol(BuildTargetGroup.Standalone, "FFMPEG_UNITY_USE_BINARY_WIN");
                        deleteScriptingDefineSymbol(BuildTargetGroup.Standalone, "FFMPEG_UNITY_USE_OUTER_WIN");
                        setScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_BINARY_WIN");
                        deleteScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_OUTER_WIN");
                        setScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_BINARY_WIN");
                        deleteScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_OUTER_WIN");
                        break;
                    case BinaryOrLibraryToUse.InstalledBinary:
                        setScriptingDefineSymbol(BuildTargetGroup.Standalone, "FFMPEG_UNITY_USE_BINARY_WIN");
                        setScriptingDefineSymbol(BuildTargetGroup.Standalone, "FFMPEG_UNITY_USE_OUTER_WIN");
                        setScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_BINARY_WIN");
                        setScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_OUTER_WIN");
                        setScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_BINARY_WIN");
                        setScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_OUTER_WIN");
                        break;
                }

                windowsUse_ = newWindowsUse;
            }

            var newMacUse = (BinaryToUse)EditorGUILayout.EnumPopup("Mac", macUse_);
            if (newMacUse != macUse_)
            {
                switch (newMacUse)
                {
                    case BinaryToUse.BuiltInBinary:
                        deleteScriptingDefineSymbol(BuildTargetGroup.Standalone, "FFMPEG_UNITY_USE_OUTER_MAC");
                        deleteScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_OUTER_MAC");
                        deleteScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_OUTER_MAC");
                        break;
                    case BinaryToUse.InstalledBinary:
                        setScriptingDefineSymbol(BuildTargetGroup.Standalone, "FFMPEG_UNITY_USE_OUTER_MAC");
                        setScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_OUTER_MAC");
                        setScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_OUTER_MAC");
                        break;
                }

                macUse_ = newMacUse;
            }

            var newLinuxUse = (BinaryToUse)EditorGUILayout.EnumPopup("Linux", linuxUse_);
            if (newLinuxUse != linuxUse_)
            {
                switch (newLinuxUse)
                {
                    case BinaryToUse.BuiltInBinary:
                        deleteScriptingDefineSymbol(BuildTargetGroup.Standalone, "FFMPEG_UNITY_USE_OUTER_LINUX");
                        deleteScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_OUTER_LINUX");
                        deleteScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_OUTER_LINUX");
                        break;
                    case BinaryToUse.InstalledBinary:
                        setScriptingDefineSymbol(BuildTargetGroup.Standalone, "FFMPEG_UNITY_USE_OUTER_LINUX");
                        setScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_OUTER_LINUX");
                        setScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_OUTER_LINUX");
                        break;
                }

                linuxUse_ = newLinuxUse;
            }

            GUILayout.Label("IPC Mode (for Moblie)");

            var newAndroidIPC = (IPCForMoblie)EditorGUILayout.EnumPopup("Android", androidIPC_);
            if (newAndroidIPC != androidIPC_)
            {
                switch (newAndroidIPC)
                {
                    case IPCForMoblie.LibraryMemory:
                        deleteScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_PIPE");
                        break;
                    case IPCForMoblie.Pipe:
                        setScriptingDefineSymbol(BuildTargetGroup.Android, "FFMPEG_UNITY_USE_PIPE");
                        break;
                }

                androidIPC_ = newAndroidIPC;
            }

            var newIosIPC = (IPCForMoblie)EditorGUILayout.EnumPopup("iOS", iosIPC_);
            if (newIosIPC != iosIPC_)
            {
                switch (newIosIPC)
                {
                    case IPCForMoblie.LibraryMemory:
                        deleteScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_PIPE");
                        break;
                    case IPCForMoblie.Pipe:
                        setScriptingDefineSymbol(BuildTargetGroup.iOS, "FFMPEG_UNITY_USE_PIPE");
                        break;
                }

                iosIPC_ = newIosIPC;
            }

            GUILayout.Space(20);

            if (GUILayout.Button("Close"))
            {
                Close();
            }
        }
    }
}
