namespace BIS.P3D.ODOL
{
    /// <summary>
    /// Which game's field layout to use for a binarized p3d.
    /// DayZ writes ODOL versions 53-55 with a layout that differs from the Arma layout
    /// of the same version numbers; the reader cannot tell the two apart from the
    /// version alone, so the caller says which one it has.
    /// </summary>
    public enum OdolLayout
    {
        /// <summary>The layout this library has always read (Arma).</summary>
        Default,

        /// <summary>DayZ Standalone, ODOL 53-55. Measured on real DayZ files; see ModelInfo, ODOL.</summary>
        DayZ,
    }
}
