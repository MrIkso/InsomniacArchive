using InsomniacArchive.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsomniacArchive.FileTypes
{
    public abstract class AssetFile : DatFileBase
    {
        private uint AssetId { get; set; }

        private bool Compressed { get; set; }
        private bool IsGSTFile { get; set; }
        private byte[] GSTFileHeader { get; set; }
        protected override void CompressData(MemoryStream input, Stream output)
        {
            ExtendedBinaryWriter bw = new(output);

            if (IsGSTFile)
            {
                bw.Write(GSTFileHeader, 0, GSTFileHeader.Length);
                bw.Seek(0x18, SeekOrigin.Begin);
                bw.Write(input.Length);
                bw.Seek(0x1C, SeekOrigin.Begin);
                bw.Write(GSTFileHeader, 0x1C, 4);

                bw.Pad(0x20 - (int)bw.BaseStream.Position);
            }
            else
            {
                bw.Write(AssetId);
                bw.Write(input.Length);
                bw.Pad(0x24 - (int)bw.BaseStream.Position);
            }

            if (!Compressed)
            {
                input.CopyTo(output);
                return;
            }

            byte[] data = input.ToArray();
            byte[] compressed = new byte[data.Length];

            int compSize = K4os.Compression.LZ4.LZ4Codec.Encode(data, compressed);

            bw.Write(compressed, 0, compSize);
        }

        protected override void DecompressData(Stream input, MemoryStream output)
        {
            int compSize;
            int rawsize;

            BinaryReader br = new(input);
            int datStartOffset = 0x24;
            AssetId = br.ReadUInt32();
            if (AssetId == 0x00475453) // GTS Header
            {
                IsGSTFile = true;
                datStartOffset = 0x20;
                br.BaseStream.Position = 0x0;
                GSTFileHeader = br.ReadBytes(0x20);
            }

            compSize = (int)input.Length - datStartOffset;
            rawsize = br.ReadInt32();

            Compressed = false; // compSize != rawsize;

            input.Position = datStartOffset;

            if (!Compressed)
            {
                input.CopyTo(output);
                return;
            }

            byte[] compressedData = new byte[input.Length - datStartOffset];
            input.Read(compressedData);

            byte[] decompressedData = new byte[rawsize];
            K4os.Compression.LZ4.LZ4Codec.Decode(compressedData, decompressedData);

            output.Write(decompressedData);
        }
    }
}
