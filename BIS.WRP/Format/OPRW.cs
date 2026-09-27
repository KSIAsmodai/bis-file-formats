using System;
using System.IO;
using System.Diagnostics;

using BIS.Core;
using BIS.Core.Math;
using BIS.Core.Streams;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;

namespace BIS.WRP
{
    public class OPRW : IReadObject, IWrp
    {
        public int Version { get; private set; }
        public int AppID { get; private set; }
        public byte CompressionFlag { get; private set; }
        public int LandRangeX { get; private set; }
        public int LandRangeY { get; private set; }
        public int TerrainRangeX { get; private set; }
        public int TerrainRangeY { get; private set; }
        public float CellSize { get; private set; }
        public QuadTree<GeographyInfo> Geography { get; private set; }
        public QuadTree<byte> SoundMap { get; private set; }
        public Vector3P[] Mountains { get; private set; } //map peaks
        public QuadTree<ushort> Materials { get; private set; }
        public byte[] Random { get; private set; } //short values
        public byte[] GrassApprox { get; private set; }
        public byte[] PrimTexIndex { get; private set; } //coord to primary texture mapping
        public float[] Elevation { get; private set; }
        public string[] MatNames { get; private set; }
        public string[] Models { get; private set; }
        public StaticEntityInfo[] EntityInfos { get; private set; }
        public QuadTree<int> ObjectOffsets { get; private set; }
        public QuadTree<int> MapObjectOffsets { get; private set; }
        public byte[] Persistent { get; private set; }
        public int MaxObjectId { get; private set; }
        public RoadLink[][] Roadnet { get; private set; }
        public Object[] Objects { get; private set; }
        public byte[] MapInfos { get; private set; }
        IReadOnlyList<ushort> IWrp.MaterialIndex => Materials;
        public int ObjectsCount => Objects.Length;

        public OPRW()
        {

        }

        public OPRW(Stream s)
        {
            var input = new BinaryReaderEx(s);
            Read(input);

            // version 3 - OFP Retail landscape (no streaming, no map)
            // version 5 - OFP XBox landscape beta (streaming, no map)
            // version 6 - landscape (streaming and map)
            // version 7 - landscape, including roads (streaming and map)
            // version 10 - landscape, quad trees 
            // version 11 - landscape, changed geography
            // version 12 - OFP Xbox/FP2 landscape, different grid for textures and terrain
            // version 13 - landscape, subdivision hints included
            // version 14 - landscape, skew object flag added
            // version 15 - landscape, entity list added
            // version 16 - ArmA landscape, roads transform + LODShape added
            // version 17 - major texture pass added
            // version 18 - grass map added, float used as raw data
            // version 19 - water depth geography info change
            // version 20 - grass map contains flat areas around roads
            // version 21 - randomization array removed
            // version 22 - primary texture info added
            // version 23 - LZO compression used for compressed arrays
            // version 24 - extended info for roads (connection types)
            // version 25 - appID of the app or DLC the map belongs
            // version 26 - offset table at the beginning (_VBS3_WRP_OFFSET_TABLE), heightmap compression (_VBS3_HEIGHTMAP_COMPRESSION)
            // version 27 - storing of large static objects R-tree in wrp <-- NOTE: technology not used. Implemented without need of WRP changes!
        }

        //minimal version 10
        public void Read(BinaryReaderEx input)
        {
            var fileSig = input.ReadAscii(4);
            if (fileSig != "OPRW")
            {
                throw new FormatException("OPRW file does not start with correct file signature");
            }

            ReadContent(input);
        }

