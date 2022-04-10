﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Logging;
using ImGuiNET;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;

namespace TextToTalk.UI.Dalamud.Lexicons;

public class LexiconRepositorySubwindow
{
    private readonly LexiconManager lexiconManager;
    private readonly LexiconRepository lexiconRepository;

    private LexiconPackageInfo selectedPackage;
    private bool selectedPackageIsInstalled;

    private readonly object rpLock;
    private IList<LexiconPackageInfo> remotePackages;
    private bool remotePackagesLoading;
    private bool remotePackagesLoaded;

    public LexiconRepositorySubwindow(LexiconManager lm, LexiconRepository lr)
    {
        this.lexiconManager = lm;
        this.lexiconRepository = lr;

        this.rpLock = true;

        Task.Factory.StartNew(LoadRemoteLexicons);
    }

    public void Draw(ref bool visible)
    {
        ImGui.SetNextWindowSize(new Vector2(520, 480), ImGuiCond.FirstUseEver);
        ImGui.Begin("Lexicon Repository##TextToTalkLexiconRepositorySubwindow", ref visible);
        {
            if (!this.remotePackagesLoaded && !this.remotePackagesLoading)
            {
                // Fetch the list of lexicon packages
                PluginLog.Log("Fetching lexicon package list...");
                _ = LoadPackageInfo();
            }
            else if (ImGui.BeginTable("##LexiconRepoList", 2, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Lexicon", ImGuiTableColumnFlags.None, 380f);
                ImGui.TableSetupColumn("Author", ImGuiTableColumnFlags.None, 120f);
                ImGui.TableHeadersRow();

                if (this.remotePackages != null)
                {
                    lock (this.rpLock)
                    {
                        foreach (var package in this.remotePackages)
                        {
                            ImGui.TableNextRow();

                            ImGui.TableSetColumnIndex(0);
                            if (ImGui.Selectable($"##LexiconRepoList_{package.InternalName}", this.selectedPackage == package, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap, Vector2.Zero))
                            {
                                this.selectedPackage = package;
                                this.selectedPackageIsInstalled = this.lexiconRepository
                                    .GetPackage(this.selectedPackage.InternalName).IsInstalled();
                            }
                            ImGui.SameLine(0f, 0f);
                            ImGui.Text(package.Name); // TODO: Show something different if it's installed or has an update

                            ImGui.TableSetColumnIndex(1);
                            ImGui.Text(package.Author);
                        }
                    }
                }
                
                ImGui.EndTable();
            }

            lock (this.rpLock)
            {
                if (this.selectedPackage != null)
                {
                    ImGui.Text($"{this.selectedPackage.Name} by {this.selectedPackage.Author}");
                    ImGui.TextWrapped(this.selectedPackage.Description);

                    ImGui.Spacing();
                    if (!this.selectedPackageIsInstalled)
                    {
                        if (ImGui.Button("Install"))
                        {
                            _ = InstallRemoteLexicon(this.selectedPackage.InternalName);
                            this.selectedPackageIsInstalled = true;
                        }
                    }
                    else if (ImGui.Button("Uninstall"))
                    {
                        UninstallRemoteLexicon(this.selectedPackage.InternalName);
                        this.selectedPackageIsInstalled = false;
                    }
                }
            }
        }
        ImGui.End();
    }

    private async Task LoadPackageInfo()
    {
        if (this.remotePackagesLoading) return;
        this.remotePackagesLoading = true;
        var packages = await Task.WhenAll((await this.lexiconRepository.FetchPackages())
            .Select(package =>
            {
                var packageName = LexiconPackage.GetInternalName(package.Path);
                return this.lexiconRepository.GetPackage(packageName);
            })
            .Select(package => package.GetPackageInfo())
            .ToList());
        lock (this.rpLock)
        {
            this.remotePackages = packages;
            this.selectedPackage = null;
        }
        this.remotePackagesLoading = false;
        this.remotePackagesLoaded = true;
    }

    private void LoadRemoteLexicons()
    {
        IEnumerable<LexiconPackage> packages;
        try
        {
            packages = Directory.EnumerateDirectories(this.lexiconRepository.CachePath)
                .Select(dir => new DirectoryInfo(dir).Name)
                .Where(packageName => this.lexiconRepository.GetPackage(packageName).IsInstalled())
                .Select(packageName => this.lexiconRepository.GetPackage(packageName));
        }
        catch (DirectoryNotFoundException)
        {
            // Cache folder has not yet been created
            return;
        }

        foreach (var package in packages)
        {
            var packageInfo = package.GetPackageInfoLocal();
            var files = packageInfo.Files;
            foreach (var file in files)
            {
                var lexiconData = package.GetPackageFileLocal(file);
                if (lexiconData == null)
                {
                    PluginLog.Error($"Local data for lexicon file \"{file}\" of lexicon \"{package.PackageName}\" not found! Please reinstall this lexicon.");
                    continue;
                }

                try
                {
                    var lexiconId = GetLexiconFileId(package.PackageName, file);
                    this.lexiconManager.AddLexicon(lexiconData, lexiconId);
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Failed to load lexicon.");
                }
            }
        }
    }

    private async Task InstallRemoteLexicon(string packageName)
    {
        // Fetch lexicon file list
        var package = this.lexiconRepository.GetPackage(packageName);
        var packageInfo = await package.GetPackageInfo();

        // Download each file and load it
        foreach (var file in packageInfo.Files)
        {
            var lexiconId = GetLexiconFileId(packageName, file);
            await using var lexiconData = await package.GetPackageFile(file);
            try
            {
                this.lexiconManager.AddLexicon(lexiconData, lexiconId);
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to load lexicon.");
            }
        }
    }

    private async Task<IList<LexiconPackageInfo>> CheckRemoteLexiconUpdates()
    {
        var toUpdate = new List<LexiconPackageInfo>();
        var items = await this.lexiconRepository.FetchPackages();
        foreach (var item in items)
        {
            var packageName = LexiconPackage.GetInternalName(item.Path);
            var package = this.lexiconRepository.GetPackage(packageName);

            // Only check updates for installed packages
            if (!package.IsInstalled()) continue;

            var info = await package.GetPackageInfo();
            foreach (var file in info.Files)
            {
                if (await package.HasUpdate(file))
                {
                    toUpdate.Add(info);
                    break;
                }
            }
        }

        return toUpdate;
    }

    private async Task UpdateRemoteLexicon(string packageName)
    {
        var package = this.lexiconRepository.GetPackage(packageName);
        var packageInfo = await package.GetPackageInfo();

        // Download each updated file and (re)load it
        foreach (var file in packageInfo.Files)
        {
            if (!await package.HasUpdate(file)) continue;

            var lexiconId = GetLexiconFileId(packageName, file);
            await using var lexiconData = await package.GetPackageFile(file);
            this.lexiconManager.RemoveLexicon(lexiconId);
            this.lexiconManager.AddLexicon(lexiconData, lexiconId);
        }
    }

    private void UninstallRemoteLexicon(string packageName)
    {
        // TODO: Read the local package metadata file instead of doing this
        var lexiconDir = Path.Combine(this.lexiconRepository.CachePath, packageName);
        var files = Directory.EnumerateFiles(lexiconDir);
        foreach (var file in files.Where(f => f.EndsWith(".pls")))
        {
            var filename = Path.GetFileName(file);
            var lexiconId = GetLexiconFileId(packageName, filename);
            this.lexiconManager.RemoveLexicon(lexiconId);
        }

        this.lexiconRepository.RemovePackage(packageName);
    }

    private static string GetLexiconFileId(string packageName, string filename)
    {
        return $"{packageName}.{filename}";
    }
}