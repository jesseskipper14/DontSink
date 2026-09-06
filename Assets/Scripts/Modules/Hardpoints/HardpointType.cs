public enum HardpointType
{
    Engine = 0,
    Pump = 1,
    Utility = 2,
    Weapon = 3,
    Electronics = 4,
    Helm = 5,
    Storage = 6,

    // Appended to preserve the serialized integer values of all existing types.
    Rudder = 7,
    Keel = 8,
    Anchor = 9,

    // Appended to preserve the serialized integer values of all existing types.
    Winch = 10,
    TetherPayload = 11
}