        internal void ReadContent(BinaryReaderEx input)
        {
            Version = input.ReadInt32();
            input.Version = Version;
            if (Version < 10) throw new NotSupportedException("OPRW file versions below 10 are not supported");

            if (Version >= 23) input.UseLZOCompression = true;

            // DayZ (Enfusion-derived) OPRW v25+ writes a 4-byte sub-magic tag right
            // after Version that no upstream Arma OPRW reader (this port included)
            // accounts for. VERIFIED empirically 2026-09-22, hex dump of
            // ChernarusPlus.wrp (v29, sha1 be544b6b18b43756a20d8b0c509e320abe801043),
            // bytes[8..11] = 30 46 4E 45 = ASCII "0FNE" (read in reverse: "ENF0",
            // i.e. Enforce-engine tag 0) -- matches
            // repos/REFERENCE/DayZ-Modding-Knowledge-Pack/knowledge/vault-notes/
            // dayz-wrp-roadgraph-extraction.md:47 ("Header: OPRW + int32 versión
            // (28/29) + sub-magic 0FNE"). Without skipping it, this field's bytes
            // were silently read AS AppID, and everything after (LandRangeX/Y,
            // TerrainRangeX/Y, CellSize) decoded as garbage -- most dangerously
            // NOT always an immediate crash: it also produced internally-consistent
            // garbage (LandRangeX==LandRangeY, TerrainRangeX==TerrainRangeY) that
            // passed the Debug.Assert below, then hung for 16+ minutes at ~0 CPU
            // inside QuadTree's recursive reader (a garbage `flag` byte drives
            // runaway recursion reading the rest of the 223MB file one node at a
            // time) instead of failing loudly.
            // 2026-09-26: the tag is DETECTED, not assumed from the version number.
            // Arma 3 OPRW v25-27 carries no such tag (upstream read AppID straight
            // after Version), so consuming 4 bytes unconditionally broke every
            // non-DayZ v25+ file. Peek, keep it only if it is the "0FNE" tag.
            string subMagic = null;

            if (Version >= 25)
            {
                long tagPos = input.Position;
                string tag = input.ReadAscii(4);
                if (tag == "0FNE")
                    subMagic = tag;
                else
                    input.Position = tagPos;

                AppID = input.ReadInt32();
                // One extra byte after AppID, read as a "compression flag" (the
                // commented-out "UseCompressionFlag" line above is the origin of
                // the name; its meaning is not established).
                // VERIFIED empirically 2026-09-22 against ChernarusPlus.wrp: with
                // BOTH the sub-magic and this byte skipped, LandRangeX/Y and
                // TerrainRangeX/Y decode as 256/256 and 2048/2048 with
                // CellSize=60.0 -- 256*60=15360m and 2048*7.5=15360m, matching
                // Chernarus's known 15360m map extent exactly.
                // 2026-09-26 CORRECTION: the byte is v29-only, not v25+. Header hex
                // of four DayZ files: v29 ChernarusPlus.wrp and enoch.wrp carry it
                // (byte 16 = 00, LandRangeX follows at 17); v28 Chernarus2035.wrp
                // and Alpen.wrp do NOT (LandRangeX sits at byte 16: 512 and 128).
                // Reading it on v28 decoded LandRange=2x2 and hung the QuadTree
                // reader. Whether the byte follows the version or AppID (both v29
                // samples have AppID=1, both v28 have AppID=0) cannot be told
                // apart from these four files; the version gate is the choice.
                if (Version >= 29)
                    CompressionFlag = input.ReadByte();
            }

            if (Version >= 12)
            {
                LandRangeX = input.ReadInt32();
                LandRangeY = input.ReadInt32(); //same as x?
                TerrainRangeX = input.ReadInt32();
                TerrainRangeY = input.ReadInt32(); //same as x?
                CellSize = input.ReadSingle();
                Debug.Assert(LandRangeX == LandRangeY && TerrainRangeX == TerrainRangeY);

                // 2026-09-26: fail loudly on a desynced header. A wrong header
                // layout used to produce garbage grid sizes that sent the QuadTree
                // and compressed-block readers into multi-minute hangs instead of
                // an error (Alpen.wrp v28: >10 min at ~0 CPU before the v29-only
                // gate above). Only rejects values no real terrain can have.
                bool rangesOk = LandRangeX > 0 && LandRangeY > 0 && TerrainRangeX > 0 && TerrainRangeY > 0
                    && LandRangeX <= 65536 && LandRangeY <= 65536 && TerrainRangeX <= 65536 && TerrainRangeY <= 65536
                    && TerrainRangeX >= LandRangeX && TerrainRangeY >= LandRangeY
                    && TerrainRangeX % LandRangeX == 0 && TerrainRangeY % LandRangeY == 0;
                bool cellOk = float.IsFinite(CellSize) && CellSize > 0 && CellSize < 10000;
                if (!rangesOk || !cellOk)
                    throw new FormatException($"OPRW v{Version} header desync: LandRange={LandRangeX}x{LandRangeY} TerrainRange={TerrainRangeX}x{TerrainRangeY} CellSize={CellSize} (tag={subMagic ?? "none"}, AppID={AppID}, header ends at byte {input.Position})");
            }

            Console.Error.WriteLine($"[DBG] subMagic={subMagic} AppID={AppID} CompressionFlag={CompressionFlag} LandRange={LandRangeX}x{LandRangeY} TerrainRange={TerrainRangeX}x{TerrainRangeY} CellSize={CellSize} pos={input.Position}");

            Geography = new QuadTree<GeographyInfo>(LandRangeX, LandRangeY, input, (src, off) => BitConverter.ToInt16(src, off), 2);
            Console.Error.WriteLine($"[DBG] after Geography pos={input.Position}");
            //if(version<19) transformOldWaterInformation

            var soundMapCoef = 1; //ToDo: this is read from config
            SoundMap = new QuadTree<byte>(LandRangeX * soundMapCoef, LandRangeX * soundMapCoef, input, (src, off) => src[off], 1); //both landRangeX are correct. no mistake
            Console.Error.WriteLine($"[DBG] after SoundMap pos={input.Position}");

            Mountains = input.ReadArray(inp => new Vector3P(inp));
            Console.Error.WriteLine($"[DBG] after Mountains n={Mountains.Length} pos={input.Position}");

            Materials = new QuadTree<ushort>(LandRangeX, LandRangeY, input, (src, off) => BitConverter.ToUInt16(src, off), 2);
            Console.Error.WriteLine($"[DBG] after Materials pos={input.Position}");

            if (Version < 21)
                Random = input.ReadCompressed((uint)(LandRangeX * LandRangeY * 2)); //short values

            if (Version >= 18)
                GrassApprox = input.ReadCompressed((uint)(TerrainRangeX * TerrainRangeY)); //byte values
            Console.Error.WriteLine($"[DBG] after GrassApprox pos={input.Position}");

            if (Version >= 22)
                PrimTexIndex = input.ReadCompressed((uint)(TerrainRangeX * TerrainRangeY)); //signed byte values?
            Console.Error.WriteLine($"[DBG] after PrimTexIndex pos={input.Position}");

            Elevation = input.ReadCompressedFloats(TerrainRangeX * TerrainRangeY);
            Console.Error.WriteLine($"[DBG] after Elevation pos={input.Position}");

            var nMaterials = input.ReadInt32();
            Console.Error.WriteLine($"[DBG] nMaterials={nMaterials} pos={input.Position}");
            MatNames = new string[nMaterials];
            var major = new byte[nMaterials];
            for (int i = 0; i < nMaterials; i++)
            {
                MatNames[i] = input.ReadAsciiz();
                major[i] = input.ReadByte();
            }
            Console.Error.WriteLine($"[DBG] after MatNames pos={input.Position}");

            Models = input.ReadStringArray();
            Console.Error.WriteLine($"[DBG] after Models n={Models.Length} first={(Models.Length > 0 ? Models[0] : "")} pos={input.Position}");

            if (Version >= 15)
            {
                EntityInfos = input.ReadArray(inp => new StaticEntityInfo(inp));
            }
            Console.Error.WriteLine($"[DBG] after EntityInfos n={EntityInfos?.Length} pos={input.Position}");

            ObjectOffsets = new QuadTree<int>(LandRangeX, LandRangeY, input, (src, off) => BitConverter.ToInt32(src, off), 4);
            var sizeOfObjects = input.ReadInt32();
            MapObjectOffsets = new QuadTree<int>(LandRangeX, LandRangeY, input, (src, off) => BitConverter.ToInt32(src, off), 4);
            var sizeOfMapinfo = input.ReadInt32();

            Persistent = input.ReadCompressed((uint)(LandRangeX * LandRangeY));
            var subDivHints = input.ReadCompressed((uint)(TerrainRangeX * TerrainRangeY));

            MaxObjectId = input.ReadInt32();
            var roadnetSize = input.ReadInt32();

            Roadnet = new RoadLink[LandRangeX * LandRangeY][];
            var pos = input.Position;
            for (int i = 0; i < LandRangeX * LandRangeY; i++)
            {
                Roadnet[i] = input.ReadArray(inp => new RoadLink(inp));
            }
            var read = input.Position - pos;

            var nObjects = sizeOfObjects / 60;
            Objects = new Object[nObjects];

            for (int i = 0; i < nObjects; i++)
            {
                Objects[i] = new Object(input);
            }

            MapInfos = input.ReadBytes((int)(input.BaseStream.Length - input.BaseStream.Position));
        }

        public EditableWrp ToEditableWrp()
        {
            return new EditableWrp()
            {
                CellSize = CellSize,
                Elevation = Elevation,
                LandRangeX = LandRangeX,
                LandRangeY = LandRangeY,
                MatNames = MatNames,
                TerrainRangeX = TerrainRangeX,
                TerrainRangeY = TerrainRangeY,
                Objects = Objects.OrderBy(o => o.ObjectID).Select(o => new EditableWrpObject()
                {
                    Model = Models[o.ModelIndex],
                    ObjectID = o.ObjectID,
                    Transform = o.Transform
                }).Concat(new[] { EditableWrpObject.Dummy }).ToList(),
                MaterialIndex = Materials.ToArray()
            };
        }
    }
}