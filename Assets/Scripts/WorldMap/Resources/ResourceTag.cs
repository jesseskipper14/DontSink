using System;

[Flags]
public enum ResourceTag
{
    None      = 0,
    Food      = 1 << 0,
    Building  = 1 << 1,
    Fuel      = 1 << 2,
    Medical   = 1 << 3,
    Luxury    = 1 << 4,
    Tools     = 1 << 5,
    Salvage   = 1 << 6
}
