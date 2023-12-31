﻿using System.Collections.Generic;
using TextToTalk.Backends;
#pragma warning disable CS0612
#pragma warning disable CS0618

namespace TextToTalk.Migrations;

public class Migration1_18_2 : IConfigurationMigration
{
    public string Name => "v1.18.2";
    
    public bool ShouldMigrate(PluginConfiguration config)
    {
        return !config.MigratedTo1_18_2;
    }

    public void Migrate(PluginConfiguration config)
    {
        // This can be null if the configuration file was created on v1.18.1
        var currentVoicePresets = config.GetVoiceConfig().CurrentVoicePresets;
        if (currentVoicePresets != null)
        {
            foreach (var (backend, id) in currentVoicePresets)
            {
                config.GetVoiceConfig().CurrentVoicePreset[backend] = id;
            }
        }

        var ungenderedVoicePresetsBroken = config.GetVoiceConfig().UngenderedVoicePresetsBroken;
        if (ungenderedVoicePresetsBroken != null)
        {
            foreach (var (backend, o) in ungenderedVoicePresetsBroken)
            {
                if (o is int id)
                {
                    config.GetVoiceConfig().GetUngenderedPresets(backend).Add(id);
                }
                else if (o is IEnumerable<int> ids)
                {
                    if (config.GetVoiceConfig().UngenderedVoicePresets[backend] == null)
                    {
                        config.GetVoiceConfig().UngenderedVoicePresets[backend] = new SortedSet<int>(ids);
                    }
                    else
                    {
                        foreach (var idv in ids)
                        {
                            config.GetVoiceConfig().GetUngenderedPresets(backend).Add(idv);
                        }
                    }
                }
            }
        }

        var maleVoicePresetsBroken = config.GetVoiceConfig().MaleVoicePresetsBroken;
        if (maleVoicePresetsBroken != null)
        {
            foreach (var (backend, o) in maleVoicePresetsBroken)
            {
                if (o is int id)
                {
                    config.GetVoiceConfig().GetMalePresets(backend).Add(id);
                }
                else if (o is IEnumerable<int> ids)
                {
                    if (config.GetVoiceConfig().MaleVoicePresets[backend] == null)
                    {
                        config.GetVoiceConfig().MaleVoicePresets[backend] = new SortedSet<int>(ids);
                    }
                    else
                    {
                        foreach (var idv in ids)
                        {
                            config.GetVoiceConfig().GetMalePresets(backend).Add(idv);
                        }
                    }
                }
            }
        }

        var femaleVoicePresetsBroken = config.GetVoiceConfig().FemaleVoicePresetsBroken;
        if (femaleVoicePresetsBroken != null)
        {
            foreach (var (backend, o) in femaleVoicePresetsBroken)
            {
                if (o is int id)
                {
                    config.GetVoiceConfig().GetFemalePresets(backend).Add(id);
                }
                else if (o is IEnumerable<int> ids)
                {
                    if (config.GetVoiceConfig().FemaleVoicePresets[backend] == null)
                    {
                        config.GetVoiceConfig().FemaleVoicePresets[backend] = new SortedSet<int>(ids);
                    }
                    else
                    {
                        foreach (var idv in ids)
                        {
                            config.GetVoiceConfig().GetFemalePresets(backend).Add(idv);
                        }
                    }
                }
            }
        }

        config.MigratedTo1_18_2 = true;
    }
}