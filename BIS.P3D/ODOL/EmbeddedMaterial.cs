using System;
using BIS.Core;
using BIS.Core.Streams;

namespace BIS.P3D.ODOL
{
    /// <summary>DayZ material extra: an int32 then three floats (measured: -1 then 30,45,0 and -1 then 0,1,0).</summary>
    public class DayZIndexedTriple
    {
        internal DayZIndexedTriple(BinaryReaderEx input)
        {
            Index = input.ReadInt32();
            Values = new[] { input.ReadSingle(), input.ReadSingle(), input.ReadSingle() };
        }

        public int Index { get; }
        public float[] Values { get; }
    }

    public class EmbeddedMaterial
    {
        public EmbeddedMaterial(BinaryReaderEx input, bool dayz = false)
        {
            MaterialName = input.ReadAsciiz();
            Version = input.ReadUInt32();
            if (dayz && Version != 20u)
            {
                // Only material version 20 has been measured in DayZ files. Stop rather than guess.
                throw new NotSupportedException($"DayZ embedded material version {Version} not measured yet ('{MaterialName}')");
            }
            Emissive = new ColorP(input);
            Ambient = new ColorP(input);
            Diffuse = new ColorP(input);
            ForcedDiffuse = new ColorP(input);
            Specular = new ColorP(input);
            if (dayz)
            {
                // DayZ v20: 2 colours between Specular and the specular copy. MEASURED on
                // jerrycan.rvmat (values 0,0,0,1 and 0,0,0.3,0.99; the source rvmat does not set them).
                DayZColorsAfterSpecular = new[] { new ColorP(input), new ColorP(input) };
            }
            SpecularCopy = new ColorP(input);
            SpecularPower = input.ReadSingle();
            if (dayz)
            {
                // DayZ v20: 18 more words before PixelShader, read as they measured: two colours,
                // then twice (int32 + 3 floats), then 2 words. 26 extra words in all with the two above.
                DayZColorsAfterPower = new[] { new ColorP(input), new ColorP(input) };
                DayZIndexedTriples = new DayZIndexedTriple[] { new DayZIndexedTriple(input), new DayZIndexedTriple(input) };
                DayZTailWords = new[] { input.ReadUInt32(), input.ReadUInt32() };
            }
            PixelShader = input.ReadUInt32();
            VertexShader = input.ReadUInt32();
            MainLight = input.ReadUInt32();
            FogMode = input.ReadUInt32();
            if (Version == 3u)
            {
                Unused3 = input.ReadBoolean();
            }
            if (Version >= 6u)
            {
                SurfaceFile = input.ReadAsciiz();
            }
            if (Version >= 4u)
            {
                NRenderFlags = input.ReadUInt32();
                RenderFlags = input.ReadUInt32();
            }
            if (Version > 6u) // NStages
            {
                StageTextures = new StageTexture[input.ReadUInt32()];
            }
            else
            {
                StageTextures = new StageTexture[0];
            }
            if (Version > 8u) // NTexGens
            {
                StageTransforms = new StageTransform[input.ReadUInt32()];
            }
            else
            {
                StageTransforms = new StageTransform[StageTextures.Length];
            }

            if (Version < 8u)
            {
                for (int i = 0; i < StageTextures.Length; i++)
                {
                    StageTransforms[i] = new StageTransform(input);
                    StageTextures[i] = new StageTexture(input, Version);
                }
            }
            else
            {
                for (int i = 0; i < StageTextures.Length; i++)
                {
                    StageTextures[i] = new StageTexture(input, Version);
                }
                for (int i = 0; i < StageTransforms.Length; i++)
                {
                    StageTransforms[i] = new StageTransform(input);
                }
            }
            if (Version >= 10u)
            {
                StageTI = new StageTexture(input, Version);
            }
        }

        public string MaterialName { get; set; }
        public uint Version { get; }
        public ColorP Emissive { get; }
        public ColorP Ambient { get; }
        public ColorP Diffuse { get; }
        public ColorP ForcedDiffuse { get; }
        public ColorP Specular { get; }
        public ColorP SpecularCopy { get; }
        public float SpecularPower { get; }
        public uint PixelShader { get; }
        public uint VertexShader { get; }
        public uint MainLight { get; }
        public uint FogMode { get; }
        public bool Unused3 { get; }
        public string SurfaceFile { get; set; }
        public uint NRenderFlags { get; }
        public uint RenderFlags { get; }
        public StageTexture[] StageTextures { get; }
        public StageTransform[] StageTransforms { get; }
        public StageTexture StageTI { get; }
        public ColorP[] DayZColorsAfterSpecular { get; }
        public ColorP[] DayZColorsAfterPower { get; }
        public DayZIndexedTriple[] DayZIndexedTriples { get; }
        public uint[] DayZTailWords { get; }

        public void Write(BinaryWriterEx output)
        {
            output.WriteAsciiz(MaterialName);
            output.Write(Version);
            Emissive.Write(output);
            Ambient.Write(output);
            Diffuse.Write(output);
            ForcedDiffuse.Write(output);
            Specular.Write(output);
            SpecularCopy.Write(output);
            output.Write(SpecularPower);
            output.Write(PixelShader);
            output.Write(VertexShader);
            output.Write(MainLight);
            output.Write(FogMode);
            if (Version == 3u)
            {
                output.Write(Unused3);
            }
            if (Version >= 6u)
            {
                output.WriteAsciiz(SurfaceFile);
            }
            if (Version >= 4u)
            {
                output.Write(NRenderFlags);
                output.Write(RenderFlags);
            }
            if (Version > 6u) // NStages
            {
                output.Write((uint)StageTextures.Length);
            }

            if (Version > 8u) // NTexGens
            {
                output.Write((uint)StageTransforms.Length);
            }

            if (Version < 8u)
            {
                for (int i = 0; i < StageTextures.Length; i++)
                {
                    StageTransforms[i].Write(output);
                    StageTextures[i].Write(output, Version);
                }
            }
            else
            {
                for (int i = 0; i < StageTextures.Length; i++)
                {
                    StageTextures[i].Write(output, Version);
                }
                for (int i = 0; i < StageTransforms.Length; i++)
                {
                    StageTransforms[i].Write(output);
                }
            }
            if (Version >= 10u)
            {
                StageTI.Write(output, Version);
            }
        }

        public override string ToString()
        {
            return MaterialName;
        }
    }
}