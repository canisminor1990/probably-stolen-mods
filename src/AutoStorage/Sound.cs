using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AutoStorage;

internal static class Sound
{
    private const string ClipId = "storage_rules.sort_complete";
    private static AudioClip clip;
    private static bool tried;
    private static bool registered;

    internal static void Ensure()
    {
        if (clip != null)
        {
            Register();
            return;
        }

        if (tried)
        {
            return;
        }

        tried = true;
        try
        {
            Assembly assembly = typeof(Sound).Assembly;
            Stream stream = null;
            string[] names = assembly.GetManifestResourceNames();
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EndsWith("sort_complete.wav", StringComparison.OrdinalIgnoreCase))
                {
                    stream = assembly.GetManifestResourceStream(names[i]);
                    break;
                }
            }

            if (stream == null)
            {
                return;
            }

            using (stream)
            using (BinaryReader reader = new BinaryReader(stream))
            {
                clip = ReadWav(reader);
            }

            Register();
        }
        catch
        {
        }
    }

    internal static void Play()
    {
        try
        {
            Ensure();
            if (clip != null && AudioManager.Exists)
            {
                AudioManager.Instance.TryPlayOne(new[] { ClipId });
            }
        }
        catch
        {
        }
    }

    private static void Register()
    {
        if (registered || clip == null)
        {
            return;
        }

        try
        {
            AudioDirectory.RegisterModClip(ClipId, clip);
            registered = true;
        }
        catch
        {
        }
    }

    private static AudioClip ReadWav(BinaryReader reader)
    {
        if (FourCc(reader) != "RIFF")
        {
            return null;
        }

        reader.ReadUInt32();
        if (FourCc(reader) != "WAVE")
        {
            return null;
        }

        int format = 0;
        int channels = 0;
        int rate = 0;
        int block = 0;
        int bits = 0;
        byte[] data = null;
        long length = reader.BaseStream.Length;
        while (reader.BaseStream.Position + 8 <= length)
        {
            string id = FourCc(reader);
            uint size = reader.ReadUInt32();
            long next = reader.BaseStream.Position + size;
            if (size > int.MaxValue || next > length)
            {
                return null;
            }

            if (id == "fmt ")
            {
                format = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                rate = (int)reader.ReadUInt32();
                reader.ReadUInt32();
                block = reader.ReadUInt16();
                bits = reader.ReadUInt16();
            }
            else if (id == "data")
            {
                data = reader.ReadBytes((int)size);
            }

            reader.BaseStream.Position = Math.Min(length, next + ((size & 1) == 0 ? 0 : 1));
        }

        if (format != 1 || bits != 16 || channels <= 0 || channels > 8 || rate < 8000 || rate > 192000 || block != channels * 2 || data == null || data.Length < block)
        {
            return null;
        }

        int samples = data.Length / block;
        float[] pcm = new float[samples * channels];
        for (int i = 0; i < pcm.Length; i++)
        {
            pcm[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
        }

        AudioClip created = AudioClip.Create("StorageRules.SortComplete", samples, channels, rate, false);
        if (created == null || !created.SetData(pcm, 0))
        {
            return null;
        }

        return created;
    }

    private static string FourCc(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (bytes.Length != 4)
        {
            return "";
        }

        return System.Text.Encoding.ASCII.GetString(bytes);
    }
}
