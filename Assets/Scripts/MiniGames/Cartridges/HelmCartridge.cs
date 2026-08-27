using System;
using System.Globalization;
using UnityEngine;
using MiniGames;

/// <summary>
/// Helm control-system cartridge.
///
/// Step 3 adds a deliberately clumsy command console for discrete control
/// orders while preserving BoatPilotingSimulation as control authority.
/// </summary>
public sealed class HelmCartridge :
    IMiniGameCartridge,
    IOverlayRenderable,
    IBoatControlAuthorityDisplayNameProvider
{
    private const string CommandControlName =
        "HelmCommandField";

    private readonly Hardpoint _hardpoint;
    private readonly HelmReadoutSource _readout;
    private readonly BoatPilotingSimulation _simulation;

    private MiniGameContext _ctx;
    private bool _requestedClose;

    private string _commandText = string.Empty;
    private string _consoleOutput =
        "TYPE HELP FOR COMMANDS.";
    private bool _focusCommandField;

    public string ControlAuthorityDisplayName =>
        "HELM CONSOLE";

    public HelmCartridge(
        Hardpoint hardpoint,
        HelmReadoutSource readout,
        BoatPilotingSimulation simulation)
    {
        _hardpoint = hardpoint;
        _readout = readout;
        _simulation = simulation;
    }

    public void Begin(
        MiniGameContext context)
    {
        _ctx =
            context ??
            new MiniGameContext();

        _requestedClose = false;
        _commandText = string.Empty;
        _consoleOutput =
            "TYPE HELP FOR COMMANDS.";
        _focusCommandField = true;
    }

    public MiniGameResult Tick(
        float dt,
        MiniGameInput input)
    {
        if (_requestedClose)
            return Cancelled("Closed Helm");

        if (_simulation != null &&
            _simulation.HasControlAuthority(this))
        {
            HelmReadoutSnapshot snapshot =
                _readout != null
                    ? _readout.Capture()
                    : default;

            if (!snapshot.HasHelm ||
                !snapshot.HelmOnline)
            {
                _simulation.ReleaseControl(this);
            }
        }

        return Running();
    }

    public MiniGameResult Cancel()
    {
        return Cancelled(
            "Closed Helm");
    }

    public MiniGameResult Interrupt(
        string reason)
    {
        return Cancelled(
            string.IsNullOrWhiteSpace(reason)
                ? "Helm interrupted"
                : $"Helm interrupted: {reason}");
    }

    public void End()
    {
        if (_simulation != null &&
            _simulation.HasControlAuthority(this))
        {
            _simulation.ReleaseControl(this);
        }

        _ctx = null;
    }

    public void DrawOverlayGUI(
        Rect panel)
    {
        const float pad = 18f;
        const float line = 23f;

        float x =
            panel.x + pad;

        float y =
            panel.y + 12f;

        float width =
            panel.width - pad * 2f;

        GUI.Label(
            new Rect(
                x,
                y,
                width - 44f,
                26f),
            "HELM CONTROL SYSTEM");

        if (GUI.Button(
                new Rect(
                    panel.xMax - 42f,
                    panel.y + 10f,
                    28f,
                    24f),
                "X"))
        {
            _requestedClose = true;
            return;
        }

        y += 34f;

        HelmReadoutSnapshot snapshot =
            _readout != null
                ? _readout.Capture()
                : default;

        DrawStatusLine(
            x,
            ref y,
            width,
            line,
            snapshot);

        DrawLine(
            x,
            ref y,
            width,
            line,
            "CONTROL AUTHORITY:",
            string.IsNullOrWhiteSpace(
                snapshot.ControlAuthority)
                ? "NONE"
                : snapshot.ControlAuthority);

        y += 7f;

        DrawSectionBox(
            new Rect(
                x,
                y,
                width,
                98f),
            "CONTROL SYSTEM");

        float sectionX =
            x + 12f;

        float sectionWidth =
            width - 24f;

        float sectionY =
            y + 25f;

        DrawLineAt(
            sectionX,
            sectionY,
            sectionWidth,
            line,
            "STATIONS:",
            $"{snapshot.ConfiguredStations} LINKED | CAPACITY {snapshot.StationCapacity}");

        sectionY += line;

        if (!snapshot.HasSteering)
        {
            DrawWarningLineAt(
                sectionX,
                sectionY,
                sectionWidth,
                line,
                "RUDDER:",
                "UNAVAILABLE");
        }
        else
        {
            DrawLineAt(
                sectionX,
                sectionY,
                sectionWidth,
                line,
                "RUDDER:",
                snapshot.HasPilotingState
                    ? FormatRudder(snapshot.RudderDegrees)
                    : "UNAVAILABLE");
        }

        sectionY += line;

        DrawLineAt(
            sectionX,
            sectionY,
            sectionWidth,
            line,
            "THROTTLE:",
            snapshot.HasPilotingState
                ? FormatThrottle(snapshot.Throttle)
                : "UNAVAILABLE");

        y += 108f;

        DrawSectionBox(
            new Rect(
                x,
                y,
                width,
                58f),
            "PROPULSION");

        DrawLineAt(
            x + 12f,
            y + 25f,
            width - 24f,
            line,
            "ENGINE:",
            FormatEngineStatus(
                snapshot.InstalledPropulsionSources,
                snapshot.ActivePropulsionSources));

        y += 68f;

        DrawNavigationSection(
            x,
            ref y,
            width,
            line,
            snapshot.Navigation);

        y += 10f;

        DrawCommandConsole(
            x,
            ref y,
            width,
            line);
    }

    private void DrawCommandConsole(
        float x,
        ref float y,
        float width,
        float line)
    {
        const float consoleHeight = 158f;

        DrawSectionBox(
            new Rect(
                x,
                y,
                width,
                consoleHeight),
            "COMMAND CONSOLE");

        GUI.Label(
            new Rect(
                x + 12f,
                y + 25f,
                width - 24f,
                62f),
            _consoleOutput ?? string.Empty);

        float inputY =
            y + 96f;

        GUI.Label(
            new Rect(
                x + 12f,
                inputY,
                18f,
                line),
            ">");

        GUI.SetNextControlName(
            CommandControlName);

        _commandText =
            GUI.TextField(
                new Rect(
                    x + 30f,
                    inputY,
                    width - 112f,
                    line),
                _commandText ?? string.Empty);

        bool execute =
            GUI.Button(
                new Rect(
                    x + width - 74f,
                    inputY,
                    62f,
                    line),
                "EXEC");

        if (_focusCommandField)
        {
            GUI.FocusControl(
                CommandControlName);

            _focusCommandField = false;
        }

        Event current =
            Event.current;

        if (current != null &&
            current.type == EventType.KeyDown &&
            (current.keyCode == KeyCode.Return ||
             current.keyCode == KeyCode.KeypadEnter) &&
            GUI.GetNameOfFocusedControl() ==
                CommandControlName)
        {
            execute = true;
            current.Use();
        }

        if (execute)
            ExecuteCommand();

        GUI.Label(
            new Rect(
                x + 12f,
                y + 126f,
                width - 24f,
                20f),
            "SIGNED RUDDER: PORT - / STBD +   |   THROTTLE: -100..100");

        y +=
            consoleHeight + 10f;
    }

    private void ExecuteCommand()
    {
        string command =
            NormalizeCommand(
                _commandText);

        _commandText =
            string.Empty;

        _focusCommandField =
            true;

        if (string.IsNullOrWhiteSpace(
                command))
        {
            _consoleOutput =
                "NO COMMAND ENTERED.";
            return;
        }

        string upper =
            command.ToUpperInvariant();

        if (ContainsProfanity(upper))
        {
            if (upper.Contains("TURN") ||
                upper.Contains("RUDDER"))
            {
                _consoleOutput =
                    "INVALID COMMAND.\n" +
                    "RUDDER REMAINS DISAPPOINTINGLY WHERE IT WAS.";
            }
            else
            {
                _consoleOutput =
                    "COMMAND NOT RECOGNIZED.\n" +
                    "HOSTILITY HAS BEEN NOTED.";
            }

            return;
        }

        if (upper == "HELP" ||
            upper == "?")
        {
            _consoleOutput =
                "RUDDER <DEG> | RUDDER CENTER\n" +
                "THROTTLE <PCT> | HALF/FULL/STOP [AHEAD/ASTERN]\n" +
                "START ENGINE | KILL ENGINE | STATUS";

            return;
        }

        if (upper == "STATUS")
        {
            ShowConsoleStatus();
            return;
        }

        string core =
            upper.StartsWith("SET ")
                ? upper.Substring(4).Trim()
                : upper;

        if (TryHandleRudderCommand(core))
            return;

        if (TryHandleThrottleCommand(core))
            return;

        if (TryHandleEngineCommand(core))
            return;

        _consoleOutput =
            "COMMAND NOT RECOGNIZED.\n" +
            "TYPE HELP FOR COMMANDS.";
    }

    private void ShowConsoleStatus()
    {
        HelmReadoutSnapshot snapshot =
            _readout != null
                ? _readout.Capture()
                : default;

        string helm =
            !snapshot.HasHelm ||
            !snapshot.HelmOnline
                ? "OFFLINE"
                : snapshot.HelmDamaged
                    ? "DAMAGED"
                    : "ONLINE";

        string authority =
            string.IsNullOrWhiteSpace(
                snapshot.ControlAuthority)
                ? "NONE"
                : snapshot.ControlAuthority;

        string rudder =
            !snapshot.HasSteering
                ? "UNAVAILABLE"
                : snapshot.HasPilotingState
                    ? FormatRudder(
                        snapshot.RudderDegrees)
                    : "UNAVAILABLE";

        string throttle =
            snapshot.HasPilotingState
                ? FormatThrottle(
                    snapshot.Throttle)
                : "UNAVAILABLE";

        _consoleOutput =
            $"HELM {helm} | AUTH {authority}\n" +
            $"RUDDER {rudder} | THROTTLE {throttle}\n" +
            $"ENGINE {snapshot.ActivePropulsionSources}/" +
            $"{snapshot.InstalledPropulsionSources} ACTIVE";
    }

    private bool TryHandleRudderCommand(
        string command)
    {
        if (command == "RUDDER CENTER" ||
            command == "RUD CENTER" ||
            command == "CENTER RUDDER")
        {
            ExecuteRudderOrder(0f);
            return true;
        }

        string remainder = null;

        if (command.StartsWith("RUDDER "))
            remainder = command.Substring(7).Trim();
        else if (command.StartsWith("RUD "))
            remainder = command.Substring(4).Trim();

        if (remainder == null)
            return false;

        float sign = 1f;

        if (remainder.StartsWith("PORT "))
        {
            sign = -1f;
            remainder =
                remainder.Substring(5).Trim();
        }
        else if (remainder.StartsWith("STBD "))
        {
            sign = 1f;
            remainder =
                remainder.Substring(5).Trim();
        }
        else if (remainder.EndsWith(" PORT"))
        {
            sign = -1f;
            remainder =
                remainder.Substring(
                    0,
                    remainder.Length - 5).Trim();
        }
        else if (remainder.EndsWith(" STBD"))
        {
            sign = 1f;
            remainder =
                remainder.Substring(
                    0,
                    remainder.Length - 5).Trim();
        }

        if (!TryParseNumber(
                remainder,
                out float degrees))
        {
            _consoleOutput =
                "INVALID RUDDER ORDER.\n" +
                "EXAMPLE: RUDDER -15";
            return true;
        }

        ExecuteRudderOrder(
            Mathf.Abs(degrees) *
            (degrees < 0f ? -1f : sign));

        return true;
    }

    private void ExecuteRudderOrder(
        float degrees)
    {
        if (!TryAcquireConsoleAuthority(
                out string failure))
        {
            _consoleOutput = failure;
            return;
        }

        if (_simulation == null ||
            !_simulation.HasSteering)
        {
            _consoleOutput =
                "RUDDER UNAVAILABLE.\n" +
                "NO FUNCTIONAL STEERING HARDWARE.";

            return;
        }

        if (!_simulation.TrySetRudderOrder(
                this,
                degrees,
                out float applied))
        {
            _consoleOutput =
                "RUDDER ORDER REJECTED.";
            return;
        }

        bool limited =
            Mathf.Abs(
                applied - degrees) >
            0.01f;

        _consoleOutput =
            "RUDDER ORDER ACCEPTED.\n" +
            $"RUDDER {FormatRudder(applied)}" +
            (limited ? " (LIMITED)" : "");
    }

    private bool TryHandleThrottleCommand(
        string command)
    {
        if (command == "THROTTLE STOP" ||
            command == "STOP THROTTLE" ||
            command == "ALL STOP")
        {
            ExecuteThrottleOrder(0f);
            return true;
        }

        if (command == "THROTTLE HALF" ||
            command == "THROTTLE HALF AHEAD" ||
            command == "HALF AHEAD")
        {
            ExecuteThrottleOrder(0.5f);
            return true;
        }

        if (command == "THROTTLE FULL" ||
            command == "THROTTLE FULL AHEAD" ||
            command == "FULL AHEAD")
        {
            ExecuteThrottleOrder(1f);
            return true;
        }

        if (command == "THROTTLE HALF ASTERN" ||
            command == "HALF ASTERN")
        {
            ExecuteThrottleOrder(-0.5f);
            return true;
        }

        if (command == "THROTTLE FULL ASTERN" ||
            command == "FULL ASTERN")
        {
            ExecuteThrottleOrder(-1f);
            return true;
        }

        if (!command.StartsWith(
                "THROTTLE "))
        {
            return false;
        }

        string remainder =
            command.Substring(9).Trim();

        float sign = 1f;

        if (remainder.EndsWith(" ASTERN"))
        {
            sign = -1f;
            remainder =
                remainder.Substring(
                    0,
                    remainder.Length - 7).Trim();
        }
        else if (remainder.EndsWith(" AHEAD"))
        {
            remainder =
                remainder.Substring(
                    0,
                    remainder.Length - 6).Trim();
        }

        remainder =
            remainder.TrimEnd('%');

        if (!TryParseNumber(
                remainder,
                out float percent))
        {
            _consoleOutput =
                "INVALID THROTTLE ORDER.\n" +
                "EXAMPLE: THROTTLE 50";

            return true;
        }

        if (percent < 0f)
            sign = -1f;

        float requested =
            Mathf.Abs(percent) /
            100f *
            sign;

        ExecuteThrottleOrder(
            requested);

        return true;
    }

    private void ExecuteThrottleOrder(
        float throttle)
    {
        if (!TryAcquireConsoleAuthority(
                out string failure))
        {
            _consoleOutput = failure;
            return;
        }

        if (_simulation == null ||
            !_simulation.TrySetThrottleOrder(
                this,
                throttle,
                out float applied))
        {
            _consoleOutput =
                "THROTTLE ORDER REJECTED.";
            return;
        }

        bool limited =
            Mathf.Abs(
                applied - throttle) >
            0.001f;

        _consoleOutput =
            "THROTTLE ORDER ACCEPTED.\n" +
            $"THROTTLE {FormatThrottle(applied)}" +
            (limited ? " (LIMITED)" : "");
    }

    private bool TryHandleEngineCommand(
        string command)
    {
        bool? running = null;

        if (command == "START ENGINE" ||
            command == "START ENGINES" ||
            command == "ENGINE START" ||
            command == "ENGINE ON")
        {
            running = true;
        }
        else if (command == "KILL ENGINE" ||
                 command == "KILL ENGINES" ||
                 command == "STOP ENGINE" ||
                 command == "STOP ENGINES" ||
                 command == "ENGINE STOP" ||
                 command == "ENGINE OFF")
        {
            running = false;
        }

        if (!running.HasValue)
            return false;

        ExecuteEngineOrder(
            running.Value);

        return true;
    }

    private void ExecuteEngineOrder(
        bool running)
    {
        if (!TryAcquireConsoleAuthority(
                out string failure))
        {
            _consoleOutput = failure;
            return;
        }

        if (_simulation == null ||
            !_simulation.TrySetEnginesRunning(
                this,
                running,
                out int installed,
                out int successful))
        {
            _consoleOutput =
                "ENGINE ORDER REJECTED.";
            return;
        }

        if (installed <= 0)
        {
            _consoleOutput =
                "ENGINE UNAVAILABLE.\n" +
                "NO ENGINE INSTALLED.";
            return;
        }

        if (running)
        {
            if (successful <= 0)
            {
                _consoleOutput =
                    "ENGINE START FAILED.\n" +
                    "CHECK FUEL / POWER.";
            }
            else if (successful < installed)
            {
                _consoleOutput =
                    $"ENGINE START PARTIAL: {successful}/{installed} ONLINE.";
            }
            else
            {
                _consoleOutput =
                    $"ENGINE START: {successful}/{installed} ONLINE.";
            }
        }
        else
        {
            _consoleOutput =
                $"ENGINE STOP: {successful}/{installed} OFFLINE.";
        }
    }

    private bool TryAcquireConsoleAuthority(
        out string failure)
    {
        failure = null;

        HelmReadoutSnapshot snapshot =
            _readout != null
                ? _readout.Capture()
                : default;

        if (!snapshot.HasHelm ||
            !snapshot.HelmOnline)
        {
            failure =
                "HELM OFFLINE.\n" +
                "CONTROL COMMAND REJECTED.";

            return false;
        }

        if (_simulation == null)
        {
            failure =
                "CONTROL SYSTEM UNAVAILABLE.";

            return false;
        }

        object owner =
            _simulation.ControlOwner;

        if (owner != null &&
            !ReferenceEquals(
                owner,
                this))
        {
            string authority =
                string.IsNullOrWhiteSpace(
                    snapshot.ControlAuthority)
                    ? "OTHER STATION"
                    : snapshot.ControlAuthority;

            failure =
                "CONTROL STATION ACTIVE.\n" +
                $"AUTHORITY: {authority}";

            return false;
        }

        if (!_simulation.HasControlAuthority(this) &&
            !_simulation.TryClaimControl(this))
        {
            failure =
                "CONTROL AUTHORITY UNAVAILABLE.";

            return false;
        }

        return true;
    }

    private static bool ContainsProfanity(
        string command)
    {
        return !string.IsNullOrEmpty(command) &&
               command.Contains("FUCK");
    }

    private static string NormalizeCommand(
        string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string[] pieces =
            raw.Trim()
               .Split(
                   new[] { ' ', '\t', '\r', '\n' },
                   StringSplitOptions.RemoveEmptyEntries);

        return string.Join(
            " ",
            pieces);
    }

    private static bool TryParseNumber(
        string raw,
        out float value)
    {
        return float.TryParse(
            raw,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static void DrawStatusLine(
        float x,
        ref float y,
        float width,
        float line,
        HelmReadoutSnapshot snapshot)
    {
        string status;
        Color color =
            GUI.color;

        if (!snapshot.HasHelm ||
            !snapshot.HelmOnline)
        {
            status = "OFFLINE";
            GUI.color = Color.red;
        }
        else if (snapshot.HelmDamaged)
        {
            status = "DAMAGED";

            GUI.color =
                new Color(
                    1f,
                    0.72f,
                    0.2f,
                    1f);
        }
        else
        {
            status = "ONLINE";
            GUI.color = Color.green;
        }

        DrawLine(
            x,
            ref y,
            width,
            line,
            "STATUS:",
            status);

        GUI.color =
            color;
    }

    private static void DrawNavigationSection(
        float x,
        ref float y,
        float width,
        float line,
        HelmNavigationSnapshot nav)
    {
        bool underway =
            nav.Status ==
            HelmNavigationStatus.Underway;

        float boxHeight =
            underway
                ? 145f
                : 76f;

        DrawSectionBox(
            new Rect(
                x,
                y,
                width,
                boxHeight),
            "NAVIGATION");

        float nx =
            x + 12f;

        float nw =
            width - 24f;

        float ny =
            y + 25f;

        DrawLineAt(
            nx,
            ny,
            nw,
            line,
            "STATUS:",
            FormatNavigationStatus(
                nav.Status));

        ny += line;

        if (nav.Status ==
            HelmNavigationStatus.Docked)
        {
            DrawLineAt(
                nx,
                ny,
                nw,
                line,
                "ROUTE:",
                "NONE");

            y +=
                boxHeight + 10f;

            return;
        }

        if (nav.Status ==
            HelmNavigationStatus.RoutePending)
        {
            DrawLineAt(
                nx,
                ny,
                nw,
                line,
                "ROUTE DATA:",
                "PENDING DEPARTURE");

            y +=
                boxHeight + 10f;

            return;
        }

        DrawLineAt(
            nx,
            ny,
            nw,
            line,
            "HEADING EST.:",
            nav.HasHeadingEstimate
                ? FormatHeading(
                    nav.EstimatedHeadingDegrees)
                : "???");

        ny += line;

        DrawLineAt(
            nx,
            ny,
            nw,
            line,
            "DESIRED COURSE:",
            nav.HasDesiredCourse
                ? FormatHeading(
                    nav.DesiredCourseDegrees)
                : "???");

        ny += line;

        DrawLineAt(
            nx,
            ny,
            nw,
            line,
            "COURSE ERROR:",
            nav.HasCourseError
                ? FormatCourseError(
                    nav.CourseErrorDegrees)
                : "???");

        ny += line;

        DrawLineAt(
            nx,
            ny,
            nw,
            line,
            "CROSS-TRACK:",
            nav.HasCrossTrackEstimate
                ? FormatCrossTrack(
                    nav.CrossTrackError)
                : "???");

        y +=
            boxHeight + 10f;
    }

    private static string FormatNavigationStatus(
        HelmNavigationStatus status)
    {
        switch (status)
        {
            case HelmNavigationStatus.RoutePending:
                return "ROUTE PENDING";

            case HelmNavigationStatus.Underway:
                return "UNDERWAY";

            default:
                return "DOCKED";
        }
    }

    private static void DrawSectionBox(
        Rect rect,
        string title)
    {
        GUI.Box(
            rect,
            GUIContent.none);

        GUI.Label(
            new Rect(
                rect.x + 10f,
                rect.y + 4f,
                rect.width - 20f,
                20f),
            title);
    }

    private static void DrawLine(
        float x,
        ref float y,
        float width,
        float line,
        string label,
        string value)
    {
        DrawLineAt(
            x,
            y,
            width,
            line,
            label,
            value);

        y += line;
    }

    private static void DrawWarningLineAt(
        float x,
        float y,
        float width,
        float line,
        string label,
        string value)
    {
        Color previous =
            GUI.color;

        GUI.color =
            Color.red;

        DrawLineAt(
            x,
            y,
            width,
            line,
            label,
            value);

        GUI.color =
            previous;
    }

    private static void DrawLineAt(
        float x,
        float y,
        float width,
        float line,
        string label,
        string value)
    {
        float labelWidth =
            Mathf.Min(
                180f,
                width * 0.48f);

        GUI.Label(
            new Rect(
                x,
                y,
                labelWidth,
                line),
            label);

        GUI.Label(
            new Rect(
                x + labelWidth,
                y,
                width - labelWidth,
                line),
            value ?? "");
    }

    private static string FormatThrottle(
        float throttle)
    {
        if (Mathf.Abs(throttle) < 0.005f)
            return "STOP";

        int percent =
            Mathf.RoundToInt(
                Mathf.Abs(throttle) *
                100f);

        return throttle > 0f
            ? $"{percent}% AHEAD"
            : $"{percent}% ASTERN";
    }

    private static string FormatRudder(
        float rudderDegrees)
    {
        float abs =
            Mathf.Abs(rudderDegrees);

        if (abs < 0.05f)
            return "CENTER";

        return rudderDegrees > 0f
            ? $"{abs:0.0}° STBD"
            : $"{abs:0.0}° PORT";
    }

    private static string FormatEngineStatus(
        int installed,
        int active)
    {
        installed =
            Mathf.Max(
                0,
                installed);

        active =
            Mathf.Clamp(
                active,
                0,
                installed);

        if (installed <= 0)
            return "NONE";

        if (active <= 0)
            return $"0 / {installed} ACTIVE";

        return $"{active} / {installed} ACTIVE";
    }

    private static string FormatHeading(
        float degrees)
    {
        float normalized =
            Mathf.Repeat(
                degrees,
                360f);

        return $"{normalized:000}°";
    }

    private static string FormatCourseError(
        float degrees)
    {
        float abs =
            Mathf.Abs(degrees);

        if (abs < 0.05f)
            return "ON COURSE";

        return degrees > 0f
            ? $"{abs:0.0}° STBD"
            : $"{abs:0.0}° PORT";
    }

    private static string FormatCrossTrack(
        float error)
    {
        float abs =
            Mathf.Abs(error);

        if (abs < 0.01f)
            return "ON ROUTE";

        return error > 0f
            ? $"{abs:0.00} STBD"
            : $"{abs:0.00} PORT";
    }

    private static MiniGameResult Running()
    {
        return new MiniGameResult
        {
            outcome = MiniGameOutcome.None,
            quality01 = 0f,
            note = null,
            hasMeaningfulProgress = false
        };
    }

    private static MiniGameResult Cancelled(
        string note)
    {
        return new MiniGameResult
        {
            outcome = MiniGameOutcome.Cancelled,
            quality01 = 0f,
            note = note,
            hasMeaningfulProgress = false
        };
    }
}