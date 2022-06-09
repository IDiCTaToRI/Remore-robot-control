using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace FfmpegUnity
{
    public class FfmpegGetTexturePerFrameCommand : FfmpegPlayerCommand
    {
        void Awake()
        {
            SyncFrameRate = false;
            StopPerFrame = true;
        }

        protected override void Update()
        {
            ExecuteStdErrLine();
        }

        public IEnumerator GetNextFrame()
        {
            while (!WriteNextTexture())
            {
                yield return null;
            }
        }
    }
}