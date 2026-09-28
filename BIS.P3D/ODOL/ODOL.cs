using System;
using System.Diagnostics;
using System.Linq;
using BIS.Core.Streams;

namespace BIS.P3D.ODOL
{
    public class ODOL : IReadWriteObject
    {
        public int Version { get; private set; }
        public string Prefix { get; private set; }
        public ModelInfo ModelInfo { get; private set; }
        public uint AppID { get; private set; }
        public string MuzzleFlash { get; private set; }
        public byte[] Extra { get; private set; }
        public LOD[] Lods { get; set; }
        public Animations Animations { get; private set; }

        /// <summary>Field layout to read with. Set before reading; Default keeps the original behaviour.</summary>
        public OdolLayout Layout { get; set; } = OdolLayout.Default;

        public void Read(BinaryReaderEx input)
        {
            if (input.ReadAscii(4) != "ODOL")
                throw new FormatException("ODOL signature expected");

            ReadContent(input);
        }

        public void Write(BinaryWriterEx output)
        {
            if (Layout == OdolLayout.DayZ)
            {
                // The DayZ layout is read-only here: the writers do not emit every DayZ field.
                throw new NotSupportedException("Writing the DayZ ODOL layout is not supported");
            }
            output.WriteAscii("ODOL", 4);
            WriteContent(output);
        }

        internal float[] ReadHeaderOnly(BinaryReaderEx input)
        {
            Version = input.ReadInt32();
            input.Version = Version;

            if (Version >= 44)
            {
                input.UseLZOCompression = true;
            }
            if (Version >= 64)
            {
                input.UseCompressionFlag = true;
            }

            if (Version >= 75)
            {
                var enc1 = input.ReadUInt32();
                var enc2 = input.ReadUInt32();
                if (enc1 != 0 || enc2 != 0)
                {
                    throw new Exception("This P3D is encrypted. It cannot be read.");
                }
            }

            if (Version >= 59)
            {
                AppID = input.ReadUInt32();
            }
            if (Version >= 58)
            {
                MuzzleFlash = input.ReadAsciiz();
            }

            var resolutions = input.ReadFloatArray();
            var noOfLods = resolutions.Length;

            Lods = new LOD[noOfLods];

            ModelInfo = new ModelInfo(input, Version, noOfLods, Layout == OdolLayout.DayZ);

            return resolutions;
        }

        internal void ReadContent(BinaryReaderEx input)
        {
            var resolutions = ReadHeaderOnly(input);

            var noOfLods = resolutions.Length;

            Trace.TraceInformation($"ODOL after ModelInfo: {input.Position}");
            if (Version >= 30u)
            {
                var hasAnims = input.ReadBoolean();
                Trace.TraceInformation($"ODOL hasAnims={hasAnims}");
                if (hasAnims)
                {
                    Animations = new Animations(input, Version, Layout == OdolLayout.DayZ);
                    Trace.TraceInformation($"ODOL after Animations: {input.Position} ({Animations.AnimationClasses.Length} classes, {Animations.Bones2Anims.Length} lod entries)");
                }
            }
            var lodStartAdresses = input.ReadArrayBase(r => r.ReadUInt32(), noOfLods);
            var lodEndAdresses = input.ReadArrayBase(r => r.ReadUInt32(), noOfLods);
            var permanent = input.ReadArrayBase(r => r.ReadBoolean(), noOfLods);
            var loadableLodInfo = new LoadableLodInfo[noOfLods];
            for (int m = 0; m < noOfLods; m++)
            {
                if (!permanent[m])
                {
                    loadableLodInfo[m] = new LoadableLodInfo(input, Version);
                }
            }
            for (int m = 0; m < noOfLods; m++)
            {
                input.Position = lodStartAdresses[m];
                Lods[m] = new LOD(input, resolutions[m], loadableLodInfo[m], Version, Layout == OdolLayout.DayZ);
                if (input.Position != lodEndAdresses[m])
                {
                    Trace.TraceWarning($"LOD {resolutions[m]} end mismatch. Expected={lodEndAdresses[m]} Actual={input.Position}");
                }
            }
            if (lodEndAdresses.Length > 0)
                input.Position = lodEndAdresses.Max();
            var remaining = input.BaseStream.Length - input.Position;
            Extra = remaining > 0 ? input.ReadBytes((int)remaining) : Array.Empty<byte>();
        }

        internal void WriteContent(BinaryWriterEx output)
        {
            output.Write(Version);

            if (Version >= 44)
            {
                output.UseLZOCompression = true;
            }
            if (Version >= 64)
            {
                output.UseCompressionFlag = true;
            }

            if (Version >= 59)
            {
                output.Write(AppID);
            }
            if (Version >= 58)
            {
                output.WriteAsciiz(MuzzleFlash);
            }

            output.WriteArray(Lods.Select(l => l.Resolution).ToArray());

            var noOfLods = Lods.Length;

            ModelInfo.Write(output, Version, noOfLods);

            if (Version >= 30u)
            {
                if (Animations != null)
                {
                    output.Write(true);
                    Animations.Write(output, Version);
                }
                else
                {
                    output.Write(false);
                }
            }
            var lodStartAdresses = new uint[noOfLods];
            var lodEndAdresses = new uint[noOfLods];
            var permanent = Lods.Select(l => l.LoadableLodInfo == null).ToArray();
            var adressesPositions = output.Position;
            output.WriteArrayBase(lodStartAdresses, (o, v) => o.Write(v));
            output.WriteArrayBase(lodEndAdresses, (o, v) => o.Write(v));
            output.WriteArrayBase(permanent, (o, v) => o.Write(v));
            foreach (var lod in Lods)
            {
                if (lod.LoadableLodInfo != null)
                {
                    lod.LoadableLodInfo.Write(output, Version);
                }
            }
            foreach (var lod in Lods.OrderByDescending(l => l.Resolution))
            {
                var m = Array.IndexOf(Lods, lod);
                lodStartAdresses[m] = (uint)output.Position;
                Lods[m].Write(output, Version);
                lodEndAdresses[m] = (uint)output.Position;
            }
            output.Write(Extra);

            output.Position = adressesPositions;
            output.WriteArrayBase(lodStartAdresses, (o, v) => o.Write(v));
            output.WriteArrayBase(lodEndAdresses, (o, v) => o.Write(v));
        }
    }
}
