using System.Diagnostics.Metrics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
using FrostySdk.IO;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using ParaTranz2FrostyLoc.Logger;

namespace ParaTranz2FrostyLoc;

public class Database
{
    //public Dictionary<uint, string> strings = new Dictionary<uint, string>();
    public class HistogramChunk
    {
        public uint unk;
        public long size;
        public uint unk2;
        public List<ushort> values = new();
    }
    public HistogramChunk histogramChunk { get; set; } = new();

    public class StringBinaryChunk
    {
        public uint magic;
        public uint size;
        public int count;
        public uint dataOffset;
        public uint stringsOffset;
        public string tag = "";
        public List<uint> ids = new();
        public List<uint> offsets = new();
        public Dictionary<uint, string> strings = new();
        public string Path = "";
    }
    public StringBinaryChunk stringChunk { get; set; } = new();

    //protected byte[] buffer;

    public void InitializeHistograms(string ChunkPath)
    {
        using (NativeReader reader = new(File.OpenRead(ChunkPath)))
        {
            histogramChunk.unk = reader.ReadUInt();
            histogramChunk.size = reader.ReadUInt();
            histogramChunk.unk2 = reader.ReadUInt();

            for (int i = 0; i < (histogramChunk.size / 2); i++)
                histogramChunk.values.Add(reader.ReadUShort());
        }
    }

    public void InitializeStrings(string ChunkPath)
    {
        stringChunk.Path = ChunkPath;
        using (NativeReader reader = new(File.OpenRead(ChunkPath)))
        {
            stringChunk.magic = reader.ReadUInt();
            if (stringChunk.magic != 0x00039000)
                throw new InvalidDataException();

            stringChunk.size = reader.ReadUInt();
            stringChunk.count = reader.ReadInt();
            stringChunk.dataOffset = reader.ReadUInt();
            stringChunk.stringsOffset = reader.ReadUInt();
            stringChunk.tag = reader.ReadNullTerminatedString();

            reader.Position = stringChunk.dataOffset + 8;
            
            stringChunk.ids = new List<uint>();
            stringChunk.offsets = new List<uint>();

            while (reader.Position != stringChunk.stringsOffset + 8)
            {
                stringChunk.ids.Add(reader.ReadUInt());
                stringChunk.offsets.Add(reader.ReadUInt());
            }

            for (int i = 0; i < stringChunk.ids.Count; i++)
            {
                reader.Position = stringChunk.stringsOffset + stringChunk.offsets[i] + 8;

                string str = reader.ReadNullTerminatedString();
                string dstStr = "";


                for (int j = 0; j < str.Length; j++)
                {
                    byte b = (byte)str[j];

                    if (b < 0x80)
                        dstStr += (char)b;
                    else
                    {
                        ushort tmp = histogramChunk.values[b];
                        if (tmp >= 0x80)
                            dstStr += (char)tmp;
                        else
                        {
                            b = (byte)str[++j];
                            if (b >= 0x80)
                            {
                                b -= 0x80;
                                dstStr += (char)histogramChunk.values[(b + (tmp << 7))];
                            }
                        }
                    }
                }

                if (!stringChunk.strings.ContainsKey(stringChunk.ids[i]))
                {
                    stringChunk.strings.Add(stringChunk.ids[i], dstStr);
                }
            }
        }
    }

    public string ShowUnsupportChars(List<string> strings)
    {
        List<char> currentHistogram = new();
        List<char> unsupportedChars = new();

        foreach (var c in histogramChunk.values)
        {
            currentHistogram.Add((char)c);
        }

        foreach (var str in strings)
        {
            foreach (char c in str.ToCharArray())
            {
                if (!(c < 0x80))
                {
                    int index = currentHistogram.FindIndex((char a) => { return a.Equals(c); });
                    if (index == -1)
                    {
                        unsupportedChars.Add(c);
                        continue;
                    }

                    //if (index > 0xFF)
                    //{
                    //    //List<byte> list = GetShifts(currentHistogram);
                    //    List<byte> list = new();
                    //    for (int i = 0x1FE; i >= 0x80; i--)
                    //    {
                    //        if (currentHistogram[i] < 0x80)
                    //            list.Add((byte)i);
                    //    }

                    //    foreach (byte shift in list)
                    //    {
                    //        if ((index - (currentHistogram[shift] << 7)) < 0x80)
                    //            break;
                    //    }
                    //    unsupportedChars.Add(c);
                    //}
                }
            }
        }

        unsupportedChars = unsupportedChars.Distinct().ToList();

        StringBuilder sb = new StringBuilder();
        foreach (var item in unsupportedChars)
        {
            sb.Append(item);
        }
        return sb.ToString();
    }

