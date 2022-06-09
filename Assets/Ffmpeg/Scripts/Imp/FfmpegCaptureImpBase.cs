using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace FfmpegUnity
{
    public abstract class FfmpegCaptureImpBase
    {
        public bool IsEnd
        {
            get;
            set;
        } = false;

        protected FfmpegCaptureCommand CaptureCommand
        {
            get;
            private set;
        } = null;

        public FfmpegCaptureImpBase(FfmpegCaptureCommand captureCommand)
        {
            CaptureCommand = captureCommand;
        }

        protected virtual string GenerateCaptureFileName()
        {
            return null;
        }

        public virtual string GenerateCaptureFileNameVideo(int width, int height)
        {
            return GenerateCaptureFileName();
        }

        public virtual string GenerateCaptureFileNameAudio(int sampleRate, int channels)
        {
            return GenerateCaptureFileName();
        }

        public abstract bool Reverse
        {
            get;
        }

        public abstract void WriteVideo(int streamId, string pipeFileName, int width, int height, Dictionary<int, byte[]> videoBuffers);

        protected void StreamWriteVideo(BinaryWriter writer, int streamId, Dictionary<int, byte[]> videoBuffers)
        {
            while (!IsEnd)
            {
                if (!videoBuffers.ContainsKey(streamId))
                {
                    Thread.Sleep(1);
                    continue;
                }

                byte[] buffer;
                lock (videoBuffers)
                {
                    buffer = videoBuffers[streamId];
                }

                try
                {
                    if (writer.BaseStream.CanWrite)
                    {
                        writer.Write(buffer);
                    }
                }
                catch (IOException)
                {
                    IsEnd = true;
                    break;
                }
            }
        }

        public abstract void WriteAudio(int streamId, string pipeFileName, int sampleRate, int channels, Dictionary<int, List<float>> audioBuffers);

        protected void StreamWriteAudio(BinaryWriter writer, int streamId, Dictionary<int, List<float>> audioBuffers)
        {
            while (!IsEnd)
            {
                if (!audioBuffers.ContainsKey(streamId) || audioBuffers[streamId] == null || audioBuffers[streamId].Count <= 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                //lock (audioBuffers[streamId])
                {
                    int loopLength = audioBuffers[streamId].Count;
                    for (int loop = 0; loop < loopLength; loop++)
                    {
                        if (writer.BaseStream.CanWrite)
                        {
                            writer.Write(audioBuffers[streamId][loop]);
                        }
                    }

                    audioBuffers[streamId].RemoveRange(0, loopLength);
                }
            }
        }
    }
}