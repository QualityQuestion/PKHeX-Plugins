using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoModPlugins.GUI;
using AutoModPlugins.Properties;
using PKHeX.Core;
using PKHeX.Core.AutoMod;

namespace AutoModPlugins;

public class SmogonLivingDex : AutoModPlugin
{
    public override string Name => "Generate Smogon Living Dex";
    public override int Priority => 1;

    protected override void AddPluginControl(ToolStripDropDownItem modmenu)
    {
        var ctrl = new ToolStripMenuItem(Name)
        {
            Image = WinFormsUtil.GetIconForTheme(Resources.smogongenner, Application.IsDarkModeEnabled),
        };
        ctrl.Click += GenSmogonLivingDex;
        ctrl.Name = "Menu_SmogonLivingDex";
        modmenu.DropDownItems.Add(ctrl);
    }

    private async void GenSmogonLivingDex(object? sender, EventArgs e)
    {
        var formats = string.Join(", ", ModLogic.SmogonLivingDexFormats);
        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo,
            "Generate a Smogon Living Dex?",
            $"One Pokémon per species will be generated using Smogon sets.\nFormat priority: {formats}\n\nNote: this requires an internet connection and may take several minutes.");
        if (prompt != DialogResult.Yes)
            return;

        var sav = SaveFileEditor.SAV;
        var t = new ALMStatusBar("Smogon Living Dex", sav.MaxSpeciesID)
        {
            Count = ModLogic.TrackingCount
        };
        t.Show();

        _ = Task.Run(() => PollingLoop(t));

        var dex = await Task.Run(() => sav.GenerateSmogonLivingDex(sav.Personal));
        List<PKM> extra = [];
        t.Close();

        prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Overwrite any existing Pokémon in your boxes?");
        int generated = IngestToBoxes(sav, dex, extra, prompt == DialogResult.Yes);
        System.Diagnostics.Debug.WriteLine($"Generated Smogon Living Dex with {generated} entries.");
        SaveFileEditor.ReloadSlots();

        if (extra.Count == 0)
            return;

        prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "This Living Dex does not fit in all boxes. Save the extra to a folder?");
        if (prompt != DialogResult.Yes)
            return;

        using var ofd = new FolderBrowserDialog();
        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        foreach (var f in extra)
        {
            File.WriteAllBytes($"{ofd.SelectedPath}/{f.FileName}", f.DecryptedPartyData);
        }
    }

    private static void PollingLoop(ALMStatusBar t)
    {
        int lastCount = -1;
        while (!t.IsDisposed)
        {
            if (ModLogic.TrackingCount != lastCount)
            {
                lastCount = ModLogic.TrackingCount;
                t.Count = lastCount;
            }
        }
    }

    private static int IngestToBoxes(SaveFile sav, IEnumerable<PKM> list, IList<PKM> extra, bool overwrite, int slot = 0)
    {
        int generated = 0;
        foreach (var pk in list)
        {
            generated++;
            if (TryAdd(sav, extra, pk, overwrite, ref slot))
                continue;
            do
            {
                slot++;
            }
            while (!TryAdd(sav, extra, pk, overwrite, ref slot));
        }
        return generated;
    }

    private static bool TryAdd(SaveFile sav, IList<PKM> extra, PKM pk, bool overwrite, ref int slot)
    {
        if (slot >= sav.SlotCount)
        {
            extra.Add(pk);
            return true;
        }
        if (!overwrite && sav.NextOpenBoxSlot(slot - 1) != slot)
            return false;
        if (!sav.IsBoxSlotOverwriteProtected(slot))
        {
            sav.SetBoxSlotAtIndex(pk, slot++);
            return true;
        }
        return false;
    }
}