    public void WriteChunks(string HistogramChunkPath, string StringChunkPath, Logger.Logger logger, byte[] histogram, Dictionary<uint, string> newStrings)
    {
        using (NativeWriter writer = new(new FileStream(HistogramChunkPath, FileMode.Create)))
        {
            writer.Write(histogramChunk.unk);
            writer.Write(0xDEADBEEF);
            writer.Write(histogramChunk.unk2);

            foreach (var c in histogramChunk.values)
            {
                if (c > 0xFFFF)
                    logger.LogWarning(((char)c).ToString());
                else
                    writer.Write(c);
            }

            writer.Write(histogram);
            writer.Write(0x0000);

            logger.LogInformation("Histogram Chunk size is: " + writer.Position.ToString());

            uint size = (uint)(writer.Position - 8);
            writer.Position = 4;
            writer.Write(size);
        }

        List<char> currentHistogram = new();

        using (NativeReader reader = new(File.OpenRead(HistogramChunkPath)))
        {
            reader.ReadUInt();
            long size = reader.ReadUInt();
            reader.ReadUInt();

            for (int i = 0; i < (size / 2); i++)
                currentHistogram.Add(reader.ReadWideChar());
        }

        string tag = null;
        List<uint> ids = null;
        List<uint> offsets = null;
        Dictionary<uint, string> strings = new Dictionary<uint, string>();

        using (NativeReader reader = new(File.OpenRead(stringChunk.Path)))
        {
            uint magic = reader.ReadUInt();
            if (magic != stringChunk.magic)
                throw new InvalidDataException();

            uint size = reader.ReadUInt();
            int count = reader.ReadInt();
            uint dataOffset = reader.ReadUInt();
            uint stringsOffset = reader.ReadUInt();
            tag = reader.ReadNullTerminatedString();
            if (tag != stringChunk.tag)
                throw new InvalidDataException();

            reader.Position = dataOffset + 8;

            ids = new List<uint>(count);
            offsets = new List<uint>(count);

            for (int i = 0; i < count; i++)
            {
                ids.Add(reader.ReadUInt());
                offsets.Add(reader.ReadUInt());
            }

            for (int i = 0; i < count; i++)
            {
                reader.Position = stringsOffset + offsets[i] + 8;
                strings[ids[i]] = reader.ReadNullTerminatedString();

            }
        }

        //List<char> currentHistogram = new();
        //List<ushort> unsupportedChars = new();

        //foreach (var c in histogramChunk.values)
        //{
        //    currentHistogram.Add((char)c);
        //}

        //foreach (var item in stringChunk.strings)
        //{
        //    foreach (char c in item.Value.ToCharArray())
        //    {
        //        if (!(c < 0x80) && currentHistogram.FindIndex((char a) => { return a.Equals(c); }) == -1)
        //        {
        //            unsupportedChars.Add((ushort)c);
        //        }
        //    }
        //}

        //GC.Collect();

        //unsupportedChars = unsupportedChars.Distinct().ToList();
        //histogramChunk.values.AddRange(unsupportedChars);
        //currentHistogram = new();

        //using (NativeWriter writer = new(new FileStream(HistogramChunkPath, FileMode.Create)))
        //{
        //    writer.Write(histogramChunk.unk);
        //    writer.Write(0xDEADBEEF);
        //    writer.Write(histogramChunk.unk2);

        //    foreach (ushort c in histogramChunk.values)
        //    {
        //        if (c > 0xFFFF)
        //            logger.LogWarning(((char)c).ToString());
        //        else
        //        {
        //            writer.Write(c);
        //            currentHistogram.Add((char)c);
        //        }
        //    }

        //    logger.LogInformation("Histogram Chunk size is: " + writer.Position.ToString());

        //    uint size = (uint)(writer.Position - 8);
        //    writer.Position = 4;
        //    writer.Write(size);
        //}



        List<char> debugErrorChars = new();


        foreach (var strInfo in newStrings)
        {
            string str = strInfo.Value;
            StringBuilder sb = new StringBuilder();
            for (int j = 0; j < str.Length; j++)
            {
                char b = str[j];

                if (b < 0x80)
                    sb.Append(b);
                else
                {
                    int index = currentHistogram.FindIndex((char a) => { return a.Equals(b); });
                    if (index == -1)
                    {
                        //logger.LogError(b.ToString());
                        debugErrorChars.Add(b);
                        continue;
                    }

                    if (index <= 0xFF)
                        sb.Append((char)((byte)index));
                    else
                    {
                        //List<byte> list = GetShifts(currentHistogram);
                        List<byte> list = new();
                        for (int i = 0x1FE; i >= 0x80; i--)
                        {
                            if (currentHistogram[i] < 0x80)
                                list.Add((byte)i);
                        }

                        foreach (byte shift in list)
                        {
                            if ((index - (currentHistogram[shift] << 7)) < 0x80)
                            {
                                sb.Append((char)shift);
                                sb.Append((char)((byte)(index - (currentHistogram[shift] << 7) + 0x80)));
                                break;
                            }
                        }
                        debugErrorChars.Add(b);
                    }
                }
            }
            if (!ids.Contains(strInfo.Key))
            {
                ids.Add(strInfo.Key);
                strings.Add(strInfo.Key, sb.ToString());
            }
            else
                strings[strInfo.Key] = sb.ToString();
        }

        _ = debugErrorChars;

        ids.Sort();
        offsets.Clear();

        byte[] stringData = null;
        using (NativeWriter writer = new NativeWriter(new MemoryStream()))
        {
            foreach (var id in ids)
            {
                offsets.Add((uint)writer.Position);

                foreach (char c in strings[id].ToCharArray())
                {
                    writer.Write((byte)c);
                }
                writer.Write((byte)0x00);
            }
            stringData = writer.ToByteArray();
        }

        using (NativeWriter writer = new(new FileStream(StringChunkPath, FileMode.Create)))
        {
            writer.Write(0x00039000);
            writer.Write(0xdeadbeef); // size
            writer.Write(ids.Count);
            writer.Write(0x8C); // dataOffset
            writer.Write((uint)(0x8C + (8 * ids.Count))); // stringOffset
            writer.WriteNullTerminatedString(tag);
            while (writer.Position < 0x8C + 8)
                writer.Write((byte)0x00);

            for (int i = 0; i < ids.Count; i++)
            {
                writer.Write(ids[i]);
                writer.Write(offsets[i]);
            }

            writer.Write(stringData);

            logger.LogInformation("String Chunk size is: " + writer.Position.ToString());

            uint size = (uint)(writer.Position - 8);
            writer.Position = 4;
            writer.Write(size);
        }
    }
}
