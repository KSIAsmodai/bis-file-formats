using BIS.Core.Math;
using BIS.Core.Streams;
using System;
using System.Collections.Generic;
using System.Text;

namespace BIS.WRP
{
    //public class RoadList
    //{
    //    private int nRoadLinks;
    //    private RoadLink[] roadLinks;

    //    public void Read(BinaryReaderEx input, int version)
    //    {
    //        nRoadLinks = input.ReadInt32();
    //        roadLinks = new RoadLink[nRoadLinks];
    //        for (int i = 0; i < nRoadLinks; i++)
    //        {
    //            roadLinks[i] = new RoadLink(input);
    //        }
    //    }
    //}

    public class RoadLink
    {
        public short ConnectionCount { get; }
        public Vector3P[] Positions { get; }
        public byte[] ConnectionTypes { get; }
        public int ObjectID { get; }
        public uint ExtraV29 { get; }
        public string P3dPath { get; }
        public Matrix4P ToWorld { get; }

        public RoadLink(BinaryReaderEx input)
        {
            ConnectionCount = input.ReadInt16();
            Positions = new Vector3P[ConnectionCount];
            for (int i = 0; i < ConnectionCount; i++)
                Positions[i] = new Vector3P(input);

            if (input.Version >= 24)
            {
                ConnectionTypes = new byte[ConnectionCount];
                for (int i = 0; i < ConnectionCount; i++)
                    ConnectionTypes[i] = input.ReadByte();
            }

            ObjectID = input.ReadInt32();

            // DayZ OPRW v29 extra field, same position/shape as StaticEntityInfo's
            // (see StaticEntityInfo.cs) and matching the in-house vault note's
            // documented v29 layout: "RoadLink = ConnectionCount + Positions[] +
            // ConnectionTypes[] + ObjectID + extra_v29 (4 bytes) + asciiz P3dPath +
            // Matrix4P (48B)" (repos/REFERENCE/DayZ-Modding-Knowledge-Pack/
            // knowledge/vault-notes/dayz-wrp-roadgraph-extraction.md:51-52). Not yet
            // independently hex-verified against ChernarusPlus.wrp this session
            // (EntityInfos was; RoadNet parsing was not reached -- see
            // WRP_PARSER.md 2026-09-22 session note) -- applied on the strength of
            // the vault note's explicit spec plus the identical pattern just
            // confirmed in StaticEntityInfo. Re-verify with hex evidence once
            // RoadNet is reached; revert this one field if the byte counts don't
            // line up.
            if (input.Version >= 29)
                ExtraV29 = input.ReadUInt32();

            if (input.Version >= 16)
            {
                P3dPath = input.ReadAsciiz();
                ToWorld = new Matrix4P(input);
            }
        }
    }
}
