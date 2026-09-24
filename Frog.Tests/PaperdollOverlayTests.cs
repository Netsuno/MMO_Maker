using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Frog.Core.Gameplay;
using Xunit;

namespace Frog.Tests;

/// <summary>Paperdoll draw order and equip → overlay visibility (Netsun).</summary>
public sealed class PaperdollOverlayTests
{
    private static readonly (int R, int G, int B) HatFill = (196, 48, 72);
    private static readonly (int R, int G, int B) ArmorFill = (42, 138, 78);
    private static readonly (int R, int G, int B) BladeFill = (214, 224, 232);

    [Fact]
    public void DrawOrder_IsBodyTunicArmorHeadHatWeapon()
    {
        Assert.Equal(
            new[]
            {
                PaperdollLayer.Body,
                PaperdollLayer.Tunic,
                PaperdollLayer.Armor,
                PaperdollLayer.Head,
                PaperdollLayer.Hat,
                PaperdollLayer.Weapon,
            },
            PaperdollDrawOrder.All);

        var hat = Array.IndexOf(PaperdollDrawOrder.All, PaperdollLayer.Hat);
        var head = Array.IndexOf(PaperdollDrawOrder.All, PaperdollLayer.Head);
        var weapon = Array.IndexOf(PaperdollDrawOrder.All, PaperdollLayer.Weapon);
        Assert.True(head >= 0 && hat > head, "hat is drawn after the head");
        Assert.True(weapon > hat, "weapon is drawn after the hat");

        var assets = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "PlayerWorldAssets.cs"));
        var cursor = assets.IndexOf("CompositeDrawOrder", StringComparison.Ordinal);
        foreach (var name in new[] { "Body", "Tunic", "Armor", "Head", "Hat", "Weapon" })
        {
            var at = assets.IndexOf("PlayerSpriteSlot." + name, cursor, StringComparison.Ordinal);
            Assert.True(at > cursor, name);
            cursor = at;
        }

        var client = ReadEnumMembers(
            File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "PlayerSpriteSlot.cs")),
            "PlayerSpriteSlot");
        Assert.Equal(PaperdollDrawOrder.All.Length, client.Count);
        for (var i = 0; i < client.Count; i++)
        {
            Assert.Equal(PaperdollDrawOrder.All[i].ToString(), client[i].Name);
            Assert.Equal((int)PaperdollDrawOrder.All[i], client[i].Value);
        }
    }

    [Fact]
    public void EquippedItems_ShowOnlyThoseOverlays_BodyAndHeadAlways()
    {
        var none = PaperdollOverlaySet.FromItems(null, null, null);
        Assert.True(none.IsLayerVisible(PaperdollLayer.Body));
        Assert.True(none.IsLayerVisible(PaperdollLayer.Head));
        Assert.False(none.IsLayerVisible(PaperdollLayer.Tunic));
        Assert.False(none.IsLayerVisible(PaperdollLayer.Armor));
        Assert.False(none.IsLayerVisible(PaperdollLayer.Hat));
        Assert.False(none.IsLayerVisible(PaperdollLayer.Weapon));

        var weaponId = Guid.NewGuid();
        var weaponOnly = PaperdollOverlaySet.FromItems(weaponId, null, null);
        Assert.True(weaponOnly.Weapon);
        Assert.False(weaponOnly.Armor);
        Assert.False(weaponOnly.Hat);
        Assert.False(weaponOnly.Tunic);
        Assert.True(weaponOnly.IsLayerVisible(PaperdollLayer.Body));
        Assert.True(weaponOnly.IsLayerVisible(PaperdollLayer.Head));

        var armorOnly = PaperdollOverlaySet.FromItems(null, Guid.NewGuid(), null);
        Assert.True(armorOnly.Armor);
        Assert.False(armorOnly.Weapon);
        Assert.False(armorOnly.Hat);

        var hatOnly = PaperdollOverlaySet.FromItems(null, null, Guid.NewGuid());
        Assert.True(hatOnly.Hat);
        Assert.False(hatOnly.Weapon);
        Assert.False(hatOnly.Armor);

        var full = PaperdollOverlaySet.FromItems(weaponId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.True(full.Weapon && full.Armor && full.Hat && full.Tunic);

        var emptyGuids = PaperdollOverlaySet.FromItems(Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty);
        Assert.False(emptyGuids.Weapon || emptyGuids.Armor || emptyGuids.Hat || emptyGuids.Tunic);
    }

    [Fact]
    public void ServerSlots_StayWeaponAndArmor_HeadwearIsClientOnly()
    {
        Assert.Equal(1, (byte)EquipmentSlotKind.Weapon);
        Assert.Equal(2, (byte)EquipmentSlotKind.Armor);

        var slot = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Models", "EquipmentSlot.cs"));
        var members = ReadEnumMembers(slot, "EquipmentSlot");
        Assert.Contains(members, m => m.Name == "Weapon" && m.Value == (int)EquipmentSlotKind.Weapon);
        Assert.Contains(members, m => m.Name == "Armor" && m.Value == (int)EquipmentSlotKind.Armor);
        Assert.Contains(members, m => m.Name == "Headwear" && m.Value == 3);
        Assert.Contains(members, m => m.Name == "Offhand" && m.Value == 4);
        Assert.Contains(members, m => m.Name == "Tunic" && m.Value == 5);
        Assert.Contains("EquipmentSlotKind.Weapon", slot, StringComparison.Ordinal);
        Assert.Contains("EquipmentSlotKind.Armor", slot, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientWiresSnapshotToLocalAppearance_HatIsNotAProtocolField()
    {
        var service = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Services", "EquipmentService.cs"));
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var renderer = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "MapViewRenderer.cs"));
        var assets = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "PlayerWorldAssets.cs"));
        var panel = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "EquipmentPanel.cs"));

        Assert.Contains("PaperdollOverlaySet.FromItems", service, StringComparison.Ordinal);
        Assert.Contains("equipment.WeaponItemId", service, StringComparison.Ordinal);
        Assert.Contains("equipment.ArmorItemId", service, StringComparison.Ordinal);
        Assert.Contains("equipment.HeadwearItemId", service, StringComparison.Ordinal);
        Assert.Contains("equipment.TunicItemId", service, StringComparison.Ordinal);
        Assert.DoesNotContain("OffhandItemId", service, StringComparison.Ordinal);

        Assert.Contains("localAppearance: EquipmentService.ToOverlaySet(_paperdoll)", shell, StringComparison.Ordinal);
        Assert.Contains("_hudStatus.ApplyPortrait(EquipmentService.ToOverlaySet(_paperdoll))", shell, StringComparison.Ordinal);
        Assert.Contains("_paperdoll.WithServerLoadout(snapshot)", shell, StringComparison.Ordinal);
        Assert.Contains("Equipment.LocalHeadwearItemId", shell, StringComparison.Ordinal);
        Assert.Contains("Porter le casque", panel, StringComparison.Ordinal);
        Assert.Contains("Retirer le casque", panel, StringComparison.Ordinal);
        Assert.Contains("Casque: porté (local)", panel, StringComparison.Ordinal);

        Assert.Contains("PaperdollOverlaySet localAppearance", renderer, StringComparison.Ordinal);
        Assert.Contains("localPose, localAppearance", renderer, StringComparison.Ordinal);
        Assert.Contains("foreach (var slot in CompositeDrawOrder)", assets, StringComparison.Ordinal);

        var wire = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Protocol", "Phase7GameplayWire.cs"));
        Assert.Contains("EquippedWeaponItemId", wire, StringComparison.Ordinal);
        Assert.Contains("EquippedArmorItemId", wire, StringComparison.Ordinal);
        Assert.DoesNotContain("Headwear", wire, StringComparison.Ordinal);
    }

    [Fact]
    public void OverlayPngs_AreOriginalMarkers_FourDirections()
    {
        var world = Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World");
        var hat = ReadPng(Path.Combine(world, "player-hat.png"));
        var armor = ReadPng(Path.Combine(world, "player-armor.png"));
        var weapon = ReadPng(Path.Combine(world, "player-weapon.png"));
        Assert.Equal((32, 32), (hat.Width, hat.Height));
        Assert.Equal((32, 32), (armor.Width, armor.Height));
        Assert.Equal((32, 32), (weapon.Width, weapon.Height));
        Assert.True(CountColor(hat, HatFill) >= 12, "hat fill");
        Assert.True(CountColor(armor, ArmorFill) >= 8, "armor fill");
        Assert.True(CountColor(weapon, BladeFill) >= 4, "blade fill");
        Assert.True(CountTransparent(hat) > hat.Width * hat.Height / 2);
        Assert.DoesNotContain(HatFill, ColorsOf(ReadPng(Path.Combine(world, "player-head.png"))));
        Assert.DoesNotContain(ArmorFill, ColorsOf(ReadPng(Path.Combine(world, "player-body.png"))));

        var walkHat = ReadPng(Path.Combine(world, "player-walk-hat.png"));
        var walkArmor = ReadPng(Path.Combine(world, "player-walk-armor.png"));
        var walkWeapon = ReadPng(Path.Combine(world, "player-walk-weapon.png"));
        Assert.Equal((96, 128), (walkHat.Width, walkHat.Height));
        Assert.Equal((96, 128), (walkArmor.Width, walkArmor.Height));
        Assert.Equal((96, 128), (walkWeapon.Width, walkWeapon.Height));
        Assert.True(hat.Pixels.SequenceEqual(Crop(walkHat, col: 1, row: 0)));
        Assert.True(armor.Pixels.SequenceEqual(Crop(walkArmor, col: 1, row: 0)));
        Assert.True(weapon.Pixels.SequenceEqual(Crop(walkWeapon, col: 1, row: 0)));

        var southBlade = XsOf(weapon, BladeFill);
        var leftBlade = XsOf(CropCell(walkWeapon, col: 1, row: 1), BladeFill);
        Assert.NotEmpty(southBlade);
        Assert.NotEmpty(leftBlade);
        Assert.True(Average(southBlade) > 16, "south blade sits on the right");
        Assert.True(Average(leftBlade) < 16, "left blade sits on the left");

        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Frog.Client.csproj"));
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-hat.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-armor.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-weapon.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-walk-hat.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-walk-armor.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-walk-weapon.png\"", csproj, StringComparison.Ordinal);

        var generator = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "generate-paperdoll-overlays.py"));
        Assert.Contains("never a Graal sheet", generator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("urllib", generator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("urlopen", generator, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StatusDoc_RecordsHatAfterHead_AndLocalOnlyGap()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS-player-skin-v2.md"));
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("body → tunic → armor → head → hat → weapon", text, StringComparison.Ordinal);
        Assert.Contains("Porter le casque", text, StringComparison.Ordinal);
        Assert.Contains("PositionUpdate", text, StringComparison.Ordinal);
        Assert.Contains("Pas de bump protocole", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Graal sheet", text, StringComparison.OrdinalIgnoreCase);
    }

    private static List<(string Name, int Value)> ReadEnumMembers(string source, string enumName)
    {
        var start = source.IndexOf("enum " + enumName, StringComparison.Ordinal);
        Assert.True(start >= 0, enumName);
        var brace = source.IndexOf('{', start);
        var end = source.IndexOf('}', brace);
        var body = source.Substring(brace + 1, end - brace - 1);
        var list = new List<(string Name, int Value)>();
        foreach (Match match in Regex.Matches(body, @"(\w+)\s*=\s*(\d+)"))
        {
            list.Add((match.Groups[1].Value, int.Parse(match.Groups[2].Value)));
        }

        return list;
    }

    private readonly record struct Png(int Width, int Height, List<(byte R, byte G, byte B, byte A)> Pixels);

    private static Png ReadPng(string path)
    {
        Assert.True(File.Exists(path), path);
        var data = File.ReadAllBytes(path);
        Assert.Equal(0x89, data[0]);
        Assert.Equal((byte)'P', data[1]);
        var pos = 8;
        var width = 0;
        var height = 0;
        var idat = new List<byte>();
        while (pos + 8 <= data.Length)
        {
            var length = ReadBigEndianInt32(data, pos);
            var tag = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            pos += 8;
            if (tag == "IHDR")
            {
                width = ReadBigEndianInt32(data, pos);
                height = ReadBigEndianInt32(data, pos + 4);
            }
            else if (tag == "IDAT")
            {
                for (var i = 0; i < length; i++)
                {
                    idat.Add(data[pos + i]);
                }
            }
            else if (tag == "IEND")
            {
                break;
            }

            pos += length + 4;
        }

        var raw = Decompress(idat.ToArray());
        var stride = width * 4;
        var rows = new byte[height][];
        var iRaw = 0;
        var prev = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[iRaw++];
            var row = new byte[stride];
            Buffer.BlockCopy(raw, iRaw, row, 0, stride);
            iRaw += stride;
            Unfilter(filter, row, prev, 4);
            rows[y] = row;
            prev = row;
        }

        var pixels = new List<(byte R, byte G, byte B, byte A)>(width * height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var o = x * 4;
                pixels.Add((rows[y][o], rows[y][o + 1], rows[y][o + 2], rows[y][o + 3]));
            }
        }

        return new Png(width, height, pixels);
    }

    private static void Unfilter(byte filter, byte[] row, byte[] prev, int bpp)
    {
        for (var x = 0; x < row.Length; x++)
        {
            var left = x >= bpp ? row[x - bpp] : (byte)0;
            var up = prev[x];
            var ul = x >= bpp ? prev[x - bpp] : (byte)0;
            row[x] = filter switch
            {
                0 => row[x],
                1 => (byte)(row[x] + left),
                2 => (byte)(row[x] + up),
                3 => (byte)(row[x] + ((left + up) / 2)),
                4 => (byte)(row[x] + Paeth(left, up, ul)),
                _ => throw new InvalidOperationException("PNG filter " + filter),
            };
        }
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        return pb <= pc ? b : c;
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static List<(byte R, byte G, byte B, byte A)> Crop(Png sheet, int col, int row)
        => CropCell(sheet, col, row);

    private static List<(byte R, byte G, byte B, byte A)> CropCell(Png sheet, int col, int row)
    {
        var cell = new List<(byte R, byte G, byte B, byte A)>(32 * 32);
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                cell.Add(sheet.Pixels[((row * 32) + y) * sheet.Width + (col * 32) + x]);
            }
        }

        return cell;
    }

    private static int CountColor(Png png, (int R, int G, int B) rgb)
        => CountColor(png.Pixels, rgb);

    private static int CountColor(List<(byte R, byte G, byte B, byte A)> pixels, (int R, int G, int B) rgb)
    {
        var n = 0;
        foreach (var px in pixels)
        {
            if (px.A != 0 && px.R == rgb.R && px.G == rgb.G && px.B == rgb.B)
            {
                n++;
            }
        }

        return n;
    }

    private static int CountTransparent(Png png)
    {
        var n = 0;
        foreach (var px in png.Pixels)
        {
            if (px.A == 0)
            {
                n++;
            }
        }

        return n;
    }

    private static HashSet<(int R, int G, int B)> ColorsOf(Png png)
    {
        var set = new HashSet<(int, int, int)>();
        foreach (var px in png.Pixels)
        {
            if (px.A != 0)
            {
                set.Add((px.R, px.G, px.B));
            }
        }

        return set;
    }

    private static List<int> XsOf(Png png, (int R, int G, int B) rgb)
        => XsOf(png.Pixels, png.Width, rgb);

    private static List<int> XsOf(List<(byte R, byte G, byte B, byte A)> pixels, (int R, int G, int B) rgb)
        => XsOf(pixels, 32, rgb);

    private static List<int> XsOf(List<(byte R, byte G, byte B, byte A)> pixels, int width, (int R, int G, int B) rgb)
    {
        var xs = new List<int>();
        for (var i = 0; i < pixels.Count; i++)
        {
            var px = pixels[i];
            if (px.A != 0 && px.R == rgb.R && px.G == rgb.G && px.B == rgb.B)
            {
                xs.Add(i % width);
            }
        }

        return xs;
    }

    private static double Average(List<int> values)
    {
        var sum = 0;
        foreach (var value in values)
        {
            sum += value;
        }

        return sum / (double)values.Count;
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
        => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
