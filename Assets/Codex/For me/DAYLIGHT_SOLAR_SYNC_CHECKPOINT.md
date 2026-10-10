# Daylight brightness controls

2026-10-09 — revised after user feedback; ready for scene playtest.

The earlier solar-height-derived implementation made scene brightness difficult to tune and has been removed. GlobalBrightnessManager is the brightness authority. Its Inspector now exposes ordinary 24-hour clock controls:

| Control | Default | Meaning |
| --- | --- | --- |
| Dawn Start Hour | 4 | Begin fading up from Min Brightness |
| Full Daylight Hour | 6 | Reach Max Brightness |
| Dusk Start Hour | 18 | Begin fading down |
| Full Night Hour | 20 | Reach Min Brightness |

Min/Max Brightness still set actual ambient/sky levels. The optional Use Legacy Brightness Curve restores the existing normalized-hour curve when explicitly selected. Default behavior uses the four clock controls with smooth transitions.

ServiceRoot binds the existing GlobalBrightnessManager to CelestialBodyManager. Sun intensity/corona and ambient/sky brightness share the clock-driven daylight factor. Sun geometry no longer controls scene brightness. Standalone celestial managers retain their old sun-intensity curve fallback. Authored sun and moon paths, sunrise/sunset tint schedule, star fades, calendar, saved time, depth attenuation and scenes are unchanged.

Validation samples actual ambient and sun lighting at ten hours, tests midnight wrapping, full daylight at 6 AM, explicit timing changes and independence from sun height. Latest isolated results: Library/CodexThrowableChecks/daylight-results.txt. Scene playtest remains necessary.
