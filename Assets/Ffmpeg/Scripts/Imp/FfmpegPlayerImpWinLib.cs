#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace FfmpegUnity
{
    public class FfmpegPlayerImpWinLib : FfmpegPlayerImpBase
    {
        FileStream fileStream_;
        TextReader reader_;
        string ffprobeFilePath_ = null;

        Dictionary<int, string> pipeFileNames_ = new Dictionary<int, string>();

        [DllImport("libffmpegDll")]
        static extern void ffprobe_main(int argc, IntPtr argv, string file_path);
        [DllImport("avformat-59")]
        static extern IntPtr unitybuf_get_handle_dll(string uri);
        [DllImport("avformat-59")]
        static extern int unitybuf_read_dll(IntPtr handle, IntPtr buf, int size);
        [DllImport("avformat-59")]
        static extern int unitybuf_clear_dll(IntPtr handle);
        [DllImport("avformat-59")]
        static extern int unitybuf_count_dll(IntPtr handle);

        public FfmpegPlayerImpWinLib(FfmpegPlayerCommand playerCommand) : base(playerCommand)
        {

        }

        public override bool IsEOF
        {
            get;
            protected set;
        } = false;

        public override IEnumerator OpenFfprobeReaderCoroutine(string inputPathAll)
        {
            ffprobeFilePath_ = Application.temporaryCachePath + "/" + Guid.NewGuid().ToString() + ".txt";

            int elementSize = Marshal.SizeOf(typeof(IntPtr));

            string[] options = new[] { "ffprobe", "-i", inputPathAll, "-show_streams" };

            IntPtr optionsPtr = Marshal.AllocHGlobal(options.Length * elementSize);

            List<IntPtr> optionsStrPtr = new List<IntPtr>();
            foreach (string str in options)
            {
                IntPtr strPtr = Marshal.StringToHGlobalAnsi(str);
                Marshal.WriteIntPtr(optionsPtr, optionsStrPtr.Count * elementSize, strPtr);
                optionsStrPtr.Add(strPtr);
            }

            ffprobe_main(optionsStrPtr.Count, optionsPtr, ffprobeFilePath_);

            foreach (IntPtr strPtr in optionsStrPtr)
            {
                Marshal.FreeHGlobal(strPtr);
            }

            Marshal.FreeHGlobal(optionsPtr);

            reader_ = null;
            bool loopFlag;
            do
            {
                yield return null;

                loopFlag = false;
                try
                {
                    fileStream_ = new FileStream(ffprobeFilePath_, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    reader_ = new StreamReader(fileStream_);
                }
                catch (IOException)
                {
                    loopFlag = true;
                }
            } while (loopFlag);
        }

        public override TextReader OpenFfprobeReader(string inputPathAll)
        {
            return reader_;
        }

        public override void CloseFfprobeReader()
        {
            if (reader_ != null)
            {
                //reader_.ReadToEnd();
                reader_.Dispose();
                reader_ = null;
            }

            if (fileStream_ != null)
            {
                fileStream_.Close();
                fileStream_.Dispose();
                fileStream_ = null;
            }
        }

        public override string BuildBeginOptions(string path)
        {
            return " -re -i \"" + path + "\" ";
        }

        public override string BuildVideoOptions(int streamId, int width, int height)
        {
            string fileName = "unitybuf:" + (width * height * 4) + "/2/" + Guid.NewGuid().ToString();
            if (pipeFileNames_.ContainsKey(streamId))
            {
                pipeFileNames_.Remove(streamId);
            }
            pipeFileNames_.Add(streamId, fileName);
            return " -f rawvideo -pix_fmt rgba \"" + fileName + "\" ";
        }

        public override string BuildAudioOptions(int streamId, int sampleRate, int channels)
        {
            //string fileName = "unitybuf:" + (sampleRate * channels * 4 / 50) + "/0/" + Guid.NewGuid().ToString();
            string fileName = "unitybuf:0/0/" + Guid.NewGuid().ToString();
            if (pipeFileNames_.ContainsKey(streamId))
            {
                pipeFileNames_.Remove(streamId);
            }
            pipeFileNames_.Add(streamId, fileName);
            return " -f f32le \"" + fileName + "\" ";
        }

        public override void CleanBuf()
        {
            foreach (var fileName in pipeFileNames_.Values)
            {
                unitybuf_clear_dll(unitybuf_get_handle_dll(fileName));
            }
            pipeFileNames_.Clear();

            if (string.IsNullOrEmpty(ffprobeFilePath_))
            {
            	try
            	{
                	File.Delete(ffprobeFilePath_);
                }
                catch (Exception)
                {
                }
            }
        }

        public override void ReadVideo(int streamId, int width, int height)
        {
            byte[] buf = new byte[width * height * 4];

            int elementSize = Marshal.SizeOf(typeof(byte));
            IntPtr bufPtr = Marshal.AllocHGlobal(buf.Length * elementSize);

            IntPtr handle;
            do
            {
                handle = unitybuf_get_handle_dll(pipeFileNames_[streamId]);
                if (handle == IntPtr.Zero)
                {
                    Thread.Sleep(1);
                }
            } while (handle == IntPtr.Zero && !IsEnd);

            while (!IsEnd)
            {
                /*
                if (unitybuf_count_dll(handle) <= 0)
                {
                    Thread.Sleep(1);
                    continue;
                }
                */

                int readSize = unitybuf_read_dll(handle, bufPtr, buf.Length);
                if (readSize == -('E' | ('O' << 8) | ('F' << 16) | (' ' << 24)))
                {
                    break;
                }
                else if (readSize <= 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                Marshal.Copy(bufPtr, buf, 0, readSize);

                var newVideoBuffer = new byte[buf.Length];
                for (int y = 0; y < height; y++)
                {
                    Array.Copy(buf, y * width * 4,
                        newVideoBuffer, (height - y - 1) * width * 4,
                        width * 4);
                }

                PlayerCommand.SetVideoBuffer(streamId, newVideoBuffer);

                PlayerCommand.StopPerFrameFunc(streamId);
            }

            Marshal.FreeHGlobal(bufPtr);

            IsEOF = true;
        }

        public override void ReadAudio(int streamId, int sampleRate, int channels)
        {
            int size = 1024; //sampleRate * channels * 4 / 50;
            int elementSizeFloat = Marshal.SizeOf(typeof(float));
            IntPtr bufPtr = Marshal.AllocHGlobal(size);

            IntPtr handle;
            do
            {
                handle = unitybuf_get_handle_dll(pipeFileNames_[streamId]);
                if (handle == IntPtr.Zero)
                {
                    Thread.Sleep(1);
                }
            } while (handle == IntPtr.Zero && !IsEnd);

            while (!IsEnd)
            {
                int readSize = unitybuf_read_dll(handle, bufPtr, size);

                if (readSize == -('E' | ('O' << 8) | ('F' << 16) | (' ' << 24)))
                {
                    break;
                }
                else if (readSize <= 0)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    float[] floatBuffer = new float[readSize / elementSizeFloat];
                    Marshal.Copy(bufPtr, floatBuffer, 0, readSize / elementSizeFloat);
                    PlayerCommand.AddAudioBuffer(streamId, floatBuffer);
                }
            }

            Marshal.FreeHGlobal(bufPtr);

            IsEOF = true;
        }
    }
}

#endif