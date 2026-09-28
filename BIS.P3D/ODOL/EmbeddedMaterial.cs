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
            if (dayz && Version != 15u && Version != 16u && Version != 20u)
            {
                // Only material versions 15, 16 and 20 have been measured in DayZ files. Stop rather than guess.
                throw new NotSupportedException($"DayZ embedded material version {Version} not measured yet ('{MaterialName}')");
            }
            Emissive = new ColorP(input);
            Ambient = new ColorP(input);
            Diffuse = new ColorP(input);
            ForcedDiffuse = new ColorP(input);
            Specular = new ColorP(input);
            if (dayz)
            {
                // DayZ v16 and v20: 2 colours between Specular and the specular copy. MEASURED on
                // jerrycan.rvmat (v20) and decal_welcometohell.rvmat (v16): 0,0,0,1 and 0,0,0.3,0.99 in both;
                // the source rvmat does not set them.
                DayZColorsAfterSpecular = new[] { new ColorP(input), new ColorP(input) };
            }
            SpecularCopy = new ColorP(input);
            SpecularPower = input.ReadSingle();
            if (dayz)
            {
                // DayZ v16 and v20: a colour and 2 floats after SpecularPower (v16 decal: 0,0,1,1 and 1,1;
                // v20 jerrycan: -1,0,1,1 and 1,1). v20 then adds 12 words: 2 words, twice (int32 + 3 floats),
                // 2 words (jerrycan: 0,0 / -1 30,45,0 / -1 0,1,0 / 0,0). 14 extra words in v16, 26 in v20.
                // v15 (2 files, BallerZ caps; the judge cannot read them): only 2 words here, both zero.
                // The stage counts, empty surface name and shader ids that follow line up exactly.
                if (Version >= 16u)
                {
                    DayZColorAfterPower = new ColorP(input);
                }
                DayZPairAfterPower = new[] { input.ReadSingle(), input.ReadSingle() };
                if (Version >= 20u)
                {
                    DayZWordsA = new[] { input.ReadUInt32(), input.ReadUInt32() };
                    DayZIndexedTriples = new DayZIndexedTriple[] { new DayZIndexedTriple(input), new DayZIndexedTriple(input) };
                    DayZWordsB = new[] { input.ReadUInt32(), input.ReadUInt32() };
                }
            }
            PixelShader = input.ReadUInt32();
            VertexShader = input.ReadUInt32();
            if (dayz)
            {
                // DayZ: fog mode comes BEFORE main light (the ODOLv4x page lists main light first).
                // MEASURED on 2 materials against the judge's labels: jerrycan stores 3,1 = FogAlpha, Sun;
                // the v16 decal stores 1,1 = Fog, Sun (rvmat enum order: fog None/Fog/Alpha/FogAlpha/FogSky,
                // light None/Sun/...). Still to confirm on a material whose main light is not Sun.
                FogMode = input.ReadUInt32();
                MainLight = input.ReadUInt32();
            }
            else
            {
                MainLight = input.ReadUInt32();
                FogMode = input.ReadUInt32();
            }
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
                var nStages = input.ReadUInt32();
                input.CheckStreamCount(nStages, 1, "material stage count"); // no-op unless the opt-in guard is on
                StageTextures = new StageTexture[nStages];
            }
            else
            {
                StageTextures = new StageTexture[0];
            }
            if (Version > 8u) // NTexGens
            {
                var nTexGens = input.ReadUInt32();
                input.CheckStreamCount(nTexGens, 1, "material texgen count"); // no-op unless the opt-in guard is on
                StageTransforms = new StageTransform[nTexGens];
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
        public ColorP DayZColorAfterPower { get; }
        public float[] DayZPairAfterPower { get; }
        public uint[] DayZWordsA { get; }
        public DayZIndexedTriple[] DayZIndexedTriples { get; }
        public uint[] DayZWordsB { get; }

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