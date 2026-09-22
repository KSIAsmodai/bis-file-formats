using BIS.Core.Math;
using BIS.Core.Streams;

namespace BIS.WRP
{
    public class StaticEntityInfo
    {
        public string ClassName { get; }
        public string ShapeName { get; }
        public Vector3P Position { get; }
        public ObjectId ObjectId { get; }
        public uint ExtraV29 { get; }

        public StaticEntityInfo(BinaryReaderEx input)
        {
            ClassName = input.ReadAsciiz();
            ShapeName = input.ReadAsciiz();
            Position = new Vector3P(input);
            ObjectId = input.ReadInt32();

            // DayZ OPRW v29 writes an extra 4-byte field right after ObjectId that
            // no upstream Arma reader (this port included) accounts for -- same
            // engine-wide pattern as RoadLink's undocumented trailing field (see
            // RoadLink.cs and repos/REFERENCE/DayZ-Modding-Knowledge-Pack/knowledge/
            // vault-notes/dayz-wrp-roadgraph-extraction.md:51-52, "extra_v29 (4B)").
            // VERIFIED empirically 2026-09-22 against ChernarusPlus.wrp (v29, sha1
            // be544b6b18b43756a20d8b0c509e320abe801043): without skipping this
            // field, record 1 of 18452 already desyncs (ClassName decodes as an
            // empty string at file offset 18,620,776, ShapeName as
            // "H\x02\x80Land_misc_feedshack" -- garbage), and the array as a whole
            // undercounts the true entity data by ~1MB (parser stops at offset
            // 19,758,761 while well-formed "Land_wall_gate_ind2a_r..." entity text
            // continues right there). With this field read and skipped, all 18452
            // records decode as clean ASCII with plausible in-map coordinates (e.g.
            // record 0 "Land_misc_feedshack" at x=80.3 z=4422.2; record 18451
            // "Land_boat_small2" at x=15335.0 z=13842.1, both inside the known
            // 15360m Chernarus extent) and the array ends at file offset
            // 20,313,744, where the next byte (0x01) matches the QuadTree
            // GridBlock "grid present" flag documented in the vendored BI wiki spec
            // (repos/REFERENCE/BI-Wiki-Vendored/general/Wrp_File_Format_-_OPRWv17_to_24.md,
            // "GridBlock" section, "when the grid is present, the leading flag =
            // 0x01") -- i.e. ObjectOffsets (the QuadTree<int> read immediately
            // after EntityInfos in OPRW.cs) now starts on genuinely QuadTree-shaped
            // data instead of mid-entity-text.
            if (input.Version >= 29)
                ExtraV29 = input.ReadUInt32();
        }
    }
}
