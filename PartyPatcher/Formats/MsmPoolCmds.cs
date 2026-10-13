using AvaloniaToolbox.Core.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PartyPatcher.Formats
{
    public enum CmdOp : byte
    {
        End,
        Stop,
        SplitKey,
        SplitVel,
        WaitTicks,
        Loop,
        Goto,
        WaitMs,
        PlayMacro,
        SendKeyOff,
        SplitMod,
        PianoPan,
        SetAdsr,
        ScaleVolume,
        Panning,
        Envelope,
        StartSample,
        StopSample,
        KeyOff,
        SplitRnd,
        FadeIn,
        Spanning,
        SetAdsrCtrl,
        RndNote,
        AddNote,
        SetNote,
        LastNote,
        Portamento,
        Vibrato,
        PitchSweep1,
        PitchSweep2,
        SetPitch,
        SetPitchAdsr,
        ScaleVolumeDLS,
        Mod2Vibrange,
        SetupTremolo,
        Return,
        GoSub,
        TrapEvent = 0x28,
        UntrapEvent,
        SendMessage,
        GetMessage,
        GetVid,
        AddAgeCount = 0x30, /* unimplemented */
        SetAgeCount,        /* unimplemented */
        SendFlag,           /* unimplemented */
        PitchWheelR,
        SetPriority = 0x36, /* unimplemented */
        AddPriority,        /* unimplemented */
        AgeCntSpeed,        /* unimplemented */
        AgeCntVel,          /* unimplemented */
        VolSelect = 0x40,
        PanSelect,
        PitchWheelSelect,
        ModWheelSelect,
        PedalSelect,
        PortamentoSelect,
        ReverbSelect, /* serves as PostASelect */
        SpanSelect,
        DopplerSelect,
        TremoloSelect,
        PreASelect,
        PreBSelect,
        PostBSelect,
        AuxAFXSelect, /* unimplemented */
        AuxBFXSelect, /* unimplemented */
        SetupLFO = 0x50,
        ModeSelect = 0x58,
        SetKeygroup,
        SRCmodeSelect, /* unimplemented */
        WiiUnknown = 0x5E,
        WiiUnknown2 = 0x5F,
        AddVars = 0x60,
        SubVars,
        MulVars,
        DivVars,
        AddIVars,
        SetVar,
        IfEqual = 0x70,
        IfLess,
        CmdOpMAX,
        Invalid = 0xff
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdEnd : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.End;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdStop : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.Stop;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdStopSample : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.StopSample;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdKeyOff : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.KeyOff;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSplitKey : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SplitKey;
        public sbyte Key;
        public ushort Macro;
        public ushort MacroStep;
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSplitVel : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SplitVel;
        public sbyte Velocity;
        public ushort Macro;
        public ushort MacroStep;
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdWaitTicks : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.WaitTicks;
        public byte Flags;           // Bitfield
        public ushort TicksOrMs;
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;
        public byte Pad4;

        public bool KeyOff => (Flags & 1) != 0;
        public bool Random => (Flags & 2) != 0;
        public bool SampleEnd => (Flags & 4) != 0;
        public bool Absolute => (Flags & 8) != 0;
        public bool MsSwitch => (Flags & 16) != 0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdLoop : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.Loop;
        public byte Flags;
        public ushort MacroStep;
        public ushort Times;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdGoto : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.Goto;
        public byte Dummy;
        public ushort Macro;
        public ushort MacroStep;
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdWaitMs : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.WaitMs;
        public byte keyOff;
        public byte random;
        public byte sampleEnd;
        public byte absolute;

        public byte dummy;
        public ushort ms;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPlayMacro : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PlayMacro;
        public sbyte AddNote;
        public ushort Macro;
        public ushort MacroStep;
        public byte Priority;
        public byte MaxVoices;
        public byte Pad;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdStartSample : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.StartSample;
        public ushort SampleId;
        public byte Mode;
        public uint Offset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetAdsr : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetAdsr;
        public ushort TableId;
        public byte DlsMode;        // 0 or 1
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;
        public byte Pad4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetPitch : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetPitch;
        public byte Hz1;   // 24-bit little-endian
        public byte Hz2;
        public byte Hz3;
        public ushort Fine;

        public uint Hz => (uint)(Hz1 | (Hz2 << 8) | (Hz3 << 16));
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSendKeyOff : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SendKeyOff;
        public byte Variable;
        public byte LastStarted;   // 0 or 1
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;
        public byte Pad4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSplitMod : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SplitMod;
        public sbyte ModValue;
        public ushort Macro;
        public ushort MacroStep;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPianoPan : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PianoPan;
        public sbyte Scale;
        public sbyte CenterKey;
        public sbyte CenterPan;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdScaleVolume : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.ScaleVolume;
        public sbyte Scale;
        public sbyte Add;
        public ushort Table;
        public byte OriginalVol;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPanning : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.Panning;
        public sbyte PanPosition;
        public ushort TimeMs;
        public sbyte Width;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdEnvelope : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.Envelope;
        public sbyte Scale;
        public sbyte Add;
        public ushort Table;
        public byte Flags;           // MsSwitch in bit 0
        public ushort TicksOrMs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSplitRnd : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SplitRnd;
        public byte Rnd;
        public ushort Macro;
        public ushort MacroStep;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdFadeIn : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.FadeIn;
        public sbyte Scale;
        public sbyte Add;
        public ushort Table;
        public byte Flags;
        public ushort TicksOrMs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSpanning : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.Spanning;
        public sbyte SpanPosition;
        public ushort TimeMs;
        public sbyte Width;
    }

    // More Control Commands
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetAdsrCtrl : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetAdsrCtrl;
        public byte Attack;
        public byte Decay;
        public byte Sustain;
        public byte Release;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdRndNote : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.RndNote;
        public sbyte NoteLo;
        public sbyte Detune;
        public sbyte NoteHi;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdAddNote : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.AddNote;
        public sbyte Add;
        public sbyte Detune;
        public byte OriginalKey;
        public byte Pad;
        public byte Flags;
        public ushort TicksOrMs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetNote : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetNote;
        public sbyte Key;
        public sbyte Detune;
        public byte Pad1; public byte Pad2;
        public byte Flags;
        public ushort TicksOrMs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdLastNote : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.LastNote;
        public sbyte Add;
        public sbyte Detune;
        public byte Pad1; public byte Pad2;
        public byte Flags;
        public ushort TicksOrMs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPortamento : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.Portamento;
        public byte PortState;
        public byte PortType;
        public byte Pad1; public byte Pad2;
        public byte Flags;
        public ushort TicksOrMs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdVibrato : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.Vibrato;
        public sbyte LevelNote;
        public sbyte LevelFine;
        public byte ModwheelFlag;
        public byte Pad;
        public byte Flags;
        public ushort TicksOrMs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPitchSweep1 : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PitchSweep1;
        public sbyte Times;
        public short Add;
        public byte Pad;
        public byte Flags;
        public ushort TicksOrMs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPitchSweep2 : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PitchSweep2;
        public sbyte Times;
        public short Add;
        public byte Pad;
        public byte Flags;
        public ushort TicksOrMs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetPitchAdsr : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetPitchAdsr;
        public ushort Table;
        public byte Pad;
        public sbyte Keys;
        public sbyte Cents;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdScaleVolumeDLS : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.ScaleVolumeDLS;
        public short Scale;
        public byte OriginalVol;
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdMod2Vibrange : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.Mod2Vibrange;
        public sbyte Keys;
        public sbyte Cents;
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;
        public byte Pad4;
        public byte Pad5;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetupTremolo : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetupTremolo;
        public short Scale;
        public byte Pad;
        public short ModwAddScale;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdReturn : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.Return;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdGoSub : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.GoSub;
        public byte Pad;
        public ushort Macro;
        public ushort MacroStep;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdTrapEvent : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.TrapEvent;
        public byte EventType;      // 0=KeyOff, 1=SampleEnd, 2=MessageRecv
        public ushort Macro;
        public ushort MacroStep;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdUntrapEvent : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.UntrapEvent;
        public byte EventType;
        public byte Pad1; public byte Pad2; public byte Pad3;
        public byte Pad4; public byte Pad5; public byte Pad6;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSendMessage : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SendMessage;
        public byte IsVar;
        public ushort Macro;
        public byte VoiceVar;
        public byte ValueVar;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdGetMessage : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.GetMessage;
        public byte Variable;
        public byte Pad1; public byte Pad2; public byte Pad3;
        public byte Pad4; public byte Pad5; public byte Pad6;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdGetVid : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.GetVid;
        public byte Variable;
        public byte PlayMacro;
        public byte Pad1; public byte Pad2; public byte Pad3;
        public byte Pad4; public byte Pad5;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdVolSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.VolSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;        // 0=Set, 1=Add, 2=Mult
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPanSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PanSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPitchWheelSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PitchWheelSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdModWheelSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.ModWheelSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPedalSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PedalSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPortamentoSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PortamentoSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdReverbSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.ReverbSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSpanSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SpanSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdDopplerSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.DopplerSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdTremoloSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.TremoloSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPreASelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PreASelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPreBSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PreBSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPostBSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PostBSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetupLFO : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetupLFO;
        public byte LfoNumber;
        public ushort PeriodInMs;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdModeSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.ModeSelect;
        public byte DlsVol;
        public byte Itd;
        public byte Pad1; public byte Pad2; public byte Pad3; public byte Pad4; public byte Pad5;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdAddAgeCount : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.AddAgeCount;
        public byte Pad;
        public short Add;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetAgeCount : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetAgeCount;
        public byte Pad;
        public ushort Counter;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSendFlag : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SendFlag;
        public byte FlagId;
        public byte Value;
        public byte Pad1; public byte Pad2; public byte Pad3; public byte Pad4; public byte Pad5;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdPitchWheelR : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.PitchWheelR;
        public sbyte RangeUp;
        public sbyte RangeDown;
        public byte Pad1; public byte Pad2; public byte Pad3; public byte Pad4; public byte Pad5;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetPriority : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetPriority;
        public byte Prio;
        public byte Pad1; public byte Pad2; public byte Pad3; public byte Pad4; public byte Pad5; public byte Pad6;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdAddPriority : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.AddPriority;
        public byte Pad;
        public short Prio;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdAgeCntSpeed : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.AgeCntSpeed;
        public byte Pad1; public byte Pad2; public byte Pad3;
        public uint Time;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdAgeCntVel : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.AgeCntVel;
        public byte Pad;
        public ushort AgeBase;
        public ushort AgeScale;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetKeygroup : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetKeygroup;
        public byte Group;
        public byte KillNow;
        public byte Pad1;
        public byte Pad2;
        public byte Pad3;
        public byte Pad4; 
        public byte Pad5; 
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdAuxAFXSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.AuxAFXSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte ParamIndex;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdAuxBFXSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.AuxBFXSelect;
        public byte MidiControl;
        public short ScalingPercentage;
        public byte Combine;
        public byte IsVar;
        public sbyte FineScaling;
        public byte ParamIndex;
        public byte Pad1; public byte Pad2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSRCmodeSelect : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SRCmodeSelect;
        public byte SrcType;
        public byte Type0SrcFilter;
        public byte Pad1; public byte Pad2; public byte Pad3; public byte Pad4; public byte Pad5; public byte Pad6;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdWiiUnknown : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.WiiUnknown;
        public byte Flag;
        public byte Pad1; public byte Pad2; public byte Pad3; public byte Pad4; public byte Pad5; public byte Pad6;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdWiiUnknown2 : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.WiiUnknown2;
        public byte Flag;
        public byte Pad1; public byte Pad2; public byte Pad3; public byte Pad4; public byte Pad5; public byte Pad6;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdAddVars : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.AddVars;
        public byte VarCtrlA;
        public byte A;
        public byte VarCtrlB;
        public byte B;
        public byte VarCtrlC;
        public byte C;
        public byte Pad;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSubVars : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SubVars;
        public byte VarCtrlA;
        public sbyte A;
        public byte VarCtrlB;
        public sbyte B;
        public byte VarCtrlC;
        public sbyte C;
        public byte Pad;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdMulVars : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.MulVars;
        public byte VarCtrlA;
        public sbyte A;
        public byte VarCtrlB;
        public sbyte B;
        public byte VarCtrlC;
        public sbyte C;
        public byte Pad;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdDivVars : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.DivVars;
        public byte VarCtrlA;
        public sbyte A;
        public byte VarCtrlB;
        public sbyte B;
        public byte VarCtrlC;
        public sbyte C;
        public byte Pad;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdAddIVars : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.AddIVars;
        public byte VarCtrlA;
        public sbyte A;
        public byte VarCtrlB;
        public sbyte B;
        public short Imm;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdSetVar : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.SetVar;
        public byte VarCtrlA;
        public sbyte A;
        public byte Pad;
        public short Imm;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdIfEqual : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.IfEqual;
        public byte VarCtrlA;
        public sbyte A;
        public byte VarCtrlB;
        public sbyte B;
        public byte NotEq;
        public ushort MacroStep;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CmdIfLess : ISoundMacroCommand
    {
        public CmdOp Op => CmdOp.IfLess;
        public byte VarCtrlA;
        public sbyte A;
        public byte VarCtrlB;
        public sbyte B;
        public byte NotLt;
        public ushort MacroStep;
    }

    public struct CmdUnknown : ISoundMacroCommand
    {
        public CmdOp Op { get; set; }

        public uint Cmds1;
        public uint Cmds2;
    }

    [JsonDerivedType(typeof(CmdEnd), typeDiscriminator: "End")]
    [JsonDerivedType(typeof(CmdStop), typeDiscriminator: "Stop")]
    [JsonDerivedType(typeof(CmdKeyOff), typeDiscriminator: "KeyOff")]
    [JsonDerivedType(typeof(CmdSplitKey), typeDiscriminator: "SplitKey")]
    [JsonDerivedType(typeof(CmdSplitVel), typeDiscriminator: "SplitVel")]
    [JsonDerivedType(typeof(CmdWaitTicks), typeDiscriminator: "WaitTicks")]
    [JsonDerivedType(typeof(CmdLoop), typeDiscriminator: "Loop")]
    [JsonDerivedType(typeof(CmdGoto), typeDiscriminator: "Goto")]
    [JsonDerivedType(typeof(CmdWaitMs), typeDiscriminator: "WaitMs")]
    [JsonDerivedType(typeof(CmdPlayMacro), typeDiscriminator: "PlayMacro")]
    [JsonDerivedType(typeof(CmdSendKeyOff), typeDiscriminator: "SendKeyOff")]
    [JsonDerivedType(typeof(CmdSplitMod), typeDiscriminator: "SplitMod")]
    [JsonDerivedType(typeof(CmdPianoPan), typeDiscriminator: "PianoPan")]
    [JsonDerivedType(typeof(CmdSetAdsr), typeDiscriminator: "SetAdsr")]
    [JsonDerivedType(typeof(CmdScaleVolume), typeDiscriminator: "ScaleVolume")]
    [JsonDerivedType(typeof(CmdPanning), typeDiscriminator: "Panning")]
    [JsonDerivedType(typeof(CmdEnvelope), typeDiscriminator: "Envelope")]
    [JsonDerivedType(typeof(CmdStartSample), typeDiscriminator: "StartSample")]
    [JsonDerivedType(typeof(CmdStopSample), typeDiscriminator: "StopSample")]
    [JsonDerivedType(typeof(CmdSplitRnd), typeDiscriminator: "SplitRnd")]
    [JsonDerivedType(typeof(CmdFadeIn), typeDiscriminator: "FadeIn")]
    [JsonDerivedType(typeof(CmdSpanning), typeDiscriminator: "Spanning")]
    [JsonDerivedType(typeof(CmdSetAdsrCtrl), typeDiscriminator: "SetAdsrCtrl")]
    [JsonDerivedType(typeof(CmdRndNote), typeDiscriminator: "RndNote")]
    [JsonDerivedType(typeof(CmdAddNote), typeDiscriminator: "AddNote")]
    [JsonDerivedType(typeof(CmdSetNote), typeDiscriminator: "SetNote")]
    [JsonDerivedType(typeof(CmdLastNote), typeDiscriminator: "LastNote")]
    [JsonDerivedType(typeof(CmdPortamento), typeDiscriminator: "Portamento")]
    [JsonDerivedType(typeof(CmdVibrato), typeDiscriminator: "Vibrato")]
    [JsonDerivedType(typeof(CmdPitchSweep1), typeDiscriminator: "PitchSweep1")]
    [JsonDerivedType(typeof(CmdPitchSweep2), typeDiscriminator: "PitchSweep2")]
    [JsonDerivedType(typeof(CmdSetPitch), typeDiscriminator: "SetPitch")]
    [JsonDerivedType(typeof(CmdSetPitchAdsr), typeDiscriminator: "SetPitchAdsr")]
    [JsonDerivedType(typeof(CmdScaleVolumeDLS), typeDiscriminator: "ScaleVolumeDLS")]
    [JsonDerivedType(typeof(CmdMod2Vibrange), typeDiscriminator: "Mod2Vibrange")]
    [JsonDerivedType(typeof(CmdSetupTremolo), typeDiscriminator: "SetupTremolo")]
    [JsonDerivedType(typeof(CmdReturn), typeDiscriminator: "Return")]
    [JsonDerivedType(typeof(CmdGoSub), typeDiscriminator: "GoSub")]
    [JsonDerivedType(typeof(CmdTrapEvent), typeDiscriminator: "TrapEvent")]
    [JsonDerivedType(typeof(CmdUntrapEvent), typeDiscriminator: "UntrapEvent")]
    [JsonDerivedType(typeof(CmdSendMessage), typeDiscriminator: "SendMessage")]
    [JsonDerivedType(typeof(CmdGetMessage), typeDiscriminator: "GetMessage")]
    [JsonDerivedType(typeof(CmdGetVid), typeDiscriminator: "GetVid")]
    [JsonDerivedType(typeof(CmdAddAgeCount), typeDiscriminator: "AddAgeCount")]
    [JsonDerivedType(typeof(CmdSetAgeCount), typeDiscriminator: "SetAgeCount")]
    [JsonDerivedType(typeof(CmdSendFlag), typeDiscriminator: "SendFlag")]
    [JsonDerivedType(typeof(CmdPitchWheelR), typeDiscriminator: "PitchWheelR")]
    [JsonDerivedType(typeof(CmdSetPriority), typeDiscriminator: "SetPriority")]
    [JsonDerivedType(typeof(CmdAddPriority), typeDiscriminator: "AddPriority")]
    [JsonDerivedType(typeof(CmdAgeCntSpeed), typeDiscriminator: "AgeCntSpeed")]
    [JsonDerivedType(typeof(CmdAgeCntVel), typeDiscriminator: "AgeCntVel")]
    [JsonDerivedType(typeof(CmdVolSelect), typeDiscriminator: "VolSelect")]
    [JsonDerivedType(typeof(CmdPanSelect), typeDiscriminator: "PanSelect")]
    [JsonDerivedType(typeof(CmdPitchWheelSelect), typeDiscriminator: "PitchWheelSelect")]
    [JsonDerivedType(typeof(CmdModWheelSelect), typeDiscriminator: "ModWheelSelect")]
    [JsonDerivedType(typeof(CmdPedalSelect), typeDiscriminator: "PedalSelect")]
    [JsonDerivedType(typeof(CmdPortamentoSelect), typeDiscriminator: "PortamentoSelect")]
    [JsonDerivedType(typeof(CmdReverbSelect), typeDiscriminator: "ReverbSelect")]
    [JsonDerivedType(typeof(CmdSpanSelect), typeDiscriminator: "SpanSelect")]
    [JsonDerivedType(typeof(CmdDopplerSelect), typeDiscriminator: "DopplerSelect")]
    [JsonDerivedType(typeof(CmdTremoloSelect), typeDiscriminator: "TremoloSelect")]
    [JsonDerivedType(typeof(CmdPreASelect), typeDiscriminator: "PreASelect")]
    [JsonDerivedType(typeof(CmdPreBSelect), typeDiscriminator: "PreBSelect")]
    [JsonDerivedType(typeof(CmdPostBSelect), typeDiscriminator: "PostBSelect")]
    [JsonDerivedType(typeof(CmdSetupLFO), typeDiscriminator: "SetupLFO")]
    [JsonDerivedType(typeof(CmdModeSelect), typeDiscriminator: "ModeSelect")]
    [JsonDerivedType(typeof(CmdSetKeygroup), typeDiscriminator: "SetKeygroup")]
    [JsonDerivedType(typeof(CmdSRCmodeSelect), typeDiscriminator: "SRCmodeSelect")]
    [JsonDerivedType(typeof(CmdWiiUnknown), typeDiscriminator: "WiiUnknown")]
    [JsonDerivedType(typeof(CmdWiiUnknown2), typeDiscriminator: "WiiUnknown2")]
    [JsonDerivedType(typeof(CmdAddVars), typeDiscriminator: "AddVars")]
    [JsonDerivedType(typeof(CmdSubVars), typeDiscriminator: "SubVars")]
    [JsonDerivedType(typeof(CmdMulVars), typeDiscriminator: "MulVars")]
    [JsonDerivedType(typeof(CmdDivVars), typeDiscriminator: "DivVars")]
    [JsonDerivedType(typeof(CmdAddIVars), typeDiscriminator: "AddIVars")]
    [JsonDerivedType(typeof(CmdSetVar), typeDiscriminator: "SetVar")]
    [JsonDerivedType(typeof(CmdIfEqual), typeDiscriminator: "IfEqual")]
    [JsonDerivedType(typeof(CmdIfLess), typeDiscriminator: "IfLess")]
    [JsonDerivedType(typeof(CmdUnknown), typeDiscriminator: "Unknown")]
    public interface ISoundMacroCommand
    {
        public CmdOp Op { get; }

        public static void WriteCommand(FileWriter writer, ISoundMacroCommand command)
        {
            var start = writer.Position;

            byte[] bytes = GetCommandBytes(command);
            Console.WriteLine($"{bytes.Length} {command.Op}");

            Trace.Assert(bytes.Length <= 7);

            byte[] dst = new byte[8];
            dst[0] = (byte)command.Op;
            Array.Copy(bytes, 0, dst, 1, bytes.Length);

            uint u1 = BitConverter.ToUInt32(dst, 0);
            uint u2 = BitConverter.ToUInt32(dst, 4);

            writer.Write(u1);
            writer.Write(u2);

            writer.SeekBegin(start + 8);
        }

        private static byte[] GetCommandBytes(ISoundMacroCommand cmd)
        {
            // Use concrete type instead of interface
            return cmd switch
            {
                CmdEnd c => StructToBytes(c),
                CmdStop c => StructToBytes(c),
                CmdStopSample c => StructToBytes(c),
                CmdKeyOff c => StructToBytes(c),
                CmdSplitKey c => StructToBytes(c),
                CmdSplitVel c => StructToBytes(c),
                CmdWaitTicks c => StructToBytes(c),
                CmdLoop c => StructToBytes(c),
                CmdGoto c => StructToBytes(c),
                CmdWaitMs c => StructToBytes(c),
                CmdPlayMacro c => StructToBytes(c),
                CmdStartSample c => StructToBytes(c),
                CmdSetAdsr c => StructToBytes(c),
                CmdSetPitch c => StructToBytes(c),
                CmdSendKeyOff c => StructToBytes(c),
                CmdSplitMod c => StructToBytes(c),
                CmdPianoPan c => StructToBytes(c),
                CmdScaleVolume c => StructToBytes(c),
                CmdPanning c => StructToBytes(c),
                CmdEnvelope c => StructToBytes(c),
                CmdSplitRnd c => StructToBytes(c),
                CmdFadeIn c => StructToBytes(c),
                CmdSpanning c => StructToBytes(c),
                CmdSetAdsrCtrl c => StructToBytes(c),
                CmdRndNote c => StructToBytes(c),
                CmdAddNote c => StructToBytes(c),
                CmdSetNote c => StructToBytes(c),
                CmdLastNote c => StructToBytes(c),
                CmdPortamento c => StructToBytes(c),
                CmdVibrato c => StructToBytes(c),
                CmdPitchSweep1 c => StructToBytes(c),
                CmdPitchSweep2 c => StructToBytes(c),
                CmdSetPitchAdsr c => StructToBytes(c),
                CmdScaleVolumeDLS c => StructToBytes(c),
                CmdMod2Vibrange c => StructToBytes(c),
                CmdSetupTremolo c => StructToBytes(c),
                CmdReturn c => StructToBytes(c),
                CmdGoSub c => StructToBytes(c),
                CmdTrapEvent c => StructToBytes(c),
                CmdUntrapEvent c => StructToBytes(c),
                CmdSendMessage c => StructToBytes(c),
                CmdGetMessage c => StructToBytes(c),
                CmdGetVid c => StructToBytes(c),
                CmdAddAgeCount c => StructToBytes(c),
                CmdSetAgeCount c => StructToBytes(c),
                CmdSendFlag c => StructToBytes(c),
                CmdPitchWheelR c => StructToBytes(c),
                CmdSetPriority c => StructToBytes(c),
                CmdAddPriority c => StructToBytes(c),
                CmdAgeCntSpeed c => StructToBytes(c),
                CmdAgeCntVel c => StructToBytes(c),
                CmdVolSelect c => StructToBytes(c),
                CmdPanSelect c => StructToBytes(c),
                CmdPitchWheelSelect c => StructToBytes(c),
                CmdModWheelSelect c => StructToBytes(c),
                CmdPedalSelect c => StructToBytes(c),
                CmdPortamentoSelect c => StructToBytes(c),
                CmdReverbSelect c => StructToBytes(c),
                CmdSpanSelect c => StructToBytes(c),
                CmdDopplerSelect c => StructToBytes(c),
                CmdTremoloSelect c => StructToBytes(c),
                CmdPreASelect c => StructToBytes(c),
                CmdPreBSelect c => StructToBytes(c),
                CmdPostBSelect c => StructToBytes(c),
                CmdSetupLFO c => StructToBytes(c),
                CmdModeSelect c => StructToBytes(c),
                CmdSetKeygroup c => StructToBytes(c),
                CmdSRCmodeSelect c => StructToBytes(c),
                CmdWiiUnknown c => StructToBytes(c),
                CmdWiiUnknown2 c => StructToBytes(c),
                CmdAddVars c => StructToBytes(c),
                CmdSubVars c => StructToBytes(c),
                CmdMulVars c => StructToBytes(c),
                CmdDivVars c => StructToBytes(c),
                CmdAddIVars c => StructToBytes(c),
                CmdSetVar c => StructToBytes(c),
                CmdIfEqual c => StructToBytes(c),
                CmdIfLess c => StructToBytes(c),

                _ => StructToBytes((CmdUnknown)cmd)   // fallback
            };
        }


        private static byte[] StructToBytes<T>(T structure) where T : ISoundMacroCommand
        {
            int size = Marshal.SizeOf<T>();
            byte[] bytes = new byte[size];

            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(structure, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }

            return bytes;
        }

        public static ISoundMacroCommand ReadCommand(FileReader cmdReader, CmdOp op)
        {
            return op switch
            {
                CmdOp.End => cmdReader.ReadStruct<CmdEnd>(),
                CmdOp.Stop => cmdReader.ReadStruct<CmdStop>(),
                CmdOp.SplitKey => cmdReader.ReadStruct<CmdSplitKey>(),
                CmdOp.SplitVel => cmdReader.ReadStruct<CmdSplitVel>(),
                CmdOp.WaitTicks => cmdReader.ReadStruct<CmdWaitTicks>(),
                CmdOp.Loop => cmdReader.ReadStruct<CmdLoop>(),
                CmdOp.Goto => cmdReader.ReadStruct<CmdGoto>(),
                CmdOp.WaitMs => cmdReader.ReadStruct<CmdWaitMs>(),
                CmdOp.PlayMacro => cmdReader.ReadStruct<CmdPlayMacro>(),
                CmdOp.SendKeyOff => cmdReader.ReadStruct<CmdSendKeyOff>(),
                CmdOp.SplitMod => cmdReader.ReadStruct<CmdSplitMod>(),
                CmdOp.PianoPan => cmdReader.ReadStruct<CmdPianoPan>(),
                CmdOp.SetAdsr => cmdReader.ReadStruct<CmdSetAdsr>(),
                CmdOp.ScaleVolume => cmdReader.ReadStruct<CmdScaleVolume>(),
                CmdOp.Panning => cmdReader.ReadStruct<CmdPanning>(),
                CmdOp.Envelope => cmdReader.ReadStruct<CmdEnvelope>(),
                CmdOp.StartSample => cmdReader.ReadStruct<CmdStartSample>(),
                CmdOp.StopSample => cmdReader.ReadStruct<CmdStopSample>(),
                CmdOp.KeyOff => cmdReader.ReadStruct<CmdKeyOff>(),
                CmdOp.SplitRnd => cmdReader.ReadStruct<CmdSplitRnd>(),
                CmdOp.FadeIn => cmdReader.ReadStruct<CmdFadeIn>(),
                CmdOp.Spanning => cmdReader.ReadStruct<CmdSpanning>(),
                CmdOp.SetAdsrCtrl => cmdReader.ReadStruct<CmdSetAdsrCtrl>(),
                CmdOp.RndNote => cmdReader.ReadStruct<CmdRndNote>(),
                CmdOp.AddNote => cmdReader.ReadStruct<CmdAddNote>(),
                CmdOp.SetNote => cmdReader.ReadStruct<CmdSetNote>(),
                CmdOp.LastNote => cmdReader.ReadStruct<CmdLastNote>(),
                CmdOp.Portamento => cmdReader.ReadStruct<CmdPortamento>(),
                CmdOp.Vibrato => cmdReader.ReadStruct<CmdVibrato>(),
                CmdOp.PitchSweep1 => cmdReader.ReadStruct<CmdPitchSweep1>(),
                CmdOp.PitchSweep2 => cmdReader.ReadStruct<CmdPitchSweep2>(),
                CmdOp.SetPitch => cmdReader.ReadStruct<CmdSetPitch>(),
                CmdOp.SetPitchAdsr => cmdReader.ReadStruct<CmdSetPitchAdsr>(),
                CmdOp.ScaleVolumeDLS => cmdReader.ReadStruct<CmdScaleVolumeDLS>(),
                CmdOp.Mod2Vibrange => cmdReader.ReadStruct<CmdMod2Vibrange>(),
                CmdOp.SetupTremolo => cmdReader.ReadStruct<CmdSetupTremolo>(),
                CmdOp.Return => cmdReader.ReadStruct<CmdReturn>(),
                CmdOp.GoSub => cmdReader.ReadStruct<CmdGoSub>(),
                CmdOp.TrapEvent => cmdReader.ReadStruct<CmdTrapEvent>(),
                CmdOp.UntrapEvent => cmdReader.ReadStruct<CmdUntrapEvent>(),
                CmdOp.SendMessage => cmdReader.ReadStruct<CmdSendMessage>(),
                CmdOp.GetMessage => cmdReader.ReadStruct<CmdGetMessage>(),
                CmdOp.GetVid => cmdReader.ReadStruct<CmdGetVid>(),
                CmdOp.AddAgeCount => cmdReader.ReadStruct<CmdAddAgeCount>(),
                CmdOp.SetAgeCount => cmdReader.ReadStruct<CmdSetAgeCount>(),
                CmdOp.SendFlag => cmdReader.ReadStruct<CmdSendFlag>(),
                CmdOp.PitchWheelR => cmdReader.ReadStruct<CmdPitchWheelR>(),
                CmdOp.SetPriority => cmdReader.ReadStruct<CmdSetPriority>(),
                CmdOp.AddPriority => cmdReader.ReadStruct<CmdAddPriority>(),
                CmdOp.AgeCntSpeed => cmdReader.ReadStruct<CmdAgeCntSpeed>(),
                CmdOp.AgeCntVel => cmdReader.ReadStruct<CmdAgeCntVel>(),
                CmdOp.VolSelect => cmdReader.ReadStruct<CmdVolSelect>(),
                CmdOp.PanSelect => cmdReader.ReadStruct<CmdPanSelect>(),
                CmdOp.PitchWheelSelect => cmdReader.ReadStruct<CmdPitchWheelSelect>(),
                CmdOp.ModWheelSelect => cmdReader.ReadStruct<CmdModWheelSelect>(),
                CmdOp.PedalSelect => cmdReader.ReadStruct<CmdPedalSelect>(),
                CmdOp.PortamentoSelect => cmdReader.ReadStruct<CmdPortamentoSelect>(),
                CmdOp.ReverbSelect => cmdReader.ReadStruct<CmdReverbSelect>(),
                CmdOp.SpanSelect => cmdReader.ReadStruct<CmdSpanSelect>(),
                CmdOp.DopplerSelect => cmdReader.ReadStruct<CmdDopplerSelect>(),
                CmdOp.TremoloSelect => cmdReader.ReadStruct<CmdTremoloSelect>(),
                CmdOp.PreASelect => cmdReader.ReadStruct<CmdPreASelect>(),
                CmdOp.PreBSelect => cmdReader.ReadStruct<CmdPreBSelect>(),
                CmdOp.PostBSelect => cmdReader.ReadStruct<CmdPostBSelect>(),
                CmdOp.AuxAFXSelect => cmdReader.ReadStruct<CmdAuxAFXSelect>(),
                CmdOp.AuxBFXSelect => cmdReader.ReadStruct<CmdAuxBFXSelect>(),
                CmdOp.SetupLFO => cmdReader.ReadStruct<CmdSetupLFO>(),
                CmdOp.ModeSelect => cmdReader.ReadStruct<CmdModeSelect>(),
                CmdOp.SetKeygroup => cmdReader.ReadStruct<CmdSetKeygroup>(),
                CmdOp.SRCmodeSelect => cmdReader.ReadStruct<CmdSRCmodeSelect>(),
                CmdOp.WiiUnknown => cmdReader.ReadStruct<CmdWiiUnknown>(),
                CmdOp.WiiUnknown2 => cmdReader.ReadStruct<CmdWiiUnknown2>(),
                CmdOp.AddVars => cmdReader.ReadStruct<CmdAddVars>(),
                CmdOp.SubVars => cmdReader.ReadStruct<CmdSubVars>(),
                CmdOp.MulVars => cmdReader.ReadStruct<CmdMulVars>(),
                CmdOp.DivVars => cmdReader.ReadStruct<CmdDivVars>(),
                CmdOp.AddIVars => cmdReader.ReadStruct<CmdAddIVars>(),
                CmdOp.SetVar => cmdReader.ReadStruct<CmdSetVar>(),
                CmdOp.IfEqual => cmdReader.ReadStruct<CmdIfEqual>(),
                CmdOp.IfLess => cmdReader.ReadStruct<CmdIfLess>(),
                _ => cmdReader.ReadStruct<CmdUnknown>()
            };
        }

        public static ISoundMacroCommand Create(CmdOp op)
        {
            return op switch
            {
                CmdOp.End => new CmdEnd(),
                CmdOp.Stop => new CmdStop(),
                CmdOp.SplitKey => new CmdSplitKey(),
                CmdOp.SplitVel => new CmdSplitVel(),
                CmdOp.WaitTicks => new CmdWaitTicks(),
                CmdOp.Loop => new CmdLoop(),
                CmdOp.Goto => new CmdGoto(),
                CmdOp.WaitMs => new CmdWaitMs(),
                CmdOp.PlayMacro => new CmdPlayMacro(),
                CmdOp.SendKeyOff => new CmdSendKeyOff(),
                CmdOp.SplitMod => new CmdSplitMod(),
                CmdOp.PianoPan => new CmdPianoPan(),
                CmdOp.SetAdsr => new CmdSetAdsr(),
                CmdOp.ScaleVolume => new CmdScaleVolume(),
                CmdOp.Panning => new CmdPanning(),
                CmdOp.Envelope => new CmdEnvelope(),
                CmdOp.StartSample => new CmdStartSample(),
                CmdOp.StopSample => new CmdStopSample(),
                CmdOp.KeyOff => new CmdKeyOff(),
                CmdOp.SplitRnd => new CmdSplitRnd(),
                CmdOp.FadeIn => new CmdFadeIn(),
                CmdOp.Spanning => new CmdSpanning(),
                CmdOp.SetAdsrCtrl => new CmdSetAdsrCtrl(),
                CmdOp.RndNote => new CmdRndNote(),
                CmdOp.AddNote => new CmdAddNote(),
                CmdOp.SetNote => new CmdSetNote(),
                CmdOp.LastNote => new CmdLastNote(),
                CmdOp.Portamento => new CmdPortamento(),
                CmdOp.Vibrato => new CmdVibrato(),
                CmdOp.PitchSweep1 => new CmdPitchSweep1(),
                CmdOp.PitchSweep2 => new CmdPitchSweep2(),
                CmdOp.SetPitch => new CmdSetPitch(),
                CmdOp.SetPitchAdsr => new CmdSetPitchAdsr(),
                CmdOp.ScaleVolumeDLS => new CmdScaleVolumeDLS(),
                CmdOp.Mod2Vibrange => new CmdMod2Vibrange(),
                CmdOp.SetupTremolo => new CmdSetupTremolo(),
                CmdOp.Return => new CmdReturn(),
                CmdOp.GoSub => new CmdGoSub(),
                CmdOp.TrapEvent => new CmdTrapEvent(),
                CmdOp.UntrapEvent => new CmdUntrapEvent(),
                CmdOp.SendMessage => new CmdSendMessage(),
                CmdOp.GetMessage => new CmdGetMessage(),
                CmdOp.GetVid => new CmdGetVid(),
                CmdOp.VolSelect => new CmdVolSelect(),
                CmdOp.PanSelect => new CmdPanSelect(),
                CmdOp.PitchWheelSelect => new CmdPitchWheelSelect(),
                CmdOp.ModWheelSelect => new CmdModWheelSelect(),
                CmdOp.PedalSelect => new CmdPedalSelect(),
                CmdOp.PortamentoSelect => new CmdPortamentoSelect(),
                CmdOp.ReverbSelect => new CmdReverbSelect(),
                CmdOp.SpanSelect => new CmdSpanSelect(),
                CmdOp.DopplerSelect => new CmdDopplerSelect(),
                CmdOp.TremoloSelect => new CmdTremoloSelect(),
                CmdOp.PreASelect => new CmdPreASelect(),
                CmdOp.PreBSelect => new CmdPreBSelect(),
                CmdOp.PostBSelect => new CmdPostBSelect(),
                CmdOp.SetupLFO => new CmdSetupLFO(),
                CmdOp.ModeSelect => new CmdModeSelect(),
                CmdOp.SetKeygroup => new CmdSetKeygroup(),
                CmdOp.AddAgeCount => new CmdAddAgeCount(),
                CmdOp.SetAgeCount => new CmdSetAgeCount(),
                CmdOp.SendFlag => new CmdSendFlag(),
                CmdOp.PitchWheelR => new CmdPitchWheelR(),
                CmdOp.SetPriority => new CmdSetPriority(),
                CmdOp.AddPriority => new CmdAddPriority(),
                CmdOp.AgeCntSpeed => new CmdAgeCntSpeed(),
                CmdOp.AgeCntVel => new CmdAgeCntVel(),
                CmdOp.SRCmodeSelect => new CmdSRCmodeSelect(),
                CmdOp.WiiUnknown => new CmdWiiUnknown(),
                CmdOp.WiiUnknown2 => new CmdWiiUnknown2(),
                CmdOp.AddVars => new CmdAddVars(),
                CmdOp.SubVars => new CmdSubVars(),
                CmdOp.MulVars => new CmdMulVars(),
                CmdOp.DivVars => new CmdDivVars(),
                CmdOp.AddIVars => new CmdAddIVars(),
                CmdOp.SetVar => new CmdSetVar(),
                CmdOp.IfEqual => new CmdIfEqual(),
                CmdOp.IfLess => new CmdIfLess(),
                _ => new CmdUnknown { Op = op }
            };
        }
    }
}
