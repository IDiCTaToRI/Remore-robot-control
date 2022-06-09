using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace FfmpegUnity
{
    public class FfmpegCommand : MonoBehaviour
    {
        public bool ExecuteOnStart = true;
        public string Options = "";
        public bool PrintStdErr = false;

        public bool IsRunning
        {
            get
            {
                if (CommandImp == null)
                {
                    return false;
                }

                return CommandImp.IsRunning;
            }
        }

        public bool GetProgressOnScript
        {
            get;
            set;
        } = false;
        public TimeSpan DurationTime
        {
            get;
            private set;
        } = TimeSpan.Zero;
        public TimeSpan CurrentTime
        {
            get;
            private set;
        } = TimeSpan.Zero;
        public float Progress
        {
            get
            {
                if (DurationTime.TotalSeconds <= 0.0)
                {
                    return 0f;
                }

                return (float)(CurrentTime.TotalSeconds / DurationTime.TotalSeconds);
            }
        }
        public float Speed
        {
            get;
            private set;
        } = 0f;
        public TimeSpan RemainingTime
        {
            get
            {
                if (Speed <= 0f)
                {
                    return TimeSpan.Zero;
                }

                return TimeSpan.FromSeconds((DurationTime - CurrentTime).TotalSeconds / Speed);
            }
        }

        public bool IsGetStdErr
        {
            get;
            protected set;
        } = false;
        protected string ParsedOptionInStreamingAssetsCopy = "";
        public string PathInStreamingAssetsCopy
        {
            get;
            private set;
        } = "";

        bool isAlreadyBuild_ = false;

        List<string> deleteAssets_ = new List<string>();

        List<string> stdErrListForGetLine_ = new List<string>();

        protected bool IsFinishedBuild
        {
            get;
            set;
        } = false;

        public string RunOptions
        {
            get;
            protected set;
        } = "";

        public FfmpegCommandImpBase CommandImp
        {
            get;
            private set;
        } = null;

        protected virtual void Build()
        {
            RunOptions = Options;
            IsGetStdErr = PrintStdErr || GetProgressOnScript;
            IsFinishedBuild = true;
        }

        protected virtual void Clean()
        {

        }

        protected virtual void CleanBuf()
        {

        }

        // Get outputs from stderr.
        public string GetStdErrLine()
        {
            if (stdErrListForGetLine_.Count <= 0)
            {
                return null;
            }
            string ret = stdErrListForGetLine_[0];
            stdErrListForGetLine_.RemoveAt(0);
            return ret;
        }

        string stdErrLine()
        {
            if (CommandImp == null)
            {
                return null;
            }
            return CommandImp.StdErrLine(stdErrListForGetLine_);
        }

        IEnumerator Start()
        {
            yield return null;

            if (ExecuteOnStart)
            {
                StartFfmpeg();
            }
        }

        void OnDestroy()
        {
            StopFfmpeg();

            foreach (var file in deleteAssets_)
            {
                File.Delete(file);
            }
            deleteAssets_.Clear();
        }

        // Start ffmpeg commands. (Continuous)
        // If you want stop commands, call StopFfmpeg().
        public void StartFfmpeg()
        {
            StartCoroutine(startFfmpegCoroutine());
        }

        public IEnumerator StreamingAssetsCopyPath(string path)
        {
            path = path.Replace(Application.streamingAssetsPath, "{STREAMING_ASSETS_PATH}").Replace("\"", "");

            string searchPath = Regex.Replace(path, @"\%[0\#\ \+\-]*[diouxXfeEgGcspn\%]", "*");
            searchPath = Regex.Escape(searchPath).Replace(@"\*", ".*");
            List<string> paths = new List<string>();
            using (UnityWebRequest unityWebRequest = UnityWebRequest.Get(Application.streamingAssetsPath + "/_FfmpegUnity_files.txt"))
            {
                yield return unityWebRequest.SendWebRequest();
                string[] allPaths = unityWebRequest.downloadHandler.text.Replace("\r\n", "\n").Split('\n');
                foreach (string singlePath in allPaths)
                {
                    string addPath = "{STREAMING_ASSETS_PATH}" + singlePath.Replace("\\", "/");
                    if (Regex.IsMatch(addPath, searchPath))
                    {
                        paths.Add(addPath);
                    }
                }
            }

            foreach (var loopPath in paths)
            {
                string streamingAssetPath = loopPath.Replace("{STREAMING_ASSETS_PATH}", Application.streamingAssetsPath);

                string targetItem = loopPath.Replace("{STREAMING_ASSETS_PATH}", Application.temporaryCachePath + "/FfmpegUnity_temp");

                if (!File.Exists(targetItem))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetItem));

                    byte[] data;
                    using (UnityWebRequest unityWebRequest = UnityWebRequest.Get(streamingAssetPath))
                    {
                        yield return unityWebRequest.SendWebRequest();
                        data = unityWebRequest.downloadHandler.data;
                    }
                    File.WriteAllBytes(targetItem, data);

                    deleteAssets_.Add(targetItem);
                }
            }

            PathInStreamingAssetsCopy = path.Replace("{STREAMING_ASSETS_PATH}", Application.temporaryCachePath + "/FfmpegUnity_temp");
        }

        void connectSplitStr(List<string> optionsSplit, char targetChar)
        {
            int countChar(string baseStr, char c)
            {
                string s = baseStr.Replace("\\" + c.ToString(), "");
                return s.Length - s.Replace(c.ToString(), "").Length;
            }

            for (int loop = 0; loop < optionsSplit.Count - 1; loop++)
            {
                if (countChar(optionsSplit[loop], targetChar) % 2 == 1)
                {
                    int loop2;
                    for (loop2 = loop + 1;
                        loop2 < optionsSplit.Count - 1 && countChar(optionsSplit[loop2], targetChar) % 2 != 1;
                        loop2++)
                    {
                        if (optionsSplit[loop2] != null)
                        {
                            optionsSplit[loop] += " " + optionsSplit[loop2];
                        }
                    }
                    optionsSplit[loop] += " " + optionsSplit[loop2];

                    optionsSplit.RemoveRange(loop + 1, loop2 - loop);
                    optionsSplit[loop] = optionsSplit[loop].Replace("\\" + targetChar.ToString(), "\n")
                        .Replace(targetChar.ToString(), "")
                        .Replace("\n", "\\" + targetChar.ToString());
                }
            }
        }

        public string[] CommandSplit(string command)
        {
            var optionsSplit = command.Split().ToList();
            connectSplitStr(optionsSplit, '\"');
            connectSplitStr(optionsSplit, '\'');

            return optionsSplit.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        }

        public IEnumerator StreamingAssetsCopyOptions(string options)
        {
            options = options.Replace(Application.streamingAssetsPath, "{STREAMING_ASSETS_PATH}");

            var optionsSplit = CommandSplit(options);

            ParsedOptionInStreamingAssetsCopy = "";
            foreach (var optionItem in optionsSplit)
            {
                if (optionItem != null)
                {
                    string targetItem = optionItem;
                    if (optionItem.Contains("{STREAMING_ASSETS_PATH}"))
                    {
                        yield return StreamingAssetsCopyPath(optionItem);
                        targetItem = PathInStreamingAssetsCopy;
                    }

                    ParsedOptionInStreamingAssetsCopy += " " + targetItem;
                }
            }
        }

        IEnumerator startFfmpegCoroutine()
        {
            if (isAlreadyBuild_)
            {
                yield break;
            }

            isAlreadyBuild_ = true;

#if UNITY_EDITOR_WIN && FFMPEG_UNITY_USE_BINARY_WIN
            CommandImp = new FfmpegCommandImpWinMonoBin(this);
#elif UNITY_EDITOR_WIN && !FFMPEG_UNITY_USE_BINARY_WIN
            CommandImp = new FfmpegCommandImpWinLib(this);
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            CommandImp = new FfmpegCommandImpUnixMonoBin(this);
#elif UNITY_STANDALONE_WIN && !FFMPEG_UNITY_USE_BINARY_WIN
            CommandImp = new FfmpegCommandImpWinLib(this);
#elif UNITY_STANDALONE_WIN && !ENABLE_IL2CPP
            CommandImp = new FfmpegCommandImpWinMonoBin(this);
#elif UNITY_STANDALONE_WIN && ENABLE_IL2CPP
            CommandImp = new FfmpegCommandImpWinIL2CPPBin(this);
#elif (UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX) && !ENABLE_IL2CPP
            CommandImp = new FfmpegCommandImpUnixMonoBin(this);
#elif UNITY_STANDALONE_OSX && ENABLE_IL2CPP
            CommandImp = new FfmpegCommandImpMacIL2CPPBin(this);
#elif UNITY_STANDALONE_LINUX && ENABLE_IL2CPP
            CommandImp = new FfmpegCommandImpLinuxIL2CPPBin(this);
#elif UNITY_ANDROID
            CommandImp = new FfmpegCommandImpAndroid(this);
#elif UNITY_IOS
            CommandImp = new FfmpegCommandImpIOS(this);
#endif

            CommandImp.IsAlreadyBuild = true;
            CommandImp.LoopExit = false;

            IsFinishedBuild = false;
            Build();
            while (!IsFinishedBuild)
            {
                yield return null;
            }

            if (string.IsNullOrWhiteSpace(RunOptions))
            {
                yield break;
            }

            yield return CommandImp.StartFfmpegCoroutine(RunOptions);
        }

        // Stop ffmpeg commands.
        public void StopFfmpeg()
        {
            if (CommandImp == null)
            {
                return;
            }

            CommandImp.LoopExit = true;

            Clean();

            CommandImp.StopFfmpeg();

            CleanBuf();

            PathInStreamingAssetsCopy = "";

            isAlreadyBuild_ = false;
            CommandImp.IsAlreadyBuild = false;
        }

        // Execute ffmpeg. (Once)
        // If you want to use StopFfmpeg(), call StartFfmpeg() instead.
        public void ExecuteFfmpeg()
        {
            StartFfmpeg();

            StartCoroutine(executeFfmpegCoroutine());
        }

        IEnumerator executeFfmpegCoroutine()
        {
            while (!IsRunning)
            {
                yield return null;
            }

            while (IsRunning)
            {
                yield return null;
            }

            StopFfmpeg();
        }

        protected void ExecuteStdErrLine()
        {
            string stdErrLoopResult;
            do
            {
                stdErrLoopResult = stdErrLine();
                if (PrintStdErr && stdErrLoopResult != null)
                {
                    UnityEngine.Debug.Log(stdErrLoopResult);
                }
                if (GetProgressOnScript && stdErrLoopResult != null)
                {
                    if (stdErrLoopResult.Contains("Duration: "))
                    {
                        string durationStr = stdErrLoopResult.Split(new[] { "Duration: " }, StringSplitOptions.None)[1].Split(',')[0];
                        TimeSpan result;
                        if (TimeSpan.TryParse(durationStr, out result))
                        {
                            DurationTime = result;
                        }
                    }
                    else if (stdErrLoopResult.Contains("time=") && stdErrLoopResult.Contains("speed="))
                    {
                        string str = stdErrLoopResult.Split(new[] { "time=" }, StringSplitOptions.None)[1].Split()[0];
                        TimeSpan result;
                        if (TimeSpan.TryParse(str, out result))
                        {
                            CurrentTime = result;
                        }

                        str = stdErrLoopResult.Split(new[] { "speed=" }, StringSplitOptions.None)[1].Split('x')[0];
                        float speed;
                        if (float.TryParse(str, out speed))
                        {
                            Speed = speed;
                        }
                    }
                }
            } while (stdErrLoopResult != null);
        }
        
        protected virtual void Update()
        {
            ExecuteStdErrLine();
        }
    }
}
