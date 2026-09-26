using System;
using System.Reflection;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class LayerPreviewOpacityTests
{
    [Fact]
    public void ClampAndPercent_StayInsideThePreviewRange()
    {
        Assert.Equal(0f, LayerPreviewOpacity.Clamp(float.NaN));
        Assert.Equal(0f, LayerPreviewOpacity.Clamp(-0.2f));
        Assert.Equal(1f, LayerPreviewOpacity.Clamp(1.4f));
        Assert.Equal(0.4f, LayerPreviewOpacity.Clamp(0.4f));
        Assert.Equal(0, LayerPreviewOpacity.ToPercent(0f));
        Assert.Equal(40, LayerPreviewOpacity.ToPercent(0.4f));
        Assert.Equal(100, LayerPreviewOpacity.ToPercent(0.995f));
        Assert.Equal(0f, LayerPreviewOpacity.FromPercent(-3));
        Assert.Equal(1f, LayerPreviewOpacity.FromPercent(140));
        Assert.Equal(0.4f, LayerPreviewOpacity.FromPercent(40));
        Assert.Equal("40 %", LayerPreviewOpacity.PercentCaption(40));
        Assert.Equal("0 %", LayerPreviewOpacity.PercentCaption(-8));
        Assert.Equal("100 %", LayerPreviewOpacity.PercentCaption(250));
    }

    [Fact]
    public void DrawAlpha_HidesMaskedLayers_DimsTheOthers_AndKeepsThePaintTarget()
    {
        Assert.Equal(0.4f, LayerPreviewOpacity.DimOthersFactor);
        Assert.Equal(0f, LayerPreviewOpacity.DrawAlpha(visible: false, paintTarget: true, previewOpacity: 1f, dimOthers: true));
        Assert.Equal(1f, LayerPreviewOpacity.DrawAlpha(visible: true, paintTarget: true, previewOpacity: 1f, dimOthers: true));
        Assert.Equal(0.4f, LayerPreviewOpacity.DrawAlpha(visible: true, paintTarget: false, previewOpacity: 1f, dimOthers: true));
        Assert.Equal(0.5f, LayerPreviewOpacity.DrawAlpha(visible: true, paintTarget: true, previewOpacity: 0.5f, dimOthers: true));
        Assert.Equal(0.2f, LayerPreviewOpacity.DrawAlpha(visible: true, paintTarget: false, previewOpacity: 0.5f, dimOthers: true));
        Assert.Equal(0.5f, LayerPreviewOpacity.DrawAlpha(visible: true, paintTarget: false, previewOpacity: 0.5f, dimOthers: false));
        Assert.Equal(0f, LayerPreviewOpacity.DrawAlpha(visible: true, paintTarget: false, previewOpacity: float.NaN, dimOthers: false));
    }

    [Fact]
    public void State_FitsByIndex_AndResetClearsTheVeil()
    {
        var state = new LayerPreviewState();
        state.Fit(2);
        state.SetOpacity(0, 0.25f);
        state.DimOthers = true;
        state.Fit(3);
        Assert.Equal(0.25f, state.Opacity(0));
        Assert.Equal(1f, state.Opacity(1));
        Assert.Equal(1f, state.Opacity(2));
        Assert.Equal(1f, state.Opacity(5));
        state.SetOpacity(9, 0.1f);
        Assert.Equal(3, state.Count);
        Assert.Equal(0.4f, state.DrawAlpha(1, visible: true, activeIndex: 0));
        Assert.Equal(0.25f, state.DrawAlpha(0, visible: true, activeIndex: 0));

        state.Fit(1);
        Assert.Equal(0.25f, state.Opacity(0));
        Assert.True(state.DimOthers);

        state.Reset(2);
        Assert.False(state.DimOthers);
        Assert.Equal(1f, state.Opacity(0));
        Assert.Equal(1f, state.Opacity(1));
        Assert.Equal(1f, state.DrawAlpha(0, visible: true, activeIndex: 1));
        state.DimOthers = true;
        Assert.Equal(1f, state.DrawAlpha(1, visible: true, activeIndex: 1));
        Assert.Equal(0.4f, state.DrawAlpha(0, visible: true, activeIndex: 1));
        Assert.Equal(0f, state.DrawAlpha(0, visible: false, activeIndex: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Fit(-1));
    }

    [Fact]
    public void Preview_DoesNotChangeFmapV5OrV6_Protocol_OrTileSize()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);
        Assert.Equal((byte)6, MapFormat.CurrentWriteVersion);
        Assert.Null(typeof(Layer).GetProperty("Opacity"));
        Assert.Null(typeof(Layer).GetProperty("PreviewOpacity"));

        var sheet = new Map { Width = 3, Height = 2, Name = "Feuille" };
        sheet.Layers.Add(new Layer { LayerType = LayerType.Ground, DisplayName = "Sol", Visible = true });
        sheet.Layers.Add(new Layer { LayerType = LayerType.Attributes, Visible = false, Locked = true });
        var serializer = new MapSerializer();
        var v5 = serializer.Serialize(sheet);

        var preview = new LayerPreviewState();
        preview.Fit(sheet.Layers.Count);
        preview.SetOpacity(0, 0.25f);
        preview.DimOthers = true;
        Assert.Equal(v5, serializer.Serialize(sheet));
        Assert.Equal((byte)5, v5[4]);
        Assert.Equal(0, sheet.TileSizePixels);
        Assert.True(sheet.Layers[0].Visible);
        Assert.True(sheet.Layers[1].Locked);

        var tiles = MapFormat.CreateTileAssetMap("Tuiles", 4, 3);
        tiles.Layers.Add(new Layer { LayerType = LayerType.Ground });
        tiles.Layers.Add(new Layer { LayerType = LayerType.Fringe });
        var v6 = MapFormat.Write(tiles);
        preview.Reset(tiles.Layers.Count);
        preview.SetOpacity(1, 0.4f);
        preview.DimOthers = true;
        Assert.Equal(v6, MapFormat.Write(tiles));
        Assert.Equal((byte)6, v6[4]);
        Assert.Equal(48, tiles.TileSizePixels);
        Assert.Equal(TileGraphicIdentity.TileAsset, tiles.GraphicIdentity);

        var back = MapFormat.Read(v6);
        Assert.Equal(48, back.TileSizePixels);
        Assert.Equal(TileGraphicIdentity.TileAsset, back.GraphicIdentity);
        Assert.Equal(2, back.Layers.Count);
        Assert.True(back.Layers[0].Visible);
        Assert.False(back.Layers[0].Locked);
        Assert.Equal(LayerType.Fringe, back.Layers[1].LayerType);
    }
}
