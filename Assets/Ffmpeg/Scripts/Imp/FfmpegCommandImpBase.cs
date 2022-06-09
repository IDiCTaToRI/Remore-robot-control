using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FfmpegUnity
{
    public abstract class FfmpegCommandImpBase
    {
        public bool LoopExit
        {
            get;
            set;
        } = false;

        public bool IsAlreadyBuild
        {
            get;
            set;
        } = false;

        protected FfmpegCommand CommandObj
        {
            get;
        } = null;

        public FfmpegCommandImpBase(FfmpegCommand command)
        {
            CommandObj = command;
        }

        public abstract bool IsRunning
        {
            get;
        }

        public abstract string StdErrLine(List<string> stdErrListForGetLine);

        public abstract IEnumerator StartFfmpegCoroutine(string options);

        public abstract void StopFfmpeg();

        protected string ParseOptions(string runOptions)
        {
            string options = runOptions.Replace("{PERSISTENT_DATA_PATH}", Application.persistentDataPath)
                .Replace("{TEMPORARY_CACHE_PATH}", Application.temporaryCachePath)
                .Replace("\r\n", "\n").Replace("\\\n", " ").Replace("^\n", " ").Replace("\n", " ");
            options = options.Replace("{STREAMING_ASSETS_PATH}", Application.streamingAssetsPath);
            return options;
        }
    }
}
