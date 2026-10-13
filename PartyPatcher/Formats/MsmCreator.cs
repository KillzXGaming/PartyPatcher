using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PartyPatcher.Formats.MsmFile;

namespace PartyPatcher.Formats
{
    public class MsmCreator
    {
        // Some info based on decomp
        // https://github.com/mariopartyrd/marioparty4/blob/main/src/msm/msmse.c

        // Adds sound effect entries
        public static void AddSounds(MsmFile msm, List<AudioEntry> audioEntries, ushort groupID)
        {
            foreach (AudioEntry entry in audioEntries)
            {
                msm.SeEntries.Add(new MSM_SE()
                {
                    Gid = groupID,
                    FxId = entry.SfxID,
                    Vol = 127, // Volume
                    Pan = 64, // Pan left/right
                    PitchBend = 0, // Pitch
                    Span = 0, // Surround panning
                    Reverb = 32, // auxAVol 
                    Chorus = 0,  // auxBVol  
                    // For emitting a 3D sound
                    EmitterF = (byte)(entry.Use3DSound ? 1 : 0),
                    EmiComp = entry.Emit3DRange, //  / 127.0f
                    Pad = new byte[3],
                });
            }
        }
        // Adds group and imports audio samples
        public static void AddGroup(ushort groupID, List<AudioEntry> audioEntries, byte subGroupID = 0, byte stackNo = 1)
        {
            MusGroupEntry group = new()
            {
                Header = new MsmFile.MSM_GRP_INFO()
                {
                    Gid = groupID,
                    SubGrpId = subGroupID,
                    StackNo = stackNo,
                },
            };

            MsmProj proj = new();
            MsmPool pool = new();

            // SDirs which store audio samples with GCN DSP data
            List<MsmFile.SDIREntry> sdirs = new();
            foreach (var entry in audioEntries)
                sdirs.Add(MsmFile.SDIREntry.Create(entry.FilePath, entry.SfxID));

            // Proj file for preparing sound info
            var projEntry = new MsmProj.ProjectEntry();
            proj.Entries.Add(projEntry);

            ushort objID = 8000;
            foreach (var sdir in sdirs)
            {
                var macroID = objID++;

                projEntry.SFXPages.Add(new MsmProj.SFXGroupIndex()
                {
                    ObjId = macroID, // Can be macro id, or layer id for multiple macros
                    SfxId = sdir.Header.m_sfxId, // sfx sample id
                    DefKey = 127,
                    Panning = 64,
                    MaxVoices = 10,
                    Priority = 3,
                });
                // Setup pool for doing sound commands
                projEntry.SoundMacroIds.Add(macroID);
                projEntry.SampleIds.Add(sdir.Header.m_sfxId);
                pool.SoundMacros.Add(BuildDefaultSoundMacro(macroID, sdir.Header.m_sfxId));
            }
            // Save group data (.pool, .proj, .sdir, and .samp data)
            group.PoolData = pool.SaveBytes();
            group.ProjData = proj.SaveBytes();
            group.RebuildSDirs(sdirs);
        }

        // Default commands for playing a sound sample
        static MsmPool.SoundMacro BuildDefaultSoundMacro(ushort objID, ushort sampleID)
        {
            MsmPool.SoundMacro macro = new();
            macro.ObjectId = objID;
            macro.Commands.Add(new CmdSetKeygroup() { Group = 10, KillNow = 0 });
            macro.Commands.Add(new CmdAgeCntSpeed() { Time = 1080000 });
            macro.Commands.Add(new CmdStartSample() { SampleId = sampleID });
            macro.Commands.Add(new CmdWaitMs() { sampleEnd = 1, ms = 65535 });
            macro.Commands.Add(new CmdSetAgeCount() { Counter = 0 });
            macro.Commands.Add(new CmdStopSample());
            macro.Commands.Add(new CmdEnd());
            return macro;
        }

        public class AudioEntry
        {
            public string FilePath;
            public ushort SfxID;
            public bool Use3DSound = false;
            public byte Emit3DRange = 25;
        }
    }
}
